using UnityEngine;
using System.Collections;

public class RandomChickenAnimation : MonoBehaviour
{
    private Animator animator;
    
    [Header("Timers")]
    [Tooltip("Minimum time the chicken stays in Idle (Blend Tree) state")]
    public float minIdleTime = 2f;
    [Tooltip("Maximum time the chicken stays in Idle (Blend Tree) state")]
    public float maxIdleTime = 6f;
    
    [Tooltip("Minimum time the chicken stays in Walk state")]
    public float minWalkTime = 2f;
    [Tooltip("Maximum time the chicken stays in Walk state")]
    public float maxWalkTime = 4f;

    [Header("Movement")]
    [Tooltip("Speed at which the chicken moves forward while walking")]
    public float moveSpeed = 1f;
    [Tooltip("Speed at which the chicken rotates to a new direction while walking")]
    public float rotationSpeed = 2f;

    [Header("Fleeing")]
    [Tooltip("Speed multiplier when fleeing")]
    public float fleeSpeedMultiplier = 2.5f;
    [Tooltip("How long the chicken runs away before calming down")]
    public float fleeDuration = 3f;

    [Header("Boundaries (Optional)")]
    [Tooltip("The central point the chicken should stay near (e.g. Fence1)")]
    public Transform boundaryCenter;
    [Tooltip("Maximum distance from the boundary center the chicken is allowed to wander")]
    public float maxRadius = 15f;

    [Header("Obstacle Avoidance")]
    [Tooltip("How far ahead the chicken looks for fences")]
    public float obstacleCheckDistance = 2.0f;
    public LayerMask obstacleLayerMask;

