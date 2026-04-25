using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어그로 탄이 착탄한 지점에 생성되어 타이머를 관리하는 마커.
/// - effectDelay 초 후 폭발 SFX + 파티클 (1회)
/// - 이어서 연막 SFX + 파티클 (effectDuration 초 루프)
/// - 반경 내 적격 MutantAI에 OnAggroLured() 브로드캐스트
/// - effectDuration 종료 후 연막을 smokeFadeOutDuration에 걸쳐 서서히 페이드 아웃
/// - 모든 어그로 상태 MutantAI에 OnAggroExpired() 브로드캐스트 후 스스로 소멸
/// </summary>
public class AggroBulletMarker : MonoBehaviour
{
    // AggroBulletData —— RaycastShooter에서 Instantiate 직후 Init()으로 주입
    [HideInInspector] public float effectDelay          = 0.5f;
    [HideInInspector] public float smokeDelay           = 1.5f;  // 폭발→연막 추가 딜레이
    [HideInInspector] public float effectDuration       = 5.0f;
    [HideInInspector] public float smokeFadeOutDuration = 1.5f;
    [HideInInspector] public float aggroRadius          = 20f;
    [HideInInspector] public float circleRadius         = 3f;
    [HideInInspector] public float luredWalkSpeed       = 1.2f;

    [HideInInspector] public AudioClip   explosionSFX;
    [HideInInspector] public float       explosionVolume = 1f;  // 폭발 사운드 볼륨
    [HideInInspector] public GameObject  explosionParticlePrefab;
    [HideInInspector] public float       explosionScale = 1f;
    [HideInInspector] public AudioClip   smokeSFX;
    [HideInInspector] public float       smokeVolume    = 1f;  // 연막 사운드 볼륨
    [HideInInspector] public GameObject  smokeParticlePrefab;
    [HideInInspector] public float       smokeScale     = 1f;

    // 런타임 내부용
    private AudioSource     explosionAudio;
    private AudioSource     smokeAudio;
    private ParticleSystem  smokePS;

    // 이 마커에 의해 어그로가 끌린 뮤턴트 목록 (만료 시 전달용)
    private List<MutantAI> luredMutants = new List<MutantAI>();

    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// RaycastShooter.FireAggroBullet()에서 Instantiate 직후 호출합니다.
    /// ShopItemData의 수치를 주입하고 효과 코루틴을 시작합니다.
    /// </summary>
    public void Init(ShopItemData data)
    {
        effectDelay          = data.effectDelay;
        smokeDelay           = data.smokeDelay;
        effectDuration       = data.effectDuration;
        smokeFadeOutDuration = data.smokeFadeOutDuration;
        aggroRadius          = data.aggroRadius;
        circleRadius         = data.circleRadius;
        luredWalkSpeed       = data.luredWalkSpeed;

        explosionSFX            = data.explosionSFX;
        explosionVolume         = data.explosionVolume;
        explosionParticlePrefab = data.explosionParticlePrefab;
        explosionScale          = data.explosionScale;
        smokeSFX                = data.smokeSFX;
        smokeVolume             = data.smokeVolume;
        smokeParticlePrefab     = data.smokeParticlePrefab;
        smokeScale              = data.smokeScale;

        // AudioSource 2개 생성 (폭발 1회용 + 연막 루프용)
        explosionAudio = gameObject.AddComponent<AudioSource>();
        explosionAudio.playOnAwake  = false;
        explosionAudio.volume       = explosionVolume;
        explosionAudio.spatialBlend = 1f; // 3D 사운드
        explosionAudio.rolloffMode  = AudioRolloffMode.Linear; // ★ 핵심: 유니티 기본값(Logarithmic)의 급격한 볼륨 감소 방지
        explosionAudio.minDistance  = 10f;  // 10미터 안에서는 원본 볼륨 100% 유지
        explosionAudio.maxDistance  = 100f; // 100미터까지 서서히 감소

        smokeAudio = gameObject.AddComponent<AudioSource>();
        smokeAudio.playOnAwake  = false;
        smokeAudio.loop         = true;
        smokeAudio.volume       = smokeVolume;
        smokeAudio.spatialBlend = 1f;
        smokeAudio.rolloffMode  = AudioRolloffMode.Linear; // ★ 핵심
        smokeAudio.minDistance  = 10f;
        smokeAudio.maxDistance  = 100f;

        StartCoroutine(AggroSequenceRoutine());
    }

