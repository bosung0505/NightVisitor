using UnityEngine;
using System.Collections;

public class RandomFoxAnimation : MonoBehaviour
{
    private Animator animator;
    
    [Header("Hunting Targets")]
    [Tooltip("The central point of the Hunting Zone. Auto-finds 'HuntingZone' if left empty.")]
    public Transform huntingZoneCenter;
    [Tooltip("The chicken model in the fox's mouth. Disable this initially.")]
    public GameObject catchChickenObj;
    
    [Header("Speeds")]
    public float walkSpeed = 1f;
    public float sneakSpeed = 0.5f;
    public float runSpeed = 3f;
    public float fleeSpeedMultiplier = 2.5f;
    public float escapeSpeedMultiplier = 1.5f;
    public float rotationSpeed = 3f;

    [Header("Timers")]
    public float jumpDuration = 1.5f;
    public float fleeDuration = 3f;
    public float catchWaitDuration = 0.5f;
    [Tooltip("Distance at which the fox catches the chicken")]
    public float catchDistance = 1.5f;

    public enum FoxState { Walk, Sneak, Run, Jump, Flee, Dead, Catching, Escape }
    public FoxState currentState = FoxState.Walk;

    private Transform targetChicken;
    private Quaternion targetRotation;
    private Vector3 entryDirection;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            Debug.LogError("RandomFoxAnimation requires an Animator component!");

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
        if (currentState == FoxState.Dead || currentState == FoxState.Catching) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        float currentSpeed = 0f;
        Vector3 currentTargetPos = Vector3.zero;

        // Determine speed and target based on state
        switch (currentState)
        {
            case FoxState.Walk:
                currentSpeed = walkSpeed;
                currentTargetPos = GetDestinationDefault();
                break;
            case FoxState.Sneak:
                currentSpeed = sneakSpeed;
                currentTargetPos = GetDestinationDefault();
                break;
            case FoxState.Run:
                currentSpeed = runSpeed;
                currentTargetPos = GetTargetChickenPos();
                
                // Track entry direction to use later for escaping
                entryDirection = transform.forward;

                // Check for catch
                if (targetChicken != null)
                {
                    float dist = Vector3.Distance(transform.position, targetChicken.position);
                    if (dist <= catchDistance)
                    {
                        StartCoroutine(CatchChickenRoutine());
                        return; // Stop moving
                    }
                }
                break;
            case FoxState.Jump:
                // Move forward without recalculating angle during jump
                transform.Translate(Vector3.forward * runSpeed * Time.deltaTime, Space.Self);
                return; 
            case FoxState.Flee:
                // Rotation is already set in FleeRoutine, just move
                transform.Translate(Vector3.forward * (runSpeed * fleeSpeedMultiplier) * Time.deltaTime, Space.Self);
                return;
            case FoxState.Escape:
                // Move forward in the calculated escape direction
                transform.Translate(Vector3.forward * (runSpeed * escapeSpeedMultiplier) * Time.deltaTime, Space.Self);
                return;
        }

        // Rotate towards target and move
        if (currentTargetPos != Vector3.zero)
        {
            Vector3 direction = (currentTargetPos - transform.position).normalized;
            direction.y = 0; // Keep rotation level
            if (direction != Vector3.zero)
            {
                targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Move forward
            transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime, Space.Self);
        }
    }

    private IEnumerator CatchChickenRoutine()
    {
        SetState(FoxState.Catching);

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
        // Add random angle offset (-30 to 30 degrees)
        float randomAngle = Random.Range(-30f, 30f);
        Vector3 escapeDir = Quaternion.Euler(0, randomAngle, 0) * reverseDir;
        escapeDir.y = 0;
        
        if (escapeDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(escapeDir);
            targetRotation = transform.rotation;
        }

        SetState(FoxState.Escape);
    }

    private Vector3 GetDestinationDefault()
    {
        // Move towards the hunting zone center
        if (huntingZoneCenter != null) return huntingZoneCenter.position;
        return transform.position; // Stay still if no center
    }

    private Vector3 GetTargetChickenPos()
    {
        // If we have a live chicken, return its position
        if (targetChicken != null) return targetChicken.position;
        return transform.position; // Stay still if no chicken
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == FoxState.Dead || currentState == FoxState.Flee) return;
        
        // OutZone logic for escaping
        if (other.name.Contains("OutZone") && currentState == FoxState.Escape)
        {
            StartCoroutine(JumpEscapeRoutine());
            return;
        }

        // Check which zone the fox entered by name
        if (other.name.Contains("SneakZone") && currentState == FoxState.Walk)
        {
            SetState(FoxState.Sneak);
        }
        else if (other.name.Contains("RunZone") && currentState != FoxState.Run && currentState != FoxState.Jump && currentState != FoxState.Escape)
        {
            FindClosestChicken();
            SetState(FoxState.Run);
        }
        else if (other.name.Contains("HuntingZone") && currentState != FoxState.Jump && currentState != FoxState.Escape)
        {
            StartCoroutine(JumpRoutine());
        }
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
            case FoxState.Catching: // Stay idle or transition to bite animation here
                // animator.CrossFade("Fox_Bite", 0.1f); // If you have a bite anim
                break;
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
        yield return new WaitForSeconds(jumpDuration);

        // After jumping, resume running towards chicken
        SetState(FoxState.Run);
    }

    private IEnumerator JumpEscapeRoutine()
    {
        SetState(FoxState.Jump);
        yield return new WaitForSeconds(jumpDuration);

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
    }

    public void FleeFrom(Vector3 dangerPosition)
    {
        if (currentState == FoxState.Dead) return;

        StopAllCoroutines();
        StartCoroutine(FleeRoutine(dangerPosition));
    }

    private IEnumerator FleeRoutine(Vector3 dangerPosition)
    {
        currentState = FoxState.Flee;

        Vector3 fleeDirection = (transform.position - dangerPosition).normalized;
        fleeDirection.y = 0; 
        
        if (fleeDirection != Vector3.zero)
        {
            targetRotation = Quaternion.LookRotation(fleeDirection);
            transform.rotation = targetRotation; // Instantly turn to flee
        }

        animator.CrossFade("Fox_Run", 0.1f);
        yield return new WaitForSeconds(fleeDuration);

        // After fleeing, go back to walk to re-trigger zones, or directly to Run if angry
        SetState(FoxState.Walk);
    }
}
