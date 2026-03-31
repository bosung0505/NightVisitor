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
    
    public enum MutantPhase { ToEntrance, ToInvasion, ChasePlayer }
    private MutantPhase currentPhase = MutantPhase.ToEntrance;

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

    [Header("Animation Settings (Triggers/Bools)")]
    public string animWalkBool = "IsWalking";
    public string animRunTrigger = "run";
    public string animReactTrigger = "React_Attack";
    public string animJumpAttackTrigger = "jump_attack";
    public string animDeathTrigger = "dying";

    [Header("Jump Attack Override")]
    [Tooltip("점프 시 코드 기반으로 뻥튀기할 Y축 높이 한계점 (0이면 애니메이션 기본 점프만 사용합니다)")]
    public float extraJumpHeight = 1.5f;
    [Tooltip("점프 및 체공하는 공격 시간 (실패 전 딜레이 시간)")]
    public float jumpDuration = 1.0f;

    private bool isChaseRunning = false;
    private bool hasTriggeredAttack = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        
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
        else if (currentPhase == MutantPhase.ChasePlayer)
        {
            if (targetCamera != null && agent.isOnNavMesh && !hasTriggeredAttack)
            {
                // Y축(높이)을 무시하고 X, Z 평면 상의 카메라와의 접근 거리를 계산합니다.
                Vector2 monsterPos2D = new Vector2(transform.position.x, transform.position.z);
                Vector2 cameraPos2D = new Vector2(targetCamera.position.x, targetCamera.position.z);
                float distToPlayer = Vector2.Distance(monsterPos2D, cameraPos2D);

                // 1. 공격 거리 진입 (최우선 순위: 점프 뛰고 게임 종료)
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

                    StartCoroutine(JumpAttackAndFailRoutine());
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

    public void TakeDamage(int damage, Vector3 hitPoint)
    {
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

        hitCount++;

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
            StartCoroutine(ReactRoutine(hitCount));
        }
        else if (hitCount >= 3)
        {
            Die();
        }
    }

    private IEnumerator ReactRoutine(int currentHitCount)
    {
        isReacting = true;
        
        if(agent != null) 
        {
            agent.isStopped = true;
            agent.updateRotation = false; // 리액션 도중 임의로 회전하지 않도록 방지
        }

        // 피격 리액션 한 번 재생 (Any State -> React_attack)
        animator.SetTrigger(animReactTrigger);
        
        // 리액션 애니메이션이 끝날 때까지 대기 
        // (React_attack 클립 길이에 맞춰주세요. 여기서는 임시로 1.25초 대기)
        yield return new WaitForSeconds(1.25f); 

        if (!isDead)
        {
            // 애니메이션이 끝난 직후 메인 카메라 방향으로 회전
            if (targetCamera != null)
            {
                Vector3 direction = (targetCamera.position - transform.position).normalized;
                direction.y = 0; // x, z 평면(수평)만 고려하여 위/아래로 기울지 않게 함
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            if(agent != null) 
            {
                agent.isStopped = false;
                agent.updateRotation = true; // 이동 시 자동으로 쳐다보는 기능 복구
            }
            
            if (currentHitCount == 1)
            {
                // 천천히 걸어가기 위한 Bool 세팅
                animator.SetBool(animWalkBool, true);
            }
            else if (currentHitCount == 2)
            {
                // 달리기 애니메이션 재생 (Any State -> run)
                animator.SetTrigger(animRunTrigger);
            }
        }

        isReacting = false;
    }

    private IEnumerator JumpAttackAndFailRoutine()
    {
        // 1. 공격 애니메이션 실행
        animator.SetTrigger(animJumpAttackTrigger);

        float timer = 0f;
        Vector3 startPos = transform.position;
        
        // 타겟(카메라)이 없으면 제자리 점프로 폴백
        Vector3 targetPos = (targetCamera != null) ? targetCamera.position : startPos;
        
        // 무중력 비행을 막기 위해 타겟의 높이(Y)를 뮤턴트가 뛰기 시작한 바닥(startY)과 똑같게 맞춰버립니다. 
        targetPos.y = startPos.y; 

        // 점프의 '정점(최고 높이)'에서 게임을 끝내기 위해 반복문 절반 실행
        float peakTime = jumpDuration * 0.5f;

        while (timer < peakTime)
        {
            timer += Time.deltaTime;
            float progress = timer / jumpDuration; // 0.0 에서 0.5 까지만 증가

            // Y축 비행 없이 X, Z축(평면 평행 이동)으로만 카메라 방향을 향해 80% 돌진합니다.
            Vector3 currentFlatPos = Vector3.Lerp(startPos, targetPos, progress * 1.6f);
            
            // 유저가 인스펙터에서 입력한 고유의 점프 높이만을 적용합니다. (0이면 올라가지 않음)
            float addedHeight = 0f;
            if (extraJumpHeight > 0f)
            {
                addedHeight = Mathf.Sin(progress * Mathf.PI) * extraJumpHeight;
            }
            
            // XZ 평면 이동 결과에 내가 설정한 점프 높이만 얹어서 좌표 적용
            transform.position = new Vector3(currentFlatPos.x, startPos.y + addedHeight, currentFlatPos.z);
            yield return null;
        }

        // 루프가 끝나는 순간 = 기본/추가 점프 포물선의 정점에서 화면에 가장 가깝게 다가온 찰나!

        // 3. 게임 오버(임무 실패) 패널 호출
        if (KillCountManager.Instance != null)
        {
            KillCountManager.Instance.ShowMissionFailedPanel();
        }
        
        this.enabled = false;
    }

    private void Die()
    {
        isDead = true;
        
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // 3번째 피격 시 사망 애니메이션 (Any State -> death)
        animator.SetTrigger(animDeathTrigger);

        // 여우처럼 상호작용 막기 (콜라이더 비활성화)
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        
        // 사망 킬 카운트 누적
        if (KillCountManager.Instance != null)
        {
            KillCountManager.Instance.AddKill();
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
}
