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

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            Debug.LogError("RandomFoxAnimation requires an Animator component!");

        // 체력 초기화
        currentHealth = maxHealth;
        
        // Disable rotation update by agent if we want manual rotation, but letting agent handle it is usually better for NavMesh.
        // We'll let the NavMeshAgent handle both position and rotation for smooth obstacle avoidance.
        agent.updateRotation = true;
        agent.updatePosition = true;

        // If hunting zone center is not assigned, try to find it by name
        if (huntingZoneCenter == null)
        {
            GameObject zone = GameObject.Find("HuntingZone");
            if (zone != null) huntingZoneCenter = zone.transform;
            else Debug.LogWarning("HuntingZone center is not assigned and could not be found.");
        }
        
        if (catchChickenObj != null) catchChickenObj.SetActive(false);

        // Start default behavior (Walk towards center)
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
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        
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
                agent.SetDestination(GetDestinationDefault());
                break;
                
            case FoxState.Sneak:
                agent.speed = sneakSpeed;
                agent.SetDestination(GetDestinationDefault());
                break;
                
            case FoxState.Run:
                agent.speed = runSpeed;
                
                // Track entry direction to use later for escaping
                entryDirection = transform.forward;

                if (targetChicken != null)
                {
                    agent.SetDestination(targetChicken.position);
                    
                    // Check for catch
                    float dist = Vector3.Distance(transform.position, targetChicken.position);
                    if (dist <= catchDistance)
                    {
                        StartCoroutine(CatchChickenRoutine());
                        return;
                    }
                }
                else
                {
                    // Lost chicken, find another or go to center
                    FindClosestChicken();
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
                // 닭장에서 더 멀어지는 바깥쪽 방향을 계산합니다.
                Vector3 awayFromCenterDir = transform.forward; // 기본값은 그냥 앞으로
                if (huntingZoneCenter != null)
                {
                    awayFromCenterDir = (transform.position - huntingZoneCenter.position).normalized;
                    awayFromCenterDir.y = 0;
                }
                
                // 다시 새로운 도망갈 곳(내비메시 위)을 찾아서 계속 뜁니다.
                escapeDestination = GetRandomNavMeshLocation(transform.position + (awayFromCenterDir * 20f), 15f);
                agent.SetDestination(escapeDestination);
            }
            break;
        }
    }

    private void CheckForFenceJump()
    {
        // Use Vector3.up for world vertical offset, ignoring local tilt of the fox model
        Vector3 rayOrigin = transform.position + (Vector3.up * raycastHeightOffset);
        float sphereRadius = 0.3f;
        
        Debug.DrawRay(rayOrigin, transform.forward * jumpTriggerDistance, Color.red);
        
        // Default to everything if user hasn't set it 
        int mask = obstacleLayerMask.value == 0 ? ~0 : obstacleLayerMask.value;
        
        RaycastHit hit;
        if (Physics.SphereCast(rayOrigin, sphereRadius, transform.forward, out hit, jumpTriggerDistance, mask))
        {
            string hitName = hit.collider.gameObject.name.ToLower();
            
            // Only log if it hits a fence, to avoid spamming the console with Terrain/Ground hits
            if (hitName.Contains("fence") || hit.collider.CompareTag("Fence")) 
            {
                // [수정점] 여우가 현재 가고자 하는 방향(agent.desiredVelocity)과 
                // 시선의 방향(transform.forward) 사이의 각도를 계산합니다.
                // 닭을 물고 빙글빙글 돌 때는 시선과 이동 목적지가 심하게 어긋나므로 
                // 이 각도가 너무 크면(예: 45도 이상) 옆에 있는 울타리라고 판단하고 점프를 무시합니다.
                
                Vector3 desiredDir = agent.desiredVelocity.normalized;
                float angleToDestination = Vector3.Angle(transform.forward, desiredDir);
                
                // 에이전트가 아직 덜 돌았거나 목적지를 향해 똑바로 가고 있지 않을 때 잡다한 점프 방지 
                // (경미한 회전은 허용하기 위해 45도 정도의 여유를 줍니다)
                if (agent.velocity.magnitude > 0.1f && angleToDestination > 45f)
                {
                    Debug.Log($"Ignored Fence Jump. Angle too steep: {angleToDestination}");
                    return;
                }

                Debug.Log("Fence Detected! Jumping. Hit: " + hit.collider.gameObject.name);
                
                // We are close to the fence. Transition to jump based on current state.
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
        
        // 내비메시 위에서 도달 가능한 유효한 도망 목적지 찾기
        escapeDestination = GetRandomNavMeshLocation(transform.position + (escapeDir.normalized * 20f), 10f);

        SetState(FoxState.Escape);
        
        // Start waiting to disappear once we are safely away
        StartCoroutine(EscapeAndDisappearRoutine());
    }

    private IEnumerator EscapeAndDisappearRoutine()
    {
        // Wait until we are far enough from the center to safely disappear
        if (huntingZoneCenter != null)
        {
            while (true)
            {
                float distFromCenter = Vector3.Distance(transform.position, huntingZoneCenter.position);
                if (distFromCenter >= escapeDisappearDistance)
                {
                    break;
                }
                yield return new WaitForSeconds(0.5f); // Check twice a second
            }
        }
        else
        {
            // Fallback just in case there is no center defined
            yield return new WaitForSeconds(10f);
        }
        
        // Optionally add a fade-out effect here by shrinking or adjusting materials
        // For now, we will just silently remove the fox from the scene
        Destroy(gameObject);
    }

    private Vector3 GetDestinationDefault()
    {
        // Move towards the hunting zone center
        if (huntingZoneCenter != null) return huntingZoneCenter.position;
        return transform.position; // Stay still if no center
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == FoxState.Dead || currentState == FoxState.Flee) return;

        // Check which zone the fox entered by name to start sneaking or running to hunt
        if (other.name.Contains("SneakZone") && currentState == FoxState.Walk)
        {
            SetState(FoxState.Sneak);
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
                    GameObject[] chickens = GameObject.FindGameObjectsWithTag("Chicken");
                    foreach (GameObject chicken in chickens)
                    {
                        RandomChickenAnimation anim = chicken.GetComponent<RandomChickenAnimation>();
                        if (anim != null)
                        {
                            anim.Panic(5f); // Duration of panic
                        }
                    }
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
    }

    private void FindClosestChicken()
    {
        GameObject[] chickens = GameObject.FindGameObjectsWithTag("Chicken");
        float closestDistance = Mathf.Infinity;
        Transform closestChicken = null;

        foreach (GameObject chicken in chickens)
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
        
        // 해당 뱡향 쪽으로 내비메시 위에서 도달 가능한 유효한 도망 목적지 찾기
        escapeDestination = GetRandomNavMeshLocation(transform.position + (fleeDirection.normalized * 20f), 10f);

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
        }
        else
        {
            // 생존했으면 도망가기 (이미 Escape 중이라면 FleeFrom 내부에서 알아서 무시됨)
            FleeFrom(hitPoint);
        }
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
}