    private bool isWalkingState = false;
    private bool isFleeingState = false;
    private bool isPanicState = false;
    private bool isDead = false;
    private Quaternion targetRotation;
    private Coroutine behaviorRoutine;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("RandomChickenAnimation requires an Animator component on the same GameObject!");
            return;
        }

        // Start the random behavior loop
        behaviorRoutine = StartCoroutine(ChickenBehaviorRoutine());
    }

    private IEnumerator ChickenBehaviorRoutine()
    {
        while (true)
        {
            // --- IDLE PHASE ---
            isWalkingState = false;
            // Force blend tree state and also CrossFade to it for safety
            animator.SetFloat("State", 0f);
            animator.CrossFade("Blend Tree", 0.2f);

            // Wait for a random amount of time in Idle
            float idleDuration = Random.Range(minIdleTime, maxIdleTime);
            yield return new WaitForSeconds(idleDuration);

            // --- WALK PHASE ---
            isWalkingState = true;
            
            // Pick a random rotation on the Y axis
            float randomYRotation = Random.Range(0f, 360f);
            targetRotation = Quaternion.Euler(0f, randomYRotation, 0f);

            // Check if walking in this direction will take us outside the boundary
            if (boundaryCenter != null)
            {
                float distanceToCenter = Vector3.Distance(transform.position, boundaryCenter.position);
                if (distanceToCenter > maxRadius)
                {
                    // We are outside or at the edge. Steer back towards the center.
                    Vector3 directionToCenter = (boundaryCenter.position - transform.position).normalized;
                    directionToCenter.y = 0;
                    if (directionToCenter != Vector3.zero)
                    {
                        targetRotation = Quaternion.LookRotation(directionToCenter);
                    }
                }
            }

            // Force walk state parameter
            animator.SetFloat("State", 1f); 
            animator.CrossFade("Chicken_003_walk", 0.2f);

            // Wait for a random amount of time in Walk
            float walkDuration = Random.Range(minWalkTime, maxWalkTime);
            yield return new WaitForSeconds(walkDuration);
            
            // Loop repeats, going back to Idle
        }
    }

    void Update()
    {
        if (isWalkingState || isFleeingState || isPanicState)
        {
            float currentRotSpeed = rotationSpeed;
            if (isFleeingState) currentRotSpeed *= 2f;
            if (isPanicState) currentRotSpeed *= 5f; // Thrashing around very fast

            // -------------------------------------------------------------------
            // [추가된 로직] 장애물(울타리) 회피: 앞으로 가는 길에 울타리가 있으면 반사각으로 목표 방향 변경
            // -------------------------------------------------------------------
            if (!isDead)
            {
                // default to everything mask if not set
                int mask = obstacleLayerMask.value == 0 ? ~0 : obstacleLayerMask.value;
                RaycastHit hit;
                
                // 닭의 약간 위(0.5f)에서 앞쪽으로 구(Sphere)를 쏘아서 체크
                if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, transform.forward, out hit, obstacleCheckDistance, mask))
                {
                    string hitName = hit.collider.gameObject.name.ToLower();
                    if (hitName.Contains("fence") || hit.collider.CompareTag("Fence"))
                    {
                        Debug.DrawRay(hit.point, hit.normal, Color.blue, 0.5f);
                        // 충돌한 표면(울타리)의 법선 백터(normal)를 기준으로 반사되는(팅겨나가는) 방향 계산
                        Vector3 reflectDir = Vector3.Reflect(transform.forward, hit.normal);
                        reflectDir.y = 0;
                        if (reflectDir != Vector3.zero)
                        {
                            targetRotation = Quaternion.LookRotation(reflectDir.normalized);
                        }
                    }
                }
            }

            // Smoothly rotate towards the target rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentRotSpeed * Time.deltaTime);

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // Only move if the specific animation is playing
            if (isWalkingState && stateInfo.IsName("Chicken_003_walk") && !animator.IsInTransition(0))
            {
                transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime, Space.Self);
            }
            else if ((isFleeingState || isPanicState) && stateInfo.IsName("Chicken_002_run") && !animator.IsInTransition(0))
            {
                float mult = isPanicState ? fleeSpeedMultiplier * 1.5f : fleeSpeedMultiplier;
                transform.Translate(Vector3.forward * (moveSpeed * mult) * Time.deltaTime, Space.Self);
            }
        }
    }

    public void StopAnimation()
    {
        // Stop the coroutine so it doesn't try to change animations anymore
        StopAllCoroutines();
        // Ensure the chicken stops moving forward and rotating
        isWalkingState = false;
        isFleeingState = false;
        isPanicState = false;
        isDead = true;
    }

    public void FleeFrom(Vector3 dangerPosition)
    {
        if (isDead) return;

        if (behaviorRoutine != null)
        {
            StopCoroutine(behaviorRoutine);
        }
        
        StartCoroutine(FleeRoutine(dangerPosition));
    }

    private IEnumerator FleeRoutine(Vector3 dangerPosition)
    {
        isWalkingState = false;
        isFleeingState = true;
        isPanicState = false;

        Vector3 fleeDirection = (transform.position - dangerPosition).normalized;
        fleeDirection.y = 0; // Keep on same plane

        if (fleeDirection != Vector3.zero)
        {
            targetRotation = Quaternion.LookRotation(fleeDirection);
        }

        // According to user, chicken has a "Run" transition/animation
        animator.SetTrigger("Run"); // Sometimes just setting the trigger helps jump to it faster
        animator.CrossFade("Chicken_002_run", 0.1f);

        yield return new WaitForSeconds(fleeDuration);

        isFleeingState = false;
        behaviorRoutine = StartCoroutine(ChickenBehaviorRoutine());
    }

    public void Panic(float duration)
    {
        if (isDead) return;

        if (behaviorRoutine != null)
        {
            StopCoroutine(behaviorRoutine);
        }
        
        behaviorRoutine = StartCoroutine(PanicRoutine(duration));
    }

    private IEnumerator PanicRoutine(float duration)
    {
        isWalkingState = false;
        isFleeingState = false;
        isPanicState = true;

        animator.SetTrigger("Run");
        animator.CrossFade("Chicken_002_run", 0.1f);

        float timer = 0f;
        while (timer < duration)
        {
            // Pick a completely random direction
            float randomYRotation = Random.Range(0f, 360f);
            targetRotation = Quaternion.Euler(0f, randomYRotation, 0f);

            // Run in that direction for a short time (frantic changing of direction)
            float runTime = Random.Range(0.2f, 0.6f);
            yield return new WaitForSeconds(runTime);
            timer += runTime;
        }

        isPanicState = false;
        behaviorRoutine = StartCoroutine(ChickenBehaviorRoutine());
    }
}
