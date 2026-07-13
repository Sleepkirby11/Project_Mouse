using UnityEngine;

/*
 * 점프형 적 - 이동 및 배회 제어 (Move)
 * 1. 플레이어 인식 범위 진입 시 끝까지 추적 (Tracking)
 * 2. 공격 스크립트가 작동 중(준비, 공격)일 때는 멈춤
 */
[RequireComponent(typeof(Rigidbody2D))]
public class JumpEnemyMove : MonoBehaviour
{
    #region Settings & Variables

    [SerializeField] private float moveSpeed = 3f;

    [Header("감지 설정")]
    [SerializeField] private float findPlayerRange = 6f;
    [SerializeField] private LayerMask playerLayer;       // 플레이어 레이어 검출용

    [Header("타겟 설정")]
    [SerializeField] private Transform playerTransform;
    public bool isFacingRight = true;

    private Rigidbody2D rb;
    private JumpEnemyAttack attackScript;
    private Animator anim; 

    private Transform targetPlayer;
    private bool foundPlayer = false; // 한 번 인식하면 끝까지 추적
    private EnemyStatus enemyStatus;

    // 공격 스크립트가 가져다 쓸 정보
    public Transform TargetPlayer => targetPlayer;
    public bool FoundPlayer => foundPlayer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        attackScript = GetComponent<JumpEnemyAttack>();
        anim = GetComponentInChildren<Animator>();
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void Update()
    {
        if (enemyStatus != null && enemyStatus.isStunned)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            return;
        }

        FlipToTarget();
        UpdateAnimator();

        // 준비 중(기 모으는 중)일 때만 X 속도를 0으로 만들어 멈춤
        if (attackScript != null && attackScript.IsCharging)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            return;
        }

        // 점프 중(비행 중)에는 물리 속도가 유지되도록 리턴
        if (attackScript != null && attackScript.IsAttackingOrReady)
        {
            return;
        }

        FindPlayer();

        if (foundPlayer && targetPlayer != null)
        {
            MoveToPlayer();
        }
    }

    #endregion

    #region Player Detection & Movement

    private void FindPlayer()
    {
        if (foundPlayer)
        {
            return;
        }

        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, findPlayerRange, playerLayer);
        if (playerCollider != null)
        {
            foundPlayer = true;
            targetPlayer = playerCollider.transform;
        }
    }

    private void MoveToPlayer()
    {
        if (rb == null || targetPlayer == null) return;

        float direction = targetPlayer.position.x > transform.position.x ? 1f : -1f;
        float distanceToPlayer = Mathf.Abs(targetPlayer.position.x - transform.position.x);

        // 공격 사거리 근처에 도달할 때까지만 걸어서 추적
        float attackRange = attackScript != null ? attackScript.AttackRange : 3f;
        if (distanceToPlayer > attackRange * 0.9f)
        {
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            FlipSprite(direction);
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    #endregion

    #region Animation & Direction Controls

    private void UpdateAnimator()
    {
        if (anim == null)
        {
            return;
        }
        bool isJumping = attackScript != null && attackScript.IsAttackingOrReady && !attackScript.IsCharging;
        bool isCharging = attackScript != null && attackScript.IsCharging;
        bool isAirborne = attackScript != null && !attackScript.IsGrounded();

        anim.SetBool("IsCharging", isCharging);
        anim.SetBool("IsJumping", isJumping);
        anim.SetBool("IsAirborne", isAirborne);
    }

    private void FlipSprite(float direction)
    {
        if (Mathf.Abs(direction) > 0.01f)
        {
            transform.localScale = new Vector3(direction > 0 ? 1f : -1f, 1f, 1f);
        }
    }

    private void FlipToTarget()
    {
        Transform activeTarget = playerTransform != null ? playerTransform : targetPlayer;
        if (activeTarget == null)
        {
            return;
        }

        float direction = activeTarget.position.x - transform.position.x;

        if (direction > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (direction < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    #endregion

    #region Utility Methods

    public void PlayHurtJump() // 점프 중 공격당했을때 특수 애니메이션 재생
    {
        if (anim == null)
        {
            return;
        }
        anim.SetTrigger("Hurt_Jump");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, findPlayerRange);
    }

    #endregion
}