    // ──────────────────────────────────────────────────────────────────────────
    private IEnumerator AggroSequenceRoutine()
    {
        // ── [1단계] 착탄 후 effectDelay 초 대기 ──────────────────────────────
        float elapsed = 0f;
        while (elapsed < effectDelay)
        {
            if (KillCountManager.isGameEnding) { Destroy(gameObject); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── [2단계] 폭발! (1회) ───────────────────────────────────────────────
        // 폭발 파티클
        if (explosionParticlePrefab != null)
        {
            GameObject expGO = null;
            if (ObjectPoolManager.Instance != null)
            {
                expGO = ObjectPoolManager.Instance.SpawnFromPool(explosionParticlePrefab.name, transform.position, Quaternion.identity);
            }
            else
            {
                expGO = Instantiate(explosionParticlePrefab, transform.position, Quaternion.identity);
            }

            expGO.transform.localScale = Vector3.one * explosionScale;
            // ★ 핵심: Scaling Mode를 Hierarchy로 강제 설정해야 localScale이 파티클 크기에 반영됨
            foreach (ParticleSystem ps in expGO.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.prewarm = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
            
            // 5초 뒤 풀에 반납
            StartCoroutine(ReturnParticleToPoolRoutine(expGO, 5f));
        }
        // 폭발 SFX (1회)
        if (explosionSFX != null)
            explosionAudio.PlayOneShot(explosionSFX, explosionVolume);

        // ── [3단계] smokeDelay 추가 대기 후 연막 시작 ────────────────────────
        //    폭발과 연막이 동시에 터지는 느낌을 없애기 위해 별도로 대기합니다.
        elapsed = 0f;
        while (elapsed < smokeDelay)
        {
            if (KillCountManager.isGameEnding) { CleanupImmediate(); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── [4단계] 연막 시작 (루프) ──────────────────────────────────────────
        GameObject smokeGO = null;
        if (smokeParticlePrefab != null)
        {
            if (ObjectPoolManager.Instance != null)
            {
                smokeGO = ObjectPoolManager.Instance.SpawnFromPool(smokeParticlePrefab.name, transform.position, Quaternion.identity, transform);
            }
            else
            {
                smokeGO = Instantiate(smokeParticlePrefab, transform.position, Quaternion.identity, transform);
            }

            smokeGO.transform.localScale = Vector3.one * smokeScale;
            // ★ 핵심: Scaling Mode를 Hierarchy로 강제 설정해야 localScale이 파티클 크기에 반영됨
            foreach (ParticleSystem ps in smokeGO.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.prewarm = false; // 파티클이 켜질 때 ReStart 하는 것처럼 초기화
            }
            smokePS = smokeGO.GetComponent<ParticleSystem>();
            if (smokePS == null) smokePS = smokeGO.GetComponentInChildren<ParticleSystem>();
            if (smokePS != null)
            {
                smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                smokePS.Play(true);
            }
        }
        if (smokeSFX != null)
        {
            smokeAudio.clip = smokeSFX;
            smokeAudio.Play();
        }

        // ── [5단계] 주변 뮤턴트에게 어그로 브로드캐스트 ─────────────────────
        BroadcastAggro();

        // ── [6단계] effectDuration 초 대기 ───────────────────────────────────
        elapsed = 0f;
        while (elapsed < effectDuration)
        {
            if (KillCountManager.isGameEnding) { CleanupImmediate(); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── [6단계] 연막 서서히 페이드 아웃 ──────────────────────────────────
        // 파티클: 새 파티클 방출 중단 (기존 입자는 수명만큼 자연스럽게 사라짐)
        if (smokePS != null)
            smokePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // 사운드: 볼륨을 smokeFadeOutDuration에 걸쳐 0으로 Lerp
        if (smokeFadeOutDuration > 0f && smokeAudio != null && smokeAudio.isPlaying)
        {
            float startVol = smokeAudio.volume;
            elapsed = 0f;
            while (elapsed < smokeFadeOutDuration)
            {
                if (KillCountManager.isGameEnding) break;
                elapsed += Time.deltaTime;
                smokeAudio.volume = Mathf.Lerp(startVol, 0f, elapsed / smokeFadeOutDuration);
                yield return null;
            }
            smokeAudio.Stop();
        }
        else if (smokeAudio != null)
        {
            smokeAudio.Stop();
        }

        // ── [7단계] 어그로 만료 브로드캐스트 ────────────────────────────────
        foreach (MutantAI m in luredMutants)
        {
            if (m != null) m.OnAggroExpired();
        }
        luredMutants.Clear();

        // ── [8단계] 연막 파티클 반납 및 마커 소멸 ────────────────────────────────────────────────
        if (smokeGO != null)
        {
            if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.ReturnToPool(smokeGO);
            else Destroy(smokeGO);
        }
        Destroy(gameObject);
    }

    private IEnumerator ReturnParticleToPoolRoutine(GameObject particleObj, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (KillCountManager.isGameEnding) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (particleObj != null)
        {
            if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.ReturnToPool(particleObj);
            else Destroy(particleObj);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    private void BroadcastAggro()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, aggroRadius);

        foreach (Collider col in cols)
        {
            MutantAI mutant = col.GetComponent<MutantAI>() ?? col.GetComponentInParent<MutantAI>();
            if (mutant == null) continue;

            // 이미 목록에 있으면 중복 처리 방지
            if (luredMutants.Contains(mutant)) continue;

            // OnAggroLured가 내부에서 부적격(Crawling, 점프 중 등) 필터링을 수행
            bool accepted = mutant.OnAggroLured(transform.position, circleRadius, luredWalkSpeed);
            if (accepted)
                luredMutants.Add(mutant);
        }

        Debug.Log($"[AggroBulletMarker] 어그로 브로드캐스트 완료: {luredMutants.Count}마리 끌림");
    }

    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>게임 종료 시 연출 없이 즉시 정리합니다.</summary>
    private void CleanupImmediate()
    {
        if (smokeAudio != null) smokeAudio.Stop();
        if (smokePS    != null) smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (MutantAI m in luredMutants)
            if (m != null) m.OnAggroExpired();
        luredMutants.Clear();
        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>Scene 뷰에서 aggroRadius 구체 와이어프레임 표시 (디버깅용)</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, aggroRadius);
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, circleRadius);
    }
}
