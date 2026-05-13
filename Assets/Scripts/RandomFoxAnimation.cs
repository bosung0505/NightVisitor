using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class RandomFoxAnimation : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    
    [Header("Hunting Targets")]
    [Tooltip("The central point of the Hunting Zone. Auto-finds 'HuntingZone' if left empty.")]
    public Transform huntingZoneCenter;
    [Tooltip("The chicken model in the fox's mouth. Disable this initially.")]
    public GameObject catchChickenObj;
    
    [Header("Speeds")]
    public float walkSpeed = 1.5f;
    public float sneakSpeed = 0.8f;
    public float runSpeed = 4f;
    public float fleeSpeedMultiplier = 2.0f;
    public float escapeSpeedMultiplier = 1.5f;
    [Tooltip("여우가 도망칠 때(Escape) 방향을 트는 회전 속도 (낮을수록 천천히, 부드럽게 돕니다)")]
    public float escapeRotationSpeed = 3.0f;

    [Tooltip("스테이지 설정에 의해 Sneak 구간을 건너뛰고 바로 달리는지 여부")]
    [HideInInspector] public bool ignoreSneakZone = false;

    [Header("Health & Damage")]
    [Tooltip("여우의 최대 체력 (기본 2: 꼬리나 몸통 등 Normal 부위는 2방, 머리나 심장 등 Critical 부위는 데미지가 2배라 1방에 즉사)")]
    public int maxHealth = 2;
    private int currentHealth;

    [Header("Timers")]
    public float jumpDuration = 1.5f;
    [Tooltip("How long the fox pauses before moving forward during the jump (anticipation)")]
    public float jumpAnticipationTime = 0.3f;
    [Tooltip("How long the fox slides forward during the jump")]
    public float jumpMoveTime = 0.8f;
    [Tooltip("How long the fox must wait before jumping again")]
    public float jumpCooldown = 2.0f;
    private float lastJumpTime = -10f; // Initialize so it can jump immediately

    public float fleeDuration = 3f;
    public float catchWaitDuration = 0.5f;
    [Tooltip("Distance at which the fox catches the chicken")]
    public float catchDistance = 1.5f;
    [Tooltip("Distance from the center the fox must reach to safely disappear after catching")]
    public float escapeDisappearDistance = 40f;

    public enum FoxState { Walk, Sneak, Run, Jump, Flee, Dead, Catching, Escape }
    public FoxState currentState = FoxState.Walk;

    private Transform targetChicken;
    private Vector3 entryDirection;
    private Vector3 escapeDestination;
    private bool isJumpMoving = false;

    // [최적화] Walk/Sneak 목적지 갱신 타이머 (0.2초 간격)
    private float destUpdateTimer = 0f;
    private const float DEST_UPDATE_INTERVAL = 0.2f;

    // [최적화] FindClosestChicken 추력 타이머 (0.5초 간격으로 제한)
    private float _findChickenTimer = 0f;
    private const float FIND_CHICKEN_INTERVAL = 0.5f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            Debug.LogError("RandomFoxAnimation requires an Animator component!");
            
        // 체력 초기화
        currentHealth = maxHealth;
    }

    void Start()
    {
        // Disable rotation update by agent if we want manual rotation, but letting agent handle it is usually better for NavMesh.
        // We'll let the NavMeshAgent handle both position and rotation for smooth obstacle avoidance.
        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updatePosition = true;
        }

        // If hunting zone center is not assigned, try to find it by name
        if (huntingZoneCenter == null)
        {
            FindFallbackTarget();
        }
        
        if (catchChickenObj != null) catchChickenObj.SetActive(false);

        // Start default behavior (Walk towards center)
        SetState(FoxState.Walk);
    }

    /// <summary>
    /// 오브젝트 풀링을 위해 이전 상태를 초기화하는 함수입니다.
    /// 스포너에서 꺼낼 때 호출합니다.
    /// </summary>
    public void ResetState()
    {
        currentHealth = maxHealth;
        isJumpMoving = false;
        destUpdateTimer = 0f;
        
        if (catchChickenObj != null) catchChickenObj.SetActive(false);
        
        // 풀링(재활용) 시 무적/먹통 버그 방지를 위해 모든 콜라이더 복구
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders) col.enabled = true;
        
        this.enabled = true;
        
        if (agent != null)
        {
            // ★ [버그 핵심] 강제로 켜기 전에 현재 좌표에서 가장 가까운 내비메시 위로 스냅(Snap) 시킵니다.
            // 이렇게 해야만 허공이나 바닥 아래 등 내비메시가 없는 곳에서 켜지면서 isOnNavMesh가 false가 되어 영원히 멈추는 현상을 방지합니다.
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }

            agent.enabled = true;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.updatePosition = true;
            agent.ResetPath();
        }

        // 초기화 시점에 타겟이 없다면 여기서 바로 찾아줍니다. (Start가 늦게 도는 것 방지)
        if (huntingZoneCenter == null)
        {
            FindFallbackTarget();
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        SetState(FoxState.Walk);
    }

    void Update()
    {
        if (currentState == FoxState.Dead || currentState == FoxState.Catching) 
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            return;
        }

        HandleMovement();
    }

    [Header("Jump Detection (Raycast)")]
    public float jumpTriggerDistance = 2.5f;
    public LayerMask obstacleLayerMask;
    public float raycastHeightOffset = 0.5f;

    private void HandleMovement()
    {
        // Jump state ignores NavMesh momentarily
        if (currentState == FoxState.Jump)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
            // Only move forward during the actual moving phase of the jump
            if (isJumpMoving)
            {
                transform.Translate(Vector3.forward * runSpeed * Time.deltaTime, Space.Self);
            }
            return;
        }

        // Make sure agent is active and running
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) 
        {
            Debug.LogWarning($"[Fox Movement] 에이전트 비정상 상태! isActiveAndEnabled={agent.isActiveAndEnabled}, isOnNavMesh={agent.isOnNavMesh}");
            return;
        }
        
        agent.isStopped = false;

        // Perform dynamic Jump detection (SphereCast to avoid missing gaps)
        if (currentState == FoxState.Run || currentState == FoxState.Escape)
        {
            CheckForFenceJump();
        }

        // Determine speed and target based on state
        switch (currentState)
        {
            case FoxState.Walk:
                agent.speed = walkSpeed;
                destUpdateTimer += Time.deltaTime;
                if (destUpdateTimer >= DEST_UPDATE_INTERVAL)
                {
                    destUpdateTimer = 0f;
                    agent.SetDestination(GetDestinationDefault());
                }
                break;
                
            case FoxState.Sneak:
                agent.speed = sneakSpeed;
                destUpdateTimer += Time.deltaTime;
                if (destUpdateTimer >= DEST_UPDATE_INTERVAL)
                {
                    destUpdateTimer = 0f;
                    agent.SetDestination(GetDestinationDefault());
                }
                break;
                
            case FoxState.Run:
                agent.speed = runSpeed;
                entryDirection = transform.forward;

                if (targetChicken != null)
                {
                    agent.SetDestination(targetChicken.position);
                    float dist = Vector3.Distance(transform.position, targetChicken.position);
                    if (dist <= catchDistance)
                    {
                        StartCoroutine(CatchChickenRoutine());
                        return;
                    }
                }
                else
                {
                    // [최적화] 0.5초 간격으로 FindClosestChicken 호출 제한
                    _findChickenTimer += Time.deltaTime;
                    if (_findChickenTimer >= FIND_CHICKEN_INTERVAL)
                    {
                        _findChickenTimer = 0f;
                        FindClosestChicken();
                    }
                    if (targetChicken == null) agent.SetDestination(GetDestinationDefault());
                }
                break;
                
            case FoxState.Flee:
                agent.speed = runSpeed * fleeSpeedMultiplier;
                // Fleeing destination is set once in FleeRoutine
                break;
                
            case FoxState.Escape:
            // 탈출 목적지는 유효한 내비메시 위로 설정되어 있음
            agent.speed = runSpeed * escapeSpeedMultiplier;
            agent.SetDestination(escapeDestination);

            // [추가] 도망칠 때의 부드러운 회전 속도 조절
            // agent.desiredVelocity는 내비메시가 가리키는 현재 '나아가야 할 방향'입니다.
            if (agent.desiredVelocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * escapeRotationSpeed);
            }

            // 현재 목적지에 거의 다다랐다면(갈 수 있는 내비메시 끝에 도달했다면)
            if (!agent.pathPending && agent.remainingDistance <= 1.0f)
            {
                Vector3 awayFromCenterDir = transform.forward;
                if (huntingZoneCenter != null)
                {
                    awayFromCenterDir = (transform.position - huntingZoneCenter.position).normalized;
                    awayFromCenterDir.y = 0;
                }
                escapeDestination = GetSlidingEscapeDestination(transform.position, awayFromCenterDir.normalized, 20f);
                agent.SetDestination(escapeDestination);
            }
            break;
        }
    }

    private void CheckForFenceJump()
    {
        Vector3 rayOrigin = transform.position + (Vector3.up * raycastHeightOffset);
        float sphereRadius = 0.3f;

        Debug.DrawRay(rayOrigin, transform.forward * jumpTriggerDistance, Color.red);

        int mask = obstacleLayerMask.value == 0 ? ~0 : obstacleLayerMask.value;

        RaycastHit hit;
        if (Physics.SphereCast(rayOrigin, sphereRadius, transform.forward, out hit, jumpTriggerDistance, mask))
        {
            // [Opt] CompareTag only (zero GC)
            if (hit.collider.CompareTag("Fence"))
            {
                Vector3 desiredDir = agent.desiredVelocity.normalized;
                float angleToDestination = Vector3.Angle(transform.forward, desiredDir);

                // Skip if agent velocity and gaze are misaligned (e.g. turning)
                if (agent.velocity.magnitude > 0.1f && angleToDestination > 30f)
                {
                    Debug.Log($"Ignored Fence Jump. Angle too steep: {angleToDestination}");
                    return;
                }

                // Jump cooldown check
                if (Time.time - lastJumpTime < jumpCooldown)
                {
                    Debug.Log($"Ignored Fence Jump. On Cooldown. ({Time.time - lastJumpTime:F1}s / {jumpCooldown}s)");
                    return;
                }

                Debug.Log("Fence Detected and Path Aligned! Jumping. Hit: " + hit.collider.gameObject.name);
                lastJumpTime = Time.time;

                if (currentState == FoxState.Run)
                {
                    StartCoroutine(JumpRoutine());
                }
                else if (currentState == FoxState.Escape)
                {
                    StartCoroutine(JumpEscapeRoutine());
                }
            }
        }
    }

    private IEnumerator CatchChickenRoutine()
    {
        SetState(FoxState.Catching);
        if (agent.isActiveAndEnabled) agent.isStopped = true;

        // Stop moving and wait
        yield return new WaitForSeconds(catchWaitDuration);

        // Destroy the target chicken and enable the caught chicken model
        if (targetChicken != null)
        {
            if (KillCountManager.Instance != null)
            {
                KillCountManager.Instance.AddDeadChicken();
            }
            Destroy(targetChicken.gameObject);
        }
        if (catchChickenObj != null)
        {
            catchChickenObj.SetActive(true);
        }

        // Calculate escape direction (무조건 닭장 중앙에서 바깥쪽으로 향하는 벡터)
        Vector3 escapeDir = transform.forward; // fallback
        if (huntingZoneCenter != null)
        {
            escapeDir = (transform.position - huntingZoneCenter.position).normalized;
        }
        else
        {
            escapeDir = -entryDirection; // fallback if no center
        }

        // 약간의 랜덤성을 더해 예측 불가능하게 만듦
        float randomAngle = Random.Range(-30f, 30f);
        escapeDir = Quaternion.Euler(0, randomAngle, 0) * escapeDir;
        escapeDir.y = 0;
        
        // 내비메시 위에서 도달 가능한 유효한 도망 목적지 찾기 (벽 타기 로직 적용)
        escapeDestination = GetSlidingEscapeDestination(transform.position, escapeDir.normalized, 20f);

        SetState(FoxState.Escape);
        
        // Start waiting to disappear once we are safely away
        StartCoroutine(EscapeAndDisappearRoutine());
    }

    private IEnumerator EscapeAndDisappearRoutine()
    {
        // 렌더러 참조 (시야 밖 체크 용도)
        SkinnedMeshRenderer renderer = GetComponentInChildren<SkinnedMeshRenderer>();

        while (true)
        {
            bool isFarEnough = false;
            bool isOutOfSight = false;

            // 1. 거리 체크 (40 이상)
            if (huntingZoneCenter != null)
            {
                float distFromCenter = Vector3.Distance(transform.position, huntingZoneCenter.position);
                if (distFromCenter >= escapeDisappearDistance)
                {
                    isFarEnough = true;
                }
            }

            // 2. 시야 체크 (화면 밖인가?)
            if (renderer != null && !renderer.isVisible)
            {
                isOutOfSight = true;
            }
            // 렌더러가 없으면 그냥 안 보인다고 가정
            else if (renderer == null)
            {
                isOutOfSight = true; 
            }

            // 둘 중 하나라도 만족하면(OR 조건) 삭제
            if (isFarEnough || isOutOfSight)
            {
                break;
            }

            yield return new WaitForSeconds(0.5f); // 0.5초마다 검사
        }
        
        // 시야 밖으로 사라졌으므로 파괴 대신 풀에 반납
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private Vector3 GetDestinationDefault()
    {
        // Move towards the hunting zone center
        if (huntingZoneCenter != null) 
        {
            return huntingZoneCenter.position;
        }
        
        // If still no center, try to find one dynamically
        FindFallbackTarget();
        if (huntingZoneCenter != null) 
        {
            return huntingZoneCenter.position;
        }

        return transform.position; // Stay still if absolutely no target
    }

    private void FindFallbackTarget()
    {
        if (huntingZoneCenter != null) return;

        // 1. 유저가 지칭했을 수 있는 다양한 이름의 빈 오브젝트 검색
        GameObject zone = GameObject.Find("HuntingZone");
        if (zone == null) zone = GameObject.Find("Zone");
        if (zone == null) zone = GameObject.Find("RunningZone");
        if (zone == null) zone = GameObject.Find("SneakZone");

        if (zone != null)
        {
            huntingZoneCenter = zone.transform;
            return;
        }

        // 2. 오브젝트 이름으로 찾기 실패 시, 맵에 존재하는 닭 중 아무나 한 마리 찾아서 그쪽으로 향함
        // [Opt] Static registry replaces FindObjectOfType
        RandomChickenAnimation anyChicken = RandomChickenAnimation.All.Count > 0
            ? RandomChickenAnimation.All[0] : null;
        if (anyChicken != null)
        {
            // 닭이 배회하는 구역의 중심(boundaryCenter)이 있다면 그걸 타겟으로 삼음
            if (anyChicken.boundaryCenter != null)
            {
                huntingZoneCenter = anyChicken.boundaryCenter;
            }
            else
            {
                // 중심이 설정 안되어 있다면 그냥 그 닭의 위치를 목표로 설정 (매번 갱신됨)
                huntingZoneCenter = anyChicken.transform;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == FoxState.Dead || currentState == FoxState.Flee) return;

        // Check which zone the fox entered by name to start sneaking or running to hunt
        if (other.name.Contains("SneakZone") && currentState == FoxState.Walk)
        {
            if (ignoreSneakZone)
            {
                // Sneak 무시 옵션이 켜져있으면 바로 타겟을 찾고 달리기 시작합니다.
                FindClosestChicken();
                SetState(FoxState.Run);
            }
            else
            {
                // 기존처럼 Sneak 모드로 진입
                SetState(FoxState.Sneak);
            }
        }
        else if (other.name.Contains("RunZone") && currentState != FoxState.Run && currentState != FoxState.Jump && currentState != FoxState.Escape)
        {
            FindClosestChicken();
            SetState(FoxState.Run);
        }
        // HuntingZone and OutZone checks are removed as they are now handled by the Raycast.
    }

    private void SetState(FoxState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case FoxState.Walk:
                animator.CrossFade("Fox_Walk", 0.2f);
                break;
            case FoxState.Sneak:
                animator.CrossFade("Fox_Sneak", 0.2f);
                break;
            case FoxState.Catching: 
            case FoxState.Escape:
            case FoxState.Run:
                animator.CrossFade("Fox_Run", 0.2f);
                
                // Panic all chickens when Fox starts running to hunt
                if (newState == FoxState.Run)
                {
                    // [Opt] Static registry replaces FindGameObjectsWithTag (zero GC)
                    foreach (RandomChickenAnimation anim in RandomChickenAnimation.All)
                        anim.Panic(5f);
                }
                break;
            case FoxState.Jump:
                animator.CrossFade("Fox_Jump", 0.1f);
                break;
        }
    }

    private IEnumerator JumpRoutine()
    {
        SetState(FoxState.Jump);
        isJumpMoving = false;

        // Wait for anticipation (crouching)
        yield return new WaitForSeconds(jumpAnticipationTime);

        // Start moving horizontally
        isJumpMoving = true;
        yield return new WaitForSeconds(jumpMoveTime);

        // Stop moving horizontally for landing recovery
        isJumpMoving = false;
        
        // Wait out the rest of the total jump duration
        float remainingTime = jumpDuration - (jumpAnticipationTime + jumpMoveTime);
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);

        // After jumping, resume running towards chicken
        SetState(FoxState.Run);
    }

    private IEnumerator JumpEscapeRoutine()
    {
        SetState(FoxState.Jump);
        isJumpMoving = false;

        // Wait for anticipation (crouching)
        yield return new WaitForSeconds(jumpAnticipationTime);

        // Start moving horizontally
        isJumpMoving = true;
        yield return new WaitForSeconds(jumpMoveTime);

        // Stop moving horizontally for landing recovery
        isJumpMoving = false;
        
        // Wait out the rest of the total jump duration
        float remainingTime = jumpDuration - (jumpAnticipationTime + jumpMoveTime);
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);

        // After jumping the fence outward, resume escaping
        SetState(FoxState.Escape);

        // --- [핵심 추가] 착지 직후에 무조건 바깥 방향으로 새로운 목적지를 강제 갱신시켜버림! (뒤돌아보는 버그 완전 차단) ---
        if (currentState == FoxState.Escape)
        {
            Vector3 awayDir = transform.forward; // 일단 뛴 방향 앞쪽으로
            if (huntingZoneCenter != null)
            {
                // 착지한 현재 위치를 기준으로 다시 바깥쪽 계산
                awayDir = (transform.position - huntingZoneCenter.position).normalized;
                awayDir.y = 0;
            }
            // 미끄러지는 벽 타기 로직 다시 적용해서 멀리 좌표 찍어줌 (20 거리)
            escapeDestination = GetSlidingEscapeDestination(transform.position, awayDir, 20f);

            // NavMeshAgent가 유효하고 동작 중일 때만 목적지 하드 리셋
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath(); // 꼬여있던 이전 경로 싹 지우기
                agent.SetDestination(escapeDestination); // 새 목적지 강제 주입
            }
        }
    }

    private void FindClosestChicken()
    {
        // [Opt] Static registry replaces FindGameObjectsWithTag (zero GC)
        float closestDistance = Mathf.Infinity;
        Transform closestChicken = null;

        foreach (RandomChickenAnimation chicken in RandomChickenAnimation.All)
        {
            float distance = Vector3.Distance(transform.position, chicken.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestChicken = chicken.transform;
            }
        }
        targetChicken = closestChicken;
    }

    public void StopAnimation()
    {
        StopAllCoroutines();
        currentState = FoxState.Dead;
        if (agent != null && agent.isActiveAndEnabled) agent.isStopped = true;
    }

    public void FleeFrom(Vector3 dangerPosition)
    {
        // 만약 이미 죽었거나, 닭을 잡는 중이거나, 이미 맵 밖으로 탈출(Escape) 중이라면 무시합니다.
        if (currentState == FoxState.Dead || currentState == FoxState.Catching || currentState == FoxState.Escape) return;

        StopAllCoroutines();
        
        // 놀라서 달아날 방향 반환 (총알이 떨어진 곳 반대 방향)
        Vector3 fleeDirection = (transform.position - dangerPosition).normalized;
        if (fleeDirection == Vector3.zero) fleeDirection = -transform.forward;
        fleeDirection.y = 0; 
        
        // 해당 뱡향 쪽으로 내비메시 위에서 도달 가능한 유효한 도망 목적지 찾기 (벽 타기 로직 적용)
        escapeDestination = GetSlidingEscapeDestination(transform.position, fleeDirection.normalized, 20f);

        SetState(FoxState.Escape);
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
        
        // 닭을 물고 달아날 때와 동일하게 목적지로 뛰어가서 사라지도록 합니다.
        StartCoroutine(EscapeAndDisappearRoutine());
    }

    public void TakeDamage(int damage, Vector3 hitPoint)
    {
        // 이미 죽었다면 데미지 무시
        if (currentState == FoxState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"여우 피격! 부위별 데미지: {damage}, 남은 체력: {currentHealth}");

        if (currentHealth <= 0)
        {
            // 체력이 다 달면 사망
            StopAnimation();
            animator.SetTrigger("Die");
            
            // --- [신규 로직] 우측 상단 킬 정보 UI 갱신 ---
            if (KillCountManager.Instance != null)
            {
                KillCountManager.Instance.AddKill();
            }

            // 사망 시 콜라이더 비활성화 후 15초 뒤 가라앉기 코루틴 시작
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;
            
            StartCoroutine(SinkAndReturnRoutine());
        }
        else
        {
            // 생존했으면 도망가기 (이미 Escape 중이라면 FleeFrom 내부에서 알아서 무시됨)
            FleeFrom(hitPoint);
        }
    }

    private IEnumerator SinkAndReturnRoutine()
    {
        yield return new WaitForSeconds(15f);

        // ★ [버그 핵심] 내비메시 에이전트가 켜진 채로 비활성화되면 나중에 다시 켤 때 이 죽은 위치로 강제 스냅백 됩니다.
        // 완전히 비활성화(풀 반납) 되기 전에 에이전트를 확실히 꺼줍니다.
        if (agent != null) agent.enabled = false;

        float sinkDuration = 3f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos - new Vector3(0, 2.5f, 0);

        while (elapsed < sinkDuration)
        {
            if (KillCountManager.isGameEnding) yield break;
            
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / sinkDuration);
            yield return null;
        }

        if (ObjectPoolManager.Instance != null) ObjectPoolManager.Instance.ReturnToPool(gameObject);
        else Destroy(gameObject);
    }

    // 주어진 위치(center) 근처 반경(radius) 내에서 항상 안전하고 도달 가능한 가장 가까운 NavMesh 좌표를 반환하는 함수
    private Vector3 GetRandomNavMeshLocation(Vector3 center, float radius)
    {
        // 중심점에서 랜덤한 오프셋 생성
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;
        
        NavMeshHit hit;
        // 주어진 반경 내에서 가장 가까운 내비메시 위치 찾기
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        
        // 도저히 찾을 수 없을 땐, 에러 방지를 위해 현재 위치 반환
        return transform.position;
    }

    // --- [신규 추가] 내비메시 테두리를 감지하고 벽을 따라 미끄러지는(슬라이딩) 목적지를 반환하는 함수 ---
    private Vector3 GetSlidingEscapeDestination(Vector3 startPos, Vector3 direction, float distance)
    {
        Vector3 targetPos = startPos + (direction * distance);
        NavMeshHit hit;

        // 1차 레이캐스트: 목적지로 향하는 도중 내비메시 끝(맵 테두리)에 닿는지 검사
        if (NavMesh.Raycast(startPos, targetPos, out hit, NavMesh.AllAreas))
        {
            // 부딪힌 벽의 법선 벡터(수직 방향)를 구함
            Vector3 wallNormal = hit.normal;
            wallNormal.y = 0; // 평면상 이동을 위해 Y축 무시

            // 벡터 투영: 원래 가려던 방향을 벽면에 밀착시켜 미끄러지는(Slide) 새로운 방향 도출
            Vector3 slideDir = Vector3.ProjectOnPlane(direction, wallNormal).normalized;
            
            // 미끄러지는 방향으로 새로운 타겟 위치 계산
            Vector3 slideTarget = hit.position + (slideDir * (distance * 0.5f)); // 남은 거리를 절반 정도로 보정

            // 2차 레이캐스트: 'ㄱ'자 구석인지(미끄러지는 방향도 막혀있는지) 한 번 더 검사
            NavMeshHit cornerHit;
            if (NavMesh.Raycast(hit.position, slideTarget, out cornerHit, NavMesh.AllAreas))
            {
                // 여기도 막혔다면 완벽한 구석(코너)에 갇힌 것! 
                // 왔던 길(slideDir)의 아예 반대 방향으로 뒤를 돌아 맵을 타고 탈출하게 만듦
                Vector3 escapeCornerDir = -slideDir;
                Vector3 cornerTarget = cornerHit.position + (escapeCornerDir * (distance * 0.5f));
                
                // 최종적으로 구한 코너 탈출 좌표가 안전한 내비메시 위인지 SamplePosition으로 최종 보정
                NavMeshHit finalHit;
                if (NavMesh.SamplePosition(cornerTarget, out finalHit, 5f, NavMesh.AllAreas))
                {
                    return finalHit.position;
                }
                return startPos; // 최후의 보루 
            }
            else
            {
                // 코너가 아니라 평범한 직선 벽이므로 정상적으로 미끄러지며 계속 도망감
                return slideTarget;
            }
        }

        // 레이저가 어디에도 안 부딪혔다면(탁 트인 내부) 원래 목적지로 직진!
        return targetPos;
    }
}
