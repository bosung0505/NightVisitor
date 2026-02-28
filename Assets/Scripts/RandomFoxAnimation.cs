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
                agent.speed = runSpeed * escapeSpeedMultiplier;
                agent.SetDestination(escapeDestination);
                
                // When escaping, the destination is often far off the NavMesh (50m away).
                // When the agent reaches the edge of the NavMesh, it will try to pathfind 
                // and start spinning wildly. We need to disable its rotation and force it forward.
                
                // Calculate distance to the edge of the current calculated path
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                {
                    // Turn off automatic rotation so it doesn't spin
                    agent.updateRotation = false;
                    
                    // Manually keep it facing the escape direction and push it forward into the void
                    Vector3 dir = (escapeDestination - transform.position).normalized;
                    dir.y = 0;
                    if (dir != Vector3.zero)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(dir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
                    }
                    
                    // Force translate forward, ignoring NavMesh boundaries
                    transform.Translate(Vector3.forward * (runSpeed * escapeSpeedMultiplier) * Time.deltaTime, Space.Self);
                }
                else
                {
                    // While still running properly on the NavMesh, let the agent handle everything
                    agent.updateRotation = true;
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

        // Calculate escape direction (opposite of entry direction with slight randomness)
        Vector3 reverseDir = -entryDirection;
        float randomAngle = Random.Range(-30f, 30f);
        Vector3 escapeDir = Quaternion.Euler(0, randomAngle, 0) * reverseDir;
        escapeDir.y = 0;
        
        // Create an arbitrary far point in that direction for the agent to run towards
        escapeDestination = transform.position + (escapeDir.normalized * 50f);

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
        if (currentState == FoxState.Dead || currentState == FoxState.Catching) return;

        StopAllCoroutines();
        StartCoroutine(FleeRoutine(dangerPosition));
    }

    private IEnumerator FleeRoutine(Vector3 dangerPosition)
    {
        currentState = FoxState.Flee;

        Vector3 fleeDirection = (transform.position - dangerPosition).normalized;
        fleeDirection.y = 0; 
        
        // Calculate a safe point far away to flee towards
        Vector3 safeDestination = transform.position + (fleeDirection * 20f);
        
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.speed = runSpeed * fleeSpeedMultiplier;
            agent.SetDestination(safeDestination);
        }

        animator.CrossFade("Fox_Run", 0.1f);
        yield return new WaitForSeconds(fleeDuration);

        // After fleeing, go back to walk
        SetState(FoxState.Walk);
    }
}
