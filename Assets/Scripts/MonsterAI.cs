using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class MonsterAI : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    
    // 타격 횟수 추적
    private int hitCount = 0;
    private bool isDead = false;
    private bool isReacting = false;

    private Transform targetCamera;
    
    [Header("Wander Settings")]
    public float wanderRadius = 10f;
    public float wanderTimer = 3f;
    private float timer;

    [Header("Speed Settings")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 4f;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // 메인 카메라 타겟팅 (플레이어 방향)
        if (Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }
        else
        {
            GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
            if(camObj != null) targetCamera = camObj.transform;
        }
        
        timer = wanderTimer;
        
        if(agent != null) agent.speed = walkSpeed;
    }

    void Update()
    {
        // 죽었거나, 리액션 중이거나, 타겟이 없으면 이동 로직 무시
        if (isDead || isReacting || targetCamera == null || agent == null) return;

        if (hitCount == 0)
        {
            // 1. 타격 전: 배회(Wander) (idle2 <-> walking)
            timer += Time.deltaTime;
            if (timer >= wanderTimer)
            {
                // 랜덤한 위치로 이동
                Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, -1);
                agent.SetDestination(newPos);
                timer = 0;
            }

            // agent의 이동 속도에 맞춰 애니메이션 전환
            if (agent.velocity.magnitude > 0.1f)
            {
                animator.SetBool("IsWalking", true);
            }
            else
            {
                animator.SetBool("IsWalking", false);
            }
        }
        else if (hitCount == 1)
        {
            // 2. 1회 타격 후: 메인 카메라를 향해 걷기 (추적)
            agent.speed = walkSpeed;
            agent.SetDestination(targetCamera.position);
            animator.SetBool("IsWalking", true);
        }
        else if (hitCount == 2)
        {
            // 3. 2회 타격 후: 메인 카메라를 향해 달리기 (추적)
            agent.speed = runSpeed;
            agent.SetDestination(targetCamera.position);
            
            // 달리기 중에는 별도로 IsWalking을 끄지 않아도 됨 
            // Any State -> run 트리거를 통해 run 상태가 유지됨
        }
    }

    public void TakeDamage(int damage, Vector3 hitPoint)
    {
        if (isDead) return;

        hitCount++;

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
        if(agent != null) agent.isStopped = true;

        // 피격 리액션 한 번 재생 (Any State -> React_attack)
        animator.SetTrigger("React_attack");
        
        // 메인 카메라 방향으로 즉시 회전 (플레이어를 쳐다봄)
        if (targetCamera != null)
        {
            Vector3 direction = (targetCamera.position - transform.position).normalized;
            direction.y = 0; // x, z 평면(수평)만 고려하여 위/아래로 기울지 않게 함
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // 리액션 애니메이션이 끝날 때까지 대기 
        // (React_attack 클립 길이에 맞춰주세요. 여기서는 임시로 1.25초 대기)
        yield return new WaitForSeconds(1.25f); 

        if (!isDead)
        {
            if(agent != null) agent.isStopped = false;
            
            if (currentHitCount == 1)
            {
                // 천천히 걸어가기 위한 Bool 세팅
                animator.SetBool("IsWalking", true);
            }
            else if (currentHitCount == 2)
            {
                // 달리기 애니메이션 재생 (Any State -> run)
                animator.SetTrigger("run");
            }
        }

        isReacting = false;
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
        animator.SetTrigger("death");

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

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randDirection = UnityEngine.Random.insideUnitSphere * dist;
        randDirection += origin;
        NavMeshHit navHit;
        NavMesh.SamplePosition(randDirection, out navHit, dist, layermask);
        return navHit.position;
    }
}
