using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 뮤턴트가 옥수수밭 BoxCollider(Trigger)에 진입하면:
///   1. CornSway 셰이더에 뮤턴트 루트 위치를 실시간 전달 → 근처 구역만 강하게 흔들림
///   2. 바스락 SFX를 3D 공간음으로 쿨타임마다 재생
/// HashSet으로 동일 뮤턴트의 여러 콜라이더 중복 처리를 방지합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
// AudioSource는 자식 오브젝트(SoundEmitter)에 동적 생성하므로 RequireComponent 불필요
public class CornfieldTrigger : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────
    // Inspector
    // ────────────────────────────────────────────────────────────

    [Header("셰이더 연동")]
    [Tooltip("CornSway_URP 셰이더가 적용된 옥수수 Plane Renderer 목록 (5개까지 등록 가능)")]
    public Renderer[] cornRenderers;

    [Header("사운드")]
    [Tooltip("밭 바스락 소리 AudioClip (여러 개 등록 시 랜덤 재생)")]
    public AudioClip[] rustleSounds;

    [Tooltip("사운드 재생 쿨타임 (초)")]
    public float soundCooldown = 0.6f;

    [Tooltip("뮤턴트 퇴장 후 사운드가 서서히 꺼지는 시간 (초)")]
    public float soundFadeOutDuration = 1.5f;

    [Range(0f, 1f)]
    public float rustleVolume = 0.7f;

    public float soundMinDistance = 1f;
    public float soundMaxDistance = 15f;

    // ────────────────────────────────────────────────────────────
    // Private
    // ────────────────────────────────────────────────────────────

    private Material[] cornMats;
    private AudioSource audioSource;
    private GameObject soundEmitter; // ★ BoxCollider와 분리된 사운드 전용 자식 오브젝트
    private float lastSoundTime = -999f;

    // ★ 중복 방지: 동일 뮤턴트의 여러 콜라이더를 하나로 묶음
    // Key = MutantAI 컴포넌트 (동일 뮤턴트 식별), Value = 해당 MutantAI 루트 Transform
    private HashSet<MutantAI> mutantsInZone = new HashSet<MutantAI>();

    // ────────────────────────────────────────────────────────────
    // Unity 라이프사이클
    // ────────────────────────────────────────────────────────────

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;

        // NavMeshAgent 뮤턴트는 Rigidbody가 없으므로, 이쪽에 Kinematic Rb 필요
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        // ★ AudioSource를 별도 자식 오브젝트에 생성
        // → soundEmitter.transform을 움직여도 부모(BoxCollider)는 고정됨
        soundEmitter = new GameObject("SoundEmitter");
        soundEmitter.transform.SetParent(this.transform);
        soundEmitter.transform.localPosition = Vector3.zero;

        audioSource = soundEmitter.AddComponent<AudioSource>();
        audioSource.playOnAwake  = false;
        audioSource.loop         = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance  = soundMinDistance;
        audioSource.maxDistance  = soundMaxDistance;
        audioSource.rolloffMode  = AudioRolloffMode.Linear;
        audioSource.volume       = rustleVolume;
    }

    private void Start()
    {
        if (cornRenderers != null && cornRenderers.Length > 0)
        {
            cornMats = new Material[cornRenderers.Length];
            for (int i = 0; i < cornRenderers.Length; i++)
            {
                if (cornRenderers[i] != null)
                    cornMats[i] = cornRenderers[i].material; // 인스턴스 복사본 생성
                else
                    Debug.LogWarning($"[CornfieldTrigger] cornRenderers[{i}]가 비어 있습니다.");
            }
        }
        else
        {
            Debug.LogWarning("[CornfieldTrigger] cornRenderers가 연결되지 않았습니다!");
        }

        ResetShader();
    }

    // ────────────────────────────────────────────────────────────
    // Trigger 이벤트
    // ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        // ★ 어떤 자식 콜라이더든 최상단 부모에서 MutantAI를 찾음
        MutantAI mutant = other.GetComponentInParent<MutantAI>();
        if (mutant == null) return;

        // ★ HashSet: 이미 등록된 뮤턴트면 Add 무시 → 11개 콜라이더 중복 방지
        if (mutantsInZone.Add(mutant))
        {
            Debug.Log($"[CornfieldTrigger] 뮤턴트 진입: {mutant.name} | Zone 내 뮤턴트 수: {mutantsInZone.Count}");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        MutantAI mutant = other.GetComponentInParent<MutantAI>();
        if (mutant == null) return;

        Vector3 mutantRootPos = mutant.transform.position;

        // ★ 셰이더에 뮤턴트 루트 위치 전달
        if (cornMats != null)
        {
            foreach (var mat in cornMats)
            {
                if (mat != null)
                    mat.SetVector("_MutantWorldPos", mutantRootPos);
            }

            // 3초마다 상세 검증 로그
            bool shouldLog = (Mathf.FloorToInt(Time.time) % 3 == 0
                              && Time.time - Mathf.Floor(Time.time) < 0.05f);
            if (shouldLog && cornMats.Length > 0 && cornMats[0] != null)
            {
                Vector4 stored = cornMats[0].GetVector("_MutantWorldPos");
                Debug.Log(
                    $"[CornfieldTrigger] 셰이더 상태\n" +
                    $"  머티리얼[0]명 : {cornMats[0].name}\n" +
                    $"  셰이더명   : {cornMats[0].shader?.name}\n" +
                    $"  전달한 위치 : {mutantRootPos}\n" +
                    $"  저장된 값   : {stored}"
                );
            }
        }
        else
        {
            if (Mathf.FloorToInt(Time.time) % 3 == 0 && Time.time - Mathf.Floor(Time.time) < 0.05f)
                Debug.LogError("[CornfieldTrigger] ❌ cornMats=null! Inspector에서 Corn Renderers를 연결했는지 확인하세요.");
        }

        TryPlayRustleSound(mutantRootPos);
    }


    private void OnTriggerExit(Collider other)
    {
        MutantAI mutant = other.GetComponentInParent<MutantAI>();
        if (mutant == null) return;

        // ★ HashSet에서 제거: 마지막 뮤턴트가 나갈 때만 초기화
        mutantsInZone.Remove(mutant);
        Debug.Log($"[CornfieldTrigger] 뮤턴트 퇴장: {mutant.name} | Zone 내 뮤턴트 수: {mutantsInZone.Count}");

        if (mutantsInZone.Count == 0)
        {
            ResetShader();
            // ★ 즉시 정지 대신 서서히 페이드아웃
            StartCoroutine(FadeOutAndStop());
        }
    }

    // ────────────────────────────────────────────────────────────
    // 헬퍼
    // ────────────────────────────────────────────────────────────

    private void TryPlayRustleSound(Vector3 atPosition)
    {
        // 사운드 페이드아웃 중에 뜨턴트가 다시 진입하면 볼륨 복원
        if (audioSource.volume < rustleVolume * 0.9f)
            audioSource.volume = rustleVolume;

        if (rustleSounds == null || rustleSounds.Length == 0)
        {
            Debug.LogWarning("[CornfieldTrigger] ❌ rustleSounds 배열이 비어 있습니다! Inspector에서 AudioClip을 등록해주세요.");
            return;
        }

        if (Time.time - lastSoundTime < soundCooldown) return;
        if (audioSource.isPlaying) return;

        lastSoundTime = Time.time;
        // ★ soundEmitter(자식)만 이동 → 부모 BoxCollider 위치 불변
        soundEmitter.transform.position = atPosition;

        int idx = Random.Range(0, rustleSounds.Length);
        audioSource.clip = rustleSounds[idx];
        audioSource.Play();

        Debug.Log($"[CornfieldTrigger] 폭 바스락 사운드 재생: {audioSource.clip.name} @ {atPosition}");
    }

    /// <summary>
    /// 뜨턴트가 박스를 보듞면 븼륨을 서서히 줄이며 사운드를 종료하는 코루틴.
    /// </summary>
    private System.Collections.IEnumerator FadeOutAndStop()
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < soundFadeOutDuration && audioSource.isPlaying)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / soundFadeOutDuration);
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = rustleVolume; // 다음 재생을 위해 볼륨 복원
    }

    private void ResetShader()
    {
        if (cornMats == null) return;
        foreach (var mat in cornMats)
        {
            if (mat != null)
                mat.SetVector("_MutantWorldPos", new Vector4(0f, -9999f, 0f, 0f));
        }
    }
}
