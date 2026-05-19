using UnityEngine;
/*
 * 점프형 적
 * 평소엔 좌우를 배회
 * 플레이어가 인식 범위에 들어오면 플레이어를 향해 이동
 * 플레이어가 공격 범위에 들어오면 점프 공격
 * 만약 한번이라도 플레이어를 인식한 경우 플레이어가 공격 범위를 벗어나도 계속 추적
 * 점프 공격
 * 1. 플레이어가 공격 범위내에 들어오면 준비 자세(현재는 빨간색으로 색상 변경으로 표시)
 * 2. 점프하여 플레이어를 향해 이동
 * 3.착지 시 충격파 발생(범위 공격) 플레이어가 범위 내에 있으면 대미지
 * 4. 쿨타임 대기 후 1번으로 회귀
 */

/*
 * 점프형 적 - 이동 및 배회 제어 (Move)
 * 1. 평소엔 좌우를 배회 (Patrol)
 * 2. 플레이어 인식 범위 진입 시 끝까지 추적 (Tracking)
 * 3. 공격 스크립트가 작동 중(준비, 공격)일 때는 멈춤
 */
[RequireComponent(typeof(Rigidbody2D))]
public class JumpEnemyMove : MonoBehaviour
{
    [Header("배회 설정")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolDistance = 3f;   // 배회 반경

    [Header("감지 설정")]
    [SerializeField] private float findPlayerRange = 6f;
    [SerializeField] private LayerMask playerLayer;       // 플레이어 레이어 검출용

    private Rigidbody2D rb;
    private JumpEnemyAttack attackScript;
    // private Animator anim; 

    private Transform targetPlayer;
    private Vector2 patrolOrigin;
    private float patrolDir = 1f;
    private bool foundPlayer = false; // 한 번 인식하면 끝까지 추적

    // 공격 스크립트가 가져다 쓸 정보
    public Transform TargetPlayer => targetPlayer;
    public bool FoundPlayer => foundPlayer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attackScript = GetComponent<JumpEnemyAttack>();
        // anim = GetComponentInChildren<Animator>();

        patrolOrigin = transform.position;
    }

    private void Update()
    {
        // 공격 스크립트가 준비(기모으기) 중이거나 점프 중이면 이동 연산 정지
        if (attackScript != null && attackScript.IsAttackingOrReady)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            // anim?.SetBool("isMoving", false);
            return;
        }

        FindPlayer();

        if (foundPlayer && targetPlayer != null)
        {
            MoveToPlayer();
        }
        else
        {
            Patrol();
        }
    }

    private void FindPlayer()
    {
        if (foundPlayer) return;

        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, findPlayerRange, playerLayer);
        if (playerCollider != null)
        {
            foundPlayer = true;
            targetPlayer = playerCollider.transform;
        }
    }

    private void Patrol()
    {
        float distFromOrigin = transform.position.x - patrolOrigin.x;
        if (distFromOrigin > patrolDistance) patrolDir = -1f;
        if (distFromOrigin < -patrolDistance) patrolDir = 1f;

        rb.linearVelocity = new Vector2(patrolDir * moveSpeed * 0.6f, rb.linearVelocity.y);
        FlipSprite(patrolDir);
        // anim?.SetBool("isMoving", true);
    }

    private void MoveToPlayer()
    {
        float direction = targetPlayer.position.x > transform.position.x ? 1f : -1f;
        float distanceToPlayer = Mathf.Abs(targetPlayer.position.x - transform.position.x);

        // 공격 사거리 근처에 도달할 때까지만 걸어서 추적
        if (distanceToPlayer > attackScript.AttackRange * 0.9f)
        {
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            FlipSprite(direction);
            // anim?.SetBool("isMoving", true);
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            // anim?.SetBool("isMoving", false);
        }
    }

    private void FlipSprite(float direction)
    {
        if (Mathf.Abs(direction) > 0.01f)
        {
            transform.localScale = new Vector3(direction > 0 ? 1f : -1f, 1f, 1f);
        }
    }

    // 감지 범위 노랑으로 표시
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, findPlayerRange);
    }
}