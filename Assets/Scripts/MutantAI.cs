using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class MutantAI : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    
    // 타격 횟수 추적
    private int hitCount = 0;
    private bool isDead = false;
    private bool isReacting = false;

    private Transform targetCamera;
    
    public enum MutantPhase { ToEntrance, ToInvasion, ChasePlayer, Crawling }
    private MutantPhase currentPhase = MutantPhase.ToEntrance;

    [Header("Revival Settings")]
    [Tooltip("체크 시 일정 스테이지부터는 죽어도 한 번 기어오는 기믹이 추가됩니다.")]
    public bool enableRevive = false;
    [Tooltip("가짜 죽음 상태에서 부활하기까지 세뇌시키는 화면 대기 시간 (초)")]
    public float reviveDelay = 2.5f;
    [Tooltip("헤드샷으로 죽어서 머리가 없는 상태로 부활했을 때의 느린 속도")]
    public float slowCrawlSpeed = 1.0f;
    [Tooltip("바디샷으로 죽어서 머리가 있는 채로 부활했을 때의 빠른 속도")]
    public float fastCrawlSpeed = 3.5f;
    
    [Tooltip("기어오는 사망 모션이 따로 없을 경우, 원래 서서 죽는 모션 중 '어느 시점(0.0~1.0)'부터 틀어서 잘라 쓸 지 결정")]
    public float crawlDeathStartTime = 0.6f; 
    [Tooltip("애니메이터에 표시된 실제 죽는 모션의 네모 박스 이름 (State Name). 대소문자 정확해야 합니다.")]
    public string animDeathStateName = "Death";

    [Header("Revival Animation Triggers")]
    public string animSlowCrawlTrigger = "Crawl_Slow";
    public string animFastCrawlTrigger = "Crawl_Fast";

    // 실제 사망인지 가짜 사망인지 구분하는 변수
    private bool hasAlreadyRevived = false;
    private bool isTrueDead = false;

    private Transform villageEntrance;
    private Transform villageInvasion;

    [Header("Phase 1 Settings (ToEntrance)")]
    public float walkDurationMin = 2f;
    public float walkDurationMax = 4f;
    public float idleDurationMin = 1f;
    public float idleDurationMax = 2f;

    private Coroutine phase1Coroutine;

    [Header("Speed Settings")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;

    [Header("Player Chase Settings (Distance)")]
    [Tooltip("이 거리(m) 안으로 거리가 좁혀지면 걷기에서 '달리기'로 전환됩니다.")]
    public float runDistanceThreshold = 15f;
    [Tooltip("이 거리(m) 안으로 거리가 좁혀지면 '점프 공격'을 하고 게임이 끝납니다.")]
    public float attackDistanceThreshold = 3f;

    [Header("Reaction Settings")]
    [Tooltip("첫 총격 시 두리번거리는 대기 시간(초)")]
    public float lookAroundDuration = 4f;
    [Tooltip("두 번째 총격 시 스크림(포효) 애니메이션을 재생할지 여부")]
    public bool useScreamOnSecondHit = true;
    [Tooltip("스크림 애니메이션 재생 후 실제로 뛰어오기까지의 대기 시간(초)")]
    public float screamDuration = 2f;

    [Header("Audio Settings")]
    [Tooltip("2타격 포효 시 재생할 비명 소리 파일 (Inspector에 드래그)")]
    public AudioClip screamClip;
    [Tooltip("사망 시 모든 소리를 끊고 재생할 사망 소리 파일")]
    public AudioClip deathClip;
    
    [Header("Footstep Audio Settings")]
    [Tooltip("걷기 발자국 소리 루프")]
    public AudioClip walkStepClip;
    [Tooltip("뛰어오기(돌진) 발자국 소리 루프")]
    public AudioClip runStepClip;

    [Header("Animation Settings (Triggers/Bools)")]
    public string animWalkBool = "IsWalking";
    public string animRunTrigger = "run";
    public string animLookAroundTrigger = "LookAround";
    public string animScreamTrigger = "Scream";
    public string animJumpAttackTrigger = "jump_attack";
    public string animDeathTrigger = "dying";

    [Header("Jump Attack Override")]
    [Tooltip("점프 시 코드 기반으로 뻥튀기할 Y축 높이 한계점 (0이면 애니메이션 기본 점프만 사용합니다)")]
    public float extraJumpHeight = 1.5f;
    [Tooltip("점프 및 체공하는 공격 시간 (실패 전 딜레이 시간)")]
    public float jumpDuration = 1.0f;

    [Header("Headshot Gore Settings")]
    [Tooltip("머리통 뼈대 (Head Bone)를 여기에 할당하세요.")]
    public Transform headBone;
    [Tooltip("유저가 달아둔 목 파티클 오브젝트 (선택사항)")]
    public GameObject neckBloodParticlePrefab;
    [Tooltip("피 분수 파티클을 몇 초 동안 재생하고 멈출지 결정합니다.")]
    public float neckBloodDuration = 3.0f;

    [Header("Chain Aggro Settings")]
    [Tooltip("소리를 내거나 죽을 때 주변 동료를 분노하게 만드는 반경 (씬 뷰에서 빨간 원으로 표시됨)")]
    public float aggroRadius = 15.0f;

    private bool isChaseRunning = false;
    private bool hasTriggeredAttack = false;
    private Coroutine currentReactionCoroutine; // 연사 덮어쓰기 로직을 위한 보관함
    private AudioSource audioSource; // 비명 소리를 발생시킬 자체 스피커

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>(); // 자동 할당
        
        if(agent != null) agent.speed = walkSpeed;

        // 씬에서 목적지를 이름으로 자동 탐색
        GameObject entranceObj = GameObject.Find("VillageEntrance");
        GameObject invasionObj = GameObject.Find("VillageInvasion");

        if (entranceObj != null) villageEntrance = entranceObj.transform;
        if (invasionObj != null) villageInvasion = invasionObj.transform;

        // 시작 시 VillageEntrance를 향해 가다서다 반복
        if (villageEntrance != null)
        {
            phase1Coroutine = StartCoroutine(IdleWalkRoutine());
        }
    }

    private IEnumerator IdleWalkRoutine()
    {
        // 피격받지 않고, 살아있고, ToEntrance 상태일 때만 반복
        while (currentPhase == MutantPhase.ToEntrance && !isDead)
        {
            if (isReacting) 
            {
                yield return null;
                continue;
            }

            // 1. 목적지를 향해 걷기
            if (agent != null && agent.isOnNavMesh && villageEntrance != null)
            {
                agent.isStopped = false;
                MoveToTarget(villageEntrance.position);
                animator.SetBool(animWalkBool, true);
            }
            yield return new WaitForSeconds(Random.Range(walkDurationMin, walkDurationMax));

            if (currentPhase != MutantPhase.ToEntrance || isDead) break;

            // 2. 잠시 서서 주변 경계 (Idle)
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                animator.SetBool(animWalkBool, false);
            }
            yield return new WaitForSeconds(Random.Range(idleDurationMin, idleDurationMax));
        }
    }

    void Update()
    {
        // 죽었거나, 리액션 중이거나, 요원이 없으면 이동 로직 전체 무시
        if (isDead || isReacting || agent == null) return;

        if (hitCount == 0)
        {
            // --- 1. 2D 평면 거리 기반(Distance) 방어선 도달 판정 (Y축 높이 차이 무시) ---
            if (currentPhase == MutantPhase.ToEntrance && villageEntrance != null)
            {
                // 몬스터와 큐브의 높이(Y축)가 다르더라도 x, z 좌표만 비교하여 거리를 잽니다.
                Vector2 monsterPos2D = new Vector2(transform.position.x, transform.position.z);
                Vector2 entrancePos2D = new Vector2(villageEntrance.position.x, villageEntrance.position.z);

                // 넉넉하게 반경 4.5m 이내에 들어오면 1차 큐브 도착 완료!
                if (Vector2.Distance(monsterPos2D, entrancePos2D) < 3f)
                {
                    currentPhase = MutantPhase.ToInvasion;
                    if (phase1Coroutine != null) 
                    {
                        StopCoroutine(phase1Coroutine);
                        phase1Coroutine = null;
                    }
                    
                    if (villageInvasion == null) Debug.LogError("[MutantAI] 2차 목적지 'VillageInvasion' 큐브를 찾지 못했습니다! 이름을 확인하세요.");
                }
            }
            else if (currentPhase == MutantPhase.ToInvasion && villageInvasion != null)
            {
                Vector2 monsterPos2D = new Vector2(transform.position.x, transform.position.z);
                Vector2 invasionPos2D = new Vector2(villageInvasion.position.x, villageInvasion.position.z);

                // 2차 큐브도 마찬가지로 X, Z 평면 상으로 4.5m 이내에 진입하면 패배 처리!
                if (Vector2.Distance(monsterPos2D, invasionPos2D) < 4.5f)
                {
                    // ★ 마을 침략 성공 — 입력 차단 플래그 먼저 ON
                    KillCountManager.isGameEnding = true;
                    if (KillCountManager.Instance != null) KillCountManager.Instance.ShowMissionFailedPanel();
                    if (agent != null) agent.isStopped = true;
                    animator.SetBool(animWalkBool, false);
                    this.enabled = false; 
                }
            }

            // --- 2. 2차 큐브(VillageInvasion)를 향해 쉬지 않고 걷는 이동 로직 ---
            if (currentPhase == MutantPhase.ToInvasion)
            {
                if (villageInvasion != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    MoveToTarget(villageInvasion.position);
                    animator.SetBool(animWalkBool, true);
                }
            }
        }
        else if (currentPhase == MutantPhase.ChasePlayer || currentPhase == MutantPhase.Crawling)
        {
            if (targetCamera != null && agent.isOnNavMesh && !hasTriggeredAttack)
            {
                // 리액션(두리번거리기, 소리지르기) 도중에는 매 프레임 발생하는 이동 명령을 전면 차단하여 발을 묶습니다.
                if (isReacting) return;

                // Y축(높이)을 무시하고 X, Z 평면 상의 카메라와의 접근 거리를 계산합니다.
                Vector2 monsterPos2D = new Vector2(transform.position.x, transform.position.z);
                Vector2 cameraPos2D = new Vector2(targetCamera.position.x, targetCamera.position.z);
                float distToPlayer = Vector2.Distance(monsterPos2D, cameraPos2D);

                // 1. 공격 거리 진입 (최우선 순위: 덮치거나 발목 깨물기)
                if (distToPlayer <= attackDistanceThreshold)
                {
                    hasTriggeredAttack = true;
                    if (agent != null) 
                    {
                        agent.isStopped = true;
                        agent.enabled = false; // NavMesh 에이전트가 억지로 바닥으로 끌어내리는 현상 절대 방지
                        agent.updateRotation = false; // NavMesh 기본 회전 잠금
                    }

                    // 카메라(플레이어)를 정확히 정면으로 바라보도록 치명적 오차 즉시 보정
                    Vector3 dirToCamera = (targetCamera.position - transform.position).normalized;
                    dirToCamera.y = 0; // 수평 기준 회전 (위상 기울어짐 방지)
                    if (dirToCamera != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(dirToCamera);
                    }

                    // 기어오든 뛰어오든 동일하게 점프 공격 애니메이션 → 딜레이 → 미션 실패 흐름으로 통일
                    StartCoroutine(JumpAttackAndFailRoutine());
                }
                // 1.5. 크롤링 전용 행동 진입 (달리기 모션 교체 금지)
                else if (currentPhase == MutantPhase.Crawling)
                {
                    agent.isStopped = false;
                    MoveToTarget(targetCamera.position);
                }
                // 2. 달리기 거리 진입 또는 이미 2번 이상 맞았을 경우
                else if (distToPlayer <= runDistanceThreshold || hitCount >= 2)
                {
                    agent.isStopped = false;
                    agent.speed = runSpeed;
                    MoveToTarget(targetCamera.position);

                    // 걷고 있었다면 달리기 모션으로 변경
                    if (!isChaseRunning && !isReacting)
                    {
                        isChaseRunning = true;
                        animator.SetBool(animWalkBool, false);
                        animator.SetTrigger(animRunTrigger);
                    }
                }
                // 3. 그 밖의 먼 거리 (천천히 걷기 유지)
                else
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed;
                    MoveToTarget(targetCamera.position);
                    
                    if (!isReacting && !isChaseRunning)
                    {
                        animator.SetBool(animWalkBool, true);
                    }
                }
            }
        }
    }

    // 타겟(플레이어 카메라)이 허공이나 NavMesh 영역 바깥에 있을 경우를 위한 보정 이동 함수
    private void MoveToTarget(Vector3 targetPos)
    {
        NavMeshHit hit;
        // 타겟의 좌표를 기준으로 반경 50m 내에 있는 가장 가까운 길(NavMesh)을 찾아서 그곳으로 가라고 명령합니다.
        if (NavMesh.SamplePosition(targetPos, out hit, 50f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            // 실패 시 그냥 원본 좌표로 강제 시도
            agent.SetDestination(targetPos);
        }
    }

    public void TakeDamage(int damage, Vector3 hitPoint, bool isHeadshot = false)
    {
        if (isTrueDead) return;

        // 크롤링 중이었다면(이미 부활해서 기어오는 좀비) 데미지에 상관없이 딱 1방에 진짜 즉사!
        if (currentPhase == MutantPhase.Crawling)
        {
            Debug.Log("🎯 [MutantAI] 기어오는 돌연변이에게 명중! 즉시 완전 사망 처리 실행!");
            Die(isHeadshot, true); 
            return;
        }

        // 가짜 죽음 상태(바닥에 쓰러져 일어날 준비 중)일 때는 무적 피격 판정 무시
        if (isDead) return;

        // 피격 시 타겟(메인 카메라)이 등록되어 있지 않다면 딱 한 번 탐색합니다.
        if (targetCamera == null)
        {
            if (Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }
            else
            {
                GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null) targetCamera = camObj.transform;
            }
        }

        hitCount += damage;

        // 피격 시 무조건 플레이어 추적으로 노선 변경
        if (currentPhase != MutantPhase.ChasePlayer)
        {
            currentPhase = MutantPhase.ChasePlayer;
            if (phase1Coroutine != null) 
            {
                StopCoroutine(phase1Coroutine);
                phase1Coroutine = null;
            }
        }

        if (hitCount == 1 || hitCount == 2)
        {
            // 만약 1번 맞고 두리번거리고 있던 도중에 또 맞았다면(연사), 두리번 모션을 강제 취소시킵니다.
            if (currentReactionCoroutine != null)
            {
                StopCoroutine(currentReactionCoroutine);
            }
            currentReactionCoroutine = StartCoroutine(ReactRoutine(hitCount));
        }
        else if (hitCount >= 3)
        {
            Die(isHeadshot, false); // 3대를 맞아 처음 쓰러질 때는 진짜 죽음(True Death) 플래그를 넘기지 않습니다. Die 내부에서 enableRevive 여부에 따라 심판.
        }
    }

    private IEnumerator ReactRoutine(int currentHitCount)
    {
        isReacting = true;
        
        // 기존에 걷거나 뛰고 있던 모션을 완전히 강제 종료시켜 기절 상태(LookAround)를 방해하지 않게 만듭니다.
        animator.SetBool(animWalkBool, false);

        if(agent != null) 
        {
            agent.isStopped = true;
            agent.updateRotation = false; // 제자리 리액션 도중 몸이 꼬이지 않도록 엔진 회전 잠금
        }

        // 가장 먼저, 카메라(플레이어)를 쳐다보고 동작을 수행합니다. (요청사항 반영)
        if (targetCamera != null)
        {
            Vector3 direction = (targetCamera.position - transform.position).normalized;
            direction.y = 0; // 공중으로 몸이 꺾이지 않도록 방지
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // 타격 횟수에 따른 철저한 분기
        if (currentHitCount == 1)
        {
            // 1대 맞음: 두리번 거리는 애니메이션 재생 및 설정된 시간만큼 대기
            animator.SetTrigger(animLookAroundTrigger);
            yield return new WaitForSeconds(lookAroundDuration);
        }
        else if (currentHitCount == 2)
        {
            // 2대 맞음: 포효(Scream) 옵션이 켜져있다면, 소리지르고 대기 / 꺼져있다면 즉각 통과
            if (useScreamOnSecondHit)
            {
                animator.SetTrigger(animScreamTrigger);
                
                // 디버깅: 왜 소리가 안 나는지 추적하는 탐지기 코드
                if (audioSource == null) 
                {
                    Debug.LogWarning("🚨 [MutantAI] AudioSource 컴포넌트를 찾지 못했습니다! (프리팹 원본에 안 달려있을 확률 99%)");
                }
                else if (screamClip == null)
                {
                    Debug.LogWarning("🚨 [MutantAI] Scream Clip 사운드 파일 칸이 비어 있습니다! (인스펙터 확인 요망)");
                }
                else
                {
                    Debug.Log("✅ [MutantAI] 스크립트에서 정상적으로 오디오 재생 명령(PlayOneShot)을 실행했습니다!");
                    audioSource.PlayOneShot(screamClip);
                }
                
                // 2대 맞아서 비명을 지를 때 주변 돌연변이들에게 어그로 신호를 뿌립니다!
                BroadcastAggro();

                yield return new WaitForSeconds(screamDuration);
            }
        }

        // 지연 시간이 끝났을 때(죽지 않았다면) 다음 액션을 수행
        if (!isDead)
        {
            if(agent != null) 
            {
                agent.isStopped = false;
                agent.updateRotation = true; // 이동 시 자동으로 쳐다보는 기능 복구
            }
            
            if (currentHitCount == 1)
            {
                // 두리번거림이 끝나고 걷기 돌입
                animator.SetBool(animWalkBool, true);
            }
            else if (currentHitCount == 2)
            {
                // 포효가 끝난 뒤 미친 듯이 돌진
                isChaseRunning = true;
                animator.SetBool(animWalkBool, false); // 기존에 걷던 모션 해제
                animator.SetTrigger(animRunTrigger);
            }
        }

        isReacting = false;
        currentReactionCoroutine = null; // 완전히 끝났으므로 비워줌
    }

    private IEnumerator JumpAttackAndFailRoutine()
    {
        // ★ 1. 점프 시작 즉시: 입력 차단 + 애니메이터를 UnscaledTime으로 전환
        //    → Time.timeScale = 0이더라도 점프 애니메이션과 이동이 정상 재생됩니다.
        KillCountManager.isGameEnding = true;
        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 2. 공격 애니메이션 실행
        animator.SetTrigger(animJumpAttackTrigger);

        float timer = 0f;
        Vector3 startPos = transform.position;
        
        // 타겟(카메라)이 없으면 제자리 점프로 폴백
        Vector3 targetPos = (targetCamera != null) ? targetCamera.position : startPos;
        
        // 무중력 비행을 막기 위해 타겟의 높이(Y)를 뜻어오는 바닥(startY)과 또같게 맞춥니다. 
        targetPos.y = startPos.y; 

        // 점프의 '정점(최고 높이)'에서 게임을 끊내기 위해 반복문 절반 실행
        float peakTime = jumpDuration * 0.5f;

        // ★ 나머지 모든 NavMeshAgent(= 다른 뒤턴트)를 우선 정지
        //    (timeScale=0로도 자동 정지되지만, 추가 보험 차원에서 명시적으로 정지)
        MutantAI[] allMutants = FindObjectsByType<MutantAI>(FindObjectsSortMode.None);
        foreach (MutantAI m in allMutants)
        {
            if (m == this) continue;
            if (m.agent != null && m.agent.enabled) m.agent.isStopped = true;
        }

        while (timer < peakTime)
        {
            timer += Time.unscaledDeltaTime; // ★ timeScale=0에서도 제대로 진행
            float progress = timer / jumpDuration;

            Vector3 currentFlatPos = Vector3.Lerp(startPos, targetPos, progress * 1.6f);
            
            float addedHeight = 0f;
            if (extraJumpHeight > 0f)
            {
                addedHeight = Mathf.Sin(progress * Mathf.PI) * extraJumpHeight;
            }
            
            transform.position = new Vector3(currentFlatPos.x, startPos.y + addedHeight, currentFlatPos.z);
            yield return null;
        }

        // 3. 게임 오버(임무 실패) 패널 호출
        if (KillCountManager.Instance != null)
        {
            KillCountManager.Instance.ShowMissionFailedPanel();
        }

        // ★ 패널 표시로 timeScale=0이 된 순간, 애니메이터를 Normal 모드로 복구
        //    → timeScale=0이 이 뮤턴트 애니메이션도 함께 프리즈 (원래처럼 점프 정점에서 딱 멈춤)
        if (animator != null)
            animator.updateMode = AnimatorUpdateMode.Normal;
        
        this.enabled = false;

    }

    private void Die(bool explodeHead = false, bool isFinalDeath = false)
    {
        bool wasCrawling = (currentPhase == MutantPhase.Crawling);

        // 죽을 때 나는 엄청난 비명 혹은 터지는 소리로 인해 주변 반경에 어그로 신호를 뿌립니다!
        BroadcastAggro();

        // 이미 부활해서 기어오고 있었다면 이것은 무조건 참혹한 진짜 최후의 죽음!
        if (wasCrawling) isFinalDeath = true;
        
        // 인스펙터에서 부활 옵션이 진작 꺼져있었다면 당연히 첫 죽음부터가 진짜 죽음!
        if (!enableRevive) isFinalDeath = true;

        if (isFinalDeath)
        {
            isTrueDead = true; // 영구 사망 플래그 박제
            isDead = true;
        }
        else
        {
            isDead = true; // 가짜 죽음 (움직임 일시 정지를 위한 뇌사 상태)
        }
        
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // --- 헤드샷 폭파 연출 (최초 사망 시에만 터짐. 기어가다가 얻어맞은 놈은 무시) ---
        if (explodeHead && headBone != null && !wasCrawling)
        {
            headBone.localScale = Vector3.zero; // 머리 크기를 0으로 축소하여 감춤

            if (neckBloodParticlePrefab != null)
            {
                neckBloodParticlePrefab.SetActive(true);
                ParticleSystem[] pSystems = neckBloodParticlePrefab.GetComponentsInChildren<ParticleSystem>();
                foreach(ParticleSystem ps in pSystems) { ps.Play(); }
                if (neckBloodDuration > 0) StartCoroutine(StopBloodParticleRoutine(pSystems, neckBloodDuration));
            }
        }

        // --- 사운드 처리 (처음 쓰러질 때만 소리냄) ---
        if (audioSource != null && !wasCrawling)
        {
            audioSource.Stop();
            if (deathClip != null) audioSource.PlayOneShot(deathClip);
        }

        // --- 애니메이션 처리 (요청하신 "특정 시점부터 재생하기" 꼼수 로직) ---
        if (isFinalDeath && wasCrawling)
        {
            // [원인 해결!] 기존에는 'dying'이라는 "Trigger(조건) 파라미터 이름"으로 상태를 찾으려 했기 때문에
            // 애니메이터가 해당 이름의 네모 박스(State)를 찾지 못하고 무시해버렸을 확률이 99% 입니다.
            // 인스펙터에 새로 추가한 State 이름을 통해 명확하게 해당 상자를 즉시 실행시킵니다.
            animator.Play(animDeathStateName, 0, crawlDeathStartTime); 
        }
        else
        {
            // 첫 가짜 사망이거나, 옵션이 꺼져 있을 때의 평범하게 일어서 있다 쓰러지기
            animator.SetTrigger(animDeathTrigger);
        }

        // --- 킬 수 오르기 처리 (영구 사망인 경우에만!) ---
        if (isFinalDeath)
        {
            if (KillCountManager.Instance != null) KillCountManager.Instance.AddKill();
            
            // 모든 콜라이더 완전 삭제 처리 (영원히 상호작용 불가능, 시체로서 밟지 못하게)
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders) { col.enabled = false; }
            
            this.enabled = false; // 스크립트 전원 자체 차단! 끝!
        }
        else
        {
            // --- [중요] 가짜 사망인 경우에는 킬 수도 안 오르고 바닥에 쓰러진 후 일정 시간 뒤 부활 루틴! ---
            StartCoroutine(ReviveRoutine(explodeHead));
        }
    }

    private IEnumerator ReviveRoutine(bool isHeadshot)
    {
        // 1. 유저가 플레이어가 안심하길 기다리며 쓰러진 채 대기 (낚시)
        yield return new WaitForSeconds(reviveDelay);
        
        // 2. 가짜 시체 상태 해제 및 다시 기어오기 상태 돌입!
        isDead = false;
        hasAlreadyRevived = true;
        currentPhase = MutantPhase.Crawling;

        // NavMesh를 다시 켜되, 바닥을 기어갈 거니까 속도를 조절
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            
            // 머리가 박살나면 느린 기어가기 로직 (보너스)
            if (isHeadshot)
            {
                agent.speed = slowCrawlSpeed;
                animator.SetTrigger(animSlowCrawlTrigger);
            }
            // 머리가 멀쩡하게 바디샷으로 죽었으면 빠른 기어가기 로직 (패널티)
            else
            {
                agent.speed = fastCrawlSpeed;
                animator.SetTrigger(animFastCrawlTrigger);
            }
            
            // 즉시 카메라 쪽으로 몸 확 비틀기
            if (targetCamera != null)
            {
                MoveToTarget(targetCamera.position);
            }
        }
    }

    public void FleeFrom(Vector3 position)
    {
        // 닭이나 여우와 달리 몬스터는 총기 소음에 패닉 도주하지 않음
    }

    public void StopAnimation()
    {
        if(agent != null) agent.isStopped = true;
        animator.speed = 0; 
    }

    private IEnumerator StopBloodParticleRoutine(ParticleSystem[] pSystems, float duration)
    {
        yield return new WaitForSeconds(duration);
        
        foreach(ParticleSystem ps in pSystems)
        {
            if (ps != null)
            {
                // 오브젝트 전체를 끄지 않고(SetActive(false X), 파티클 '방출'만 멈춰서 이미 뿌려진 피는 공기 중으로 자연스럽게 떨어져 사라지게 둡니다.
                ps.Stop();
            }
        }
    }

    // --- 애니메이션 타임라인 이벤트(Event) 전용 호출 함수 ---
    // (이 함수들은 유니티의 Animator가 특정 프레임을 밟을 때 자동으로 찔러줍니다!)
    public void PlayWalkFootstep()
    {
        if (audioSource != null && walkStepClip != null && !isDead)
        {
            // 발을 밟는 강도가 매번 다른 것처럼 세기(Volume)를 무작위 조절합니다.
            float randomVol = UnityEngine.Random.Range(0.6f, 1.0f);
            audioSource.PlayOneShot(walkStepClip, randomVol);
        }
    }

    public void PlayRunFootstep()
    {
        if (audioSource != null && runStepClip != null && !isDead)
        {
            // 뛰는 발소리는 걷는 발소리보다 기본적으로 더 강하고 묵직하게 소리납니다.
            float randomVol = UnityEngine.Random.Range(0.85f, 1.0f);
            audioSource.PlayOneShot(runStepClip, randomVol);
        }
    }

    // ==========================================
    // --- 연쇄 어그로 (Chain Aggro) 시스템 로직 ---
    // ==========================================

    private void BroadcastAggro()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, aggroRadius);
        foreach (Collider col in colliders)
        {
            // 내 자식/부모에 달린 콜라이더여도 MutantAI를 정확히 찾습니다.
            MutantAI siblingMutant = col.GetComponentInParent<MutantAI>();
            
            // 나 자신이 아니고, 살아있고, 돌연변이 AI 컴포넌트를 가진 타겟에게만 알람 전송
            if (siblingMutant != null && siblingMutant != this)
            {
                siblingMutant.OnHearAggro();
            }
        }
    }

    public void OnHearAggro()
    {
        // 옵션 B 적용: 죽었거나, 죽은 척(가짜 죽음 대기) 중이거나, 이미 쫓고 있는 등 한가하지 않은 상태면 무지성 100% 무시합니다.
        if (isDead || isReacting || currentPhase == MutantPhase.ChasePlayer || currentPhase == MutantPhase.Crawling) return;

        StartCoroutine(AggroReactionRoutine());
    }

    private IEnumerator AggroReactionRoutine()
    {
        isReacting = true; // 이동 불가 잠금
        animator.SetBool(animWalkBool, false);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.updateRotation = false; // 기본 자율 회전 끄기
        }

        // 1. 유저 요청: 1초 동안 어리둥절하게 그 자리에서 소름돋는 정적(얼음)
        yield return new WaitForSeconds(1.0f);

        // 2. 시간이 지나면 플레이어(카메라)를 홱 쳐다봄
        if (targetCamera == null)
        {
            if (Camera.main != null) targetCamera = Camera.main.transform;
            else 
            {
                GameObject cam = GameObject.FindGameObjectWithTag("MainCamera");
                if (cam != null) targetCamera = cam.transform;
            }
        }

        if (targetCamera != null)
        {
            Vector3 direction = (targetCamera.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
        }

        // 3. 동료를 죽인 원수를 향해 나도 분노의 포효!
        animator.SetTrigger(animScreamTrigger);
        if (audioSource != null && screamClip != null)
        {
            audioSource.PlayOneShot(screamClip);
        }

        // 포효 사운드 클립/애니메이션 길이만큼 대기
        yield return new WaitForSeconds(screamDuration);

        // 4. 모션이 끝나고 살아있다면 즉시 미친 듯이 돌진!
        if (!isDead)
        {
            currentPhase = MutantPhase.ChasePlayer; // 페이즈 변경
            
            // 만약 걷고 있던 멍청한 (ToEntrance) 상태 코루틴 타이머가 남아있다면 박살 냄
            if (phase1Coroutine != null) 
            { 
                StopCoroutine(phase1Coroutine); 
                phase1Coroutine = null; 
            }

            if(agent != null) 
            {
                agent.isStopped = false;
                agent.updateRotation = true;
            }
            
            isChaseRunning = true;
            animator.SetBool(animWalkBool, false); // 산책 끄기
            animator.SetTrigger(animRunTrigger);   // 달리기 켜기
        }

        isReacting = false;
    }

    // 유니티 씬(Scene) 화면에서만 렌더링되는 시각화 도구 (레벨 디자인 용도)
    private void OnDrawGizmosSelected()
    {
        // 선택한 뮤턴트의 바닥(중심)을 기준으로 빨간 투명 와이어 반경을 그립니다.
        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.5f); // 강렬한 반투명 빨간색
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
    }
}
