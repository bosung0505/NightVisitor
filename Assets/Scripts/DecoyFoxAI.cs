using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class DecoyFoxAI : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    
    [Header("Patrol Targets")]
    [Tooltip("여우가 돌아다닐 외곽 웨이포인트 목록")]
    public Transform[] waypoints;
    [Tooltip("웨이포인트에 도착했을 때 머무는 시간(초)")]
    public float waitTimeAtWaypoint = 3f;
    
    [Header("Speeds")]
    public float runSpeed = 4f;
    public float fleeSpeedMultiplier = 2.0f;
    public float escapeSpeedMultiplier = 1.5f;
    public float escapeRotationSpeed = 3.0f;

    [Header("Health & Damage")]
    public int maxHealth = 2;
    private int currentHealth;

    [Header("Timers")]
    public float jumpDuration = 1.5f;
    public float jumpAnticipationTime = 0.3f;
    public float jumpMoveTime = 0.8f;

    public enum DecoyFoxState { Patrol, Idle, Jump, Flee, Dead, Escape }
    public DecoyFoxState currentState = DecoyFoxState.Idle;

    private Vector3 escapeDestination;
    private bool isJumpMoving = false;
    private int currentWaypointIndex = 0;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        currentHealth = maxHealth;
        
        agent.updateRotation = true;
        agent.updatePosition = true;

        if (waypoints.Length > 0)
        {
            // 처음에 자신의 위치 기준 가장 가까운 웨이포인트를 찾아 그 지점부터 순찰 시작
            float closestDistance = Mathf.Infinity;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                float distance = Vector3.Distance(transform.position, waypoints[i].position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    currentWaypointIndex = i;
                }
            }
            StartCoroutine(PatrolRoutine());
        }
        else
        {
            Debug.LogWarning("DecoyFoxAI: 할당된 웨이포인트가 없습니다!");
            SetState(DecoyFoxState.Idle);
        }
    }

    void Update()
    {
        if (currentState == DecoyFoxState.Dead || currentState == DecoyFoxState.Idle) 
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
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
        if (currentState == DecoyFoxState.Jump)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
            if (isJumpMoving)
            {
                transform.Translate(Vector3.forward * runSpeed * Time.deltaTime, Space.Self);
            }
            return;
        }

        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        agent.isStopped = false;

        CheckForFenceJump(); // 패트롤, 도망 뛸 때 울타리 넘기

        switch (currentState)
        {
            case DecoyFoxState.Patrol:
                agent.speed = runSpeed;
                if (waypoints.Length > 0 && waypoints[currentWaypointIndex] != null)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                    
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                    {
                        StartCoroutine(WaitAtWaypointRoutine());
                    }
                }
                break;
                
            case DecoyFoxState.Flee:
                agent.speed = runSpeed * fleeSpeedMultiplier;
                break;
                
            case DecoyFoxState.Escape:
                agent.speed = runSpeed * escapeSpeedMultiplier;
                agent.SetDestination(escapeDestination);

                if (agent.desiredVelocity.sqrMagnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * escapeRotationSpeed);
                }

                if (!agent.pathPending && agent.remainingDistance <= 1.0f)
                {
                    Vector3 awayFromCenterDir = transform.forward;
                    escapeDestination = GetRandomNavMeshLocation(transform.position + (awayFromCenterDir * 20f), 15f);
                    agent.SetDestination(escapeDestination);
                }
                break;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        SetState(DecoyFoxState.Patrol);
        yield return null;
    }

    private IEnumerator WaitAtWaypointRoutine()
    {
        SetState(DecoyFoxState.Idle);
        yield return new WaitForSeconds(waitTimeAtWaypoint);
        
        if (currentState == DecoyFoxState.Idle)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            SetState(DecoyFoxState.Patrol);
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
            string hitName = hit.collider.gameObject.name.ToLower();
            
            if (hitName.Contains("fence") || hit.collider.CompareTag("Fence")) 
            {
                Vector3 desiredDir = agent.desiredVelocity.normalized;
                float angleToDestination = Vector3.Angle(transform.forward, desiredDir);
                
                if (agent.velocity.magnitude > 0.1f && angleToDestination > 45f)
                {
                    return;
                }
                
                if (currentState == DecoyFoxState.Patrol)
                {
                    StartCoroutine(JumpRoutine());
                }
                else if (currentState == DecoyFoxState.Escape || currentState == DecoyFoxState.Flee)
                {
                    StartCoroutine(JumpEscapeRoutine());
                }
            }
        }
    }

    private void SetState(DecoyFoxState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case DecoyFoxState.Idle:
                // 대기 중일 때는 Walk와 동일한 애니메이션 베이스로 제자리에 멈추게 함
                animator.CrossFade("Fox_Walk", 0.2f);
                break;
            case DecoyFoxState.Patrol:
            case DecoyFoxState.Escape:
            case DecoyFoxState.Flee:
                animator.CrossFade("Fox_Run", 0.2f);
                break;
            case DecoyFoxState.Jump:
                animator.CrossFade("Fox_Jump", 0.1f);
                break;
        }
    }

    private IEnumerator JumpRoutine()
    {
        SetState(DecoyFoxState.Jump);
        isJumpMoving = false;
        yield return new WaitForSeconds(jumpAnticipationTime);
        isJumpMoving = true;
        yield return new WaitForSeconds(jumpMoveTime);
        isJumpMoving = false;
        
        float remainingTime = jumpDuration - (jumpAnticipationTime + jumpMoveTime);
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);

        SetState(DecoyFoxState.Patrol);
    }

    private IEnumerator JumpEscapeRoutine()
    {
        SetState(DecoyFoxState.Jump);
        isJumpMoving = false;
        yield return new WaitForSeconds(jumpAnticipationTime);
        isJumpMoving = true;
        yield return new WaitForSeconds(jumpMoveTime);
        isJumpMoving = false;
        
        float remainingTime = jumpDuration - (jumpAnticipationTime + jumpMoveTime);
        if (remainingTime > 0) yield return new WaitForSeconds(remainingTime);

        SetState(DecoyFoxState.Escape);
    }

    public void StopAnimation()
    {
        StopAllCoroutines();
        currentState = DecoyFoxState.Dead;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    public void FleeFrom(Vector3 dangerPosition)
    {
        if (currentState == DecoyFoxState.Dead || currentState == DecoyFoxState.Escape) return;

        StopAllCoroutines();
        
        Vector3 fleeDirection = (transform.position - dangerPosition).normalized;
        if (fleeDirection == Vector3.zero) fleeDirection = -transform.forward;
        fleeDirection.y = 0; 
        
        escapeDestination = GetRandomNavMeshLocation(transform.position + (fleeDirection.normalized * 20f), 10f);
        SetState(DecoyFoxState.Escape);
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
        
        StartCoroutine(EscapeAndDisappearRoutine());
    }

    private IEnumerator EscapeAndDisappearRoutine()
    {
        // 일정 시간 도망치다가 자연히 파괴
        yield return new WaitForSeconds(10f);
        Destroy(gameObject);
    }

    public void TakeDamage(int damage, Vector3 hitPoint)
    {
        if (currentState == DecoyFoxState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"교란 여우 피격! 데미지: {damage}, 남은 체력: {currentHealth}");

        if (currentHealth <= 0)
        {
            StopAnimation();
            animator.SetTrigger("Die");
        }
        else
        {
            FleeFrom(hitPoint);
        }
    }

    private Vector3 GetRandomNavMeshLocation(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return transform.position;
    }
}
