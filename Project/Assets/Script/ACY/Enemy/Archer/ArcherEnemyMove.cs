using UnityEngine;

/*
 * 행동패턴: 자리에 고정 
 * 플레이어가 근처로 접근 시 뒤로 이동하며 거리를 벌림 (백스텝)
 */
public class ArcherEnemyMove : MonoBehaviour
{
    #region Settings & Variables

    [Header("감지 설정")]
    [SerializeField] private float detectRange = 5f; // 플레이어 감지 범위
    [SerializeField] private float escapeRange = 2f; // 백스텝 트리거 범위
    [SerializeField] private LayerMask playerLayer;  // 플레이어 레이어

    [Header("백스텝 설정")]
    [SerializeField] private float backstepSpeed = 8f; // 백스텝 이동 속도
    [SerializeField] private float backstepDuration = 0.3f; // 백스텝 지속 시간
    [SerializeField] private float backstepCooldown = 2f; // 백스텝 재사용 대기 시간

    [Header("지형 체크")]
    [SerializeField] private float groundCheckDistance = 1.5f; // 체크 거리
    [SerializeField] private float groundCheckDropY = 0.5f;    // 발 아래 체크 오프셋

    private Rigidbody2D rb; 
    private Transform player;
    private Animator anim;
    private float backstepCooldownTimer = 0f; // 백스텝 쿨타임 타이머
    private float backstepEndTimer;
    private bool isBackstepping = false; // 백스텝 여부
    private bool isFacingRight = true;
    private EnemyStatus enemyStatus;

    private static readonly int BackstepHash = Animator.StringToHash("Backstep");

    // 공격 컴포넌트에서 참조
    public Transform TargetPlayer => player;
    public bool IsBackstepping => isBackstepping;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        anim = GetComponentInChildren<Animator>();
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Update()
    {
        if (enemyStatus != null && enemyStatus.isStunned)
        {
            isBackstepping = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        if (backstepCooldownTimer > 0f)
        {
            backstepCooldownTimer -= Time.deltaTime;
        }

        // 백스텝 타이머 제어
        if (isBackstepping)
        {
            backstepEndTimer -= Time.deltaTime;
            if (backstepEndTimer <= 0f)
            {
                isBackstepping = false;
            }
        }

        CheckDetection();
        UpdateFacing();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (enemyStatus != null && enemyStatus.isStunned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 백스텝 상태일 때만 이동
        if (isBackstepping && player != null)
        {
            float moveDirX = transform.position.x > player.position.x ? 1f : -1f;

            if (!CheckGround(moveDirX))
            {
                isBackstepping = false;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = new Vector2(moveDirX * backstepSpeed, rb.linearVelocity.y);
        }
        else 
        {
            if (rb.linearVelocity.x != 0)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    #endregion

    #region Target Detection & Backstep AI

    private void CheckDetection()
    {
        // 플레이어 유효성 및 거리 체크
        if (player != null)
        {
            float sqrDist = (player.position - transform.position).sqrMagnitude; // 거리 계산

            if (sqrDist > detectRange * detectRange) // 감지 범위를 벗어나면 플레이어 초기화
            {
                player = null; // 플레이어가 멀어지면 초기화
                return; 
            }

            // 백스텝 트리거 체크
            float moveDirX = transform.position.x > player.position.x ? 1f : -1f;
            if (!isBackstepping && backstepCooldownTimer <= 0f && sqrDist < escapeRange * escapeRange && CheckGround(moveDirX))
            {
                isBackstepping = true;
                backstepEndTimer = backstepDuration;
                backstepCooldownTimer = backstepCooldown;

                if (anim != null)
                {
                    anim.SetTrigger(BackstepHash);
                }
            }
        }
        else
        {
            // 플레이어 탐색 (player가 없을 때만 탐색하여 성능 최적화)
            Collider2D col = Physics2D.OverlapCircle(transform.position, detectRange, playerLayer);
            if (col != null)
            {
                player = col.transform;
            }
        }
    }

    #endregion

    #region Direction & Flips

    private void UpdateFacing()
    {
        if (player == null)
        {
            return;
        }

        float dirX = player.position.x - transform.position.x;

        if (dirX > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (dirX < 0 && isFacingRight)
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

    #region Ground Check & Utilities

    private bool CheckGround(float dirX)
    {
        Vector2 checkOrigin = new Vector2(transform.position.x + dirX * groundCheckDistance, transform.position.y - groundCheckDropY);
        RaycastHit2D hit = Physics2D.Raycast(checkOrigin, Vector2.down, 1f, LayerMask.GetMask("Ground"));
        return hit.collider != null;
    }

    private void OnDrawGizmosSelected() // 디버그용 기즈모 코드
    {
        // 감지 범위 — 노란색
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 백스텝 범위 — 빨간색
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, escapeRange);

        // 땅 감지 범위 - 흰색
        if (player != null)
        {
            float dir = transform.position.x > player.position.x ? 1f : -1f;
            Vector3 origin = transform.position + new Vector3(dir * groundCheckDistance, -groundCheckDropY, 0);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(origin, origin + Vector3.down * 1f);
        }
    }

    #endregion
}
