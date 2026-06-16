using UnityEngine;

/*
유령 이동 스크립트
1. 플레이어 미감지 시: 좌우를 루프를 그리며 배회 (유영)
2. 플레이어 감지 시: 
(1) 멈춰서돌진 준비 (반투명 상태 진입) 후 플레이어 위치로 돌진
(2) 접촉 성공 시 '공격패턴'의 1번으로 이동
(3) 접촉 실패 시 실체화 + 느린 추적(유영과 같은 스피드)
(4) 공격 쿨타임이 끝나고 공격 범위 내에 있을 시 돌진공격

특이사항: 플레이어 감지 시 반투명 상태가 됨, 벽과 땅을 무시하고 이동 
 */
public class GhostEnemyMove : MonoBehaviour, IHitReaction
{
    private enum GhostState
    {
        Patrol,
        SlowChase,
        ChargeReady,
        Charge,
        Possessing,
        Retreat
    }

    [Header("현재 상태")]
    [SerializeField] private GhostState currentState = GhostState.Patrol;

    public Transform player;

    [Header("감지 설정")]
    public float detectionRange = 6f;
    public float chargeAttackRange = 3f;
    public LayerMask playerLayer;

    [Header("배회 설정")]
    public float patrolWidth = 3f;
    public float patrolHeight = 1.2f;
    public float patrolSpeed = 1f;
    public float followStrength = 5f;

    [Header("돌진 설정")]
    public float chargeSpeed = 8f;
    public float chargeDuration = 1.2f;
    public float chargeCooldown = 2.5f;

    [Header("돌진 준비 설정")]
    public float chargeReadyTime = 0.8f;
    private float chargeReadyTimer;

    [Header("추적 속도")]
    public float slowChaseSpeed = 1f;

    [Header("공격 후 후퇴")]
    public float retreatSpeed = 6f;
    public float retreatDuration = 0.4f;

    [Header("은신 투명도")]
    public float transparentAlpha = 0.35f;

    [Header("타겟 설정")]
    [SerializeField] private Transform playerTransform;
    public bool isFacingRight = true;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private GhostEnemyAttack ghostEnemyAttack;
    private Collider2D ghostCollider;
    private Animator animator;

    private Vector2 originPosition;
    private Vector2 chargeDirection;
    private Vector2 retreatDirection;


    private float patrolTime;
    private float chargeTimer;
    private float cooldownTimer;
    private float retreatTimer;

    private bool hasDetectedPlayer = false;
    private bool isDead = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ghostEnemyAttack = GetComponent<GhostEnemyAttack>();
        ghostCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
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
        originPosition = transform.position;
        cooldownTimer = 0f;

        SetTransparent(false);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        if (!hasDetectedPlayer)
        {
            CheckPlayerDetection();
        }
        HandleFacingDirection();
        UpdateMovementAnimation();
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            return;
        }

        switch (currentState)
        {
            case GhostState.Patrol:
                PatrolInfinity();
                break;

            case GhostState.SlowChase:
                SlowChaseMove();
                break;

            case GhostState.Charge:
                ChargeMove();
                break;

            case GhostState.ChargeReady:
                ChargeReadyMove();
                break;

            case GhostState.Possessing:
                rb.linearVelocity = Vector2.zero;
                break;

            case GhostState.Retreat:
                RetreatMove();
                break;
        }
    }

    private void UpdateMovementAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (currentState == GhostState.Possessing)
        {
            animator.SetBool("IsMoving", false);
            return;
        }

        bool isMoving = rb.linearVelocity.magnitude > 0.1f;

        animator.SetBool("IsMoving", isMoving);
    }
    private void CheckPlayerDetection()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);

        if (hit == null)
        {
            return;
        }

        player = hit.transform;
        hasDetectedPlayer = true;

        StartSlowChase();
    }

    private void PatrolInfinity()
    {
        patrolTime += Time.fixedDeltaTime * patrolSpeed;

        float x = Mathf.Sin(patrolTime) * patrolWidth;
        float y = Mathf.Sin(patrolTime * 2f) * patrolHeight;

        Vector2 targetPosition = originPosition + new Vector2(x, y);

        rb.linearVelocity = (targetPosition - rb.position) * followStrength;
    }

    private void StartSlowChase()
    {
        currentState = GhostState.SlowChase;
        SetTransparent(false);
    }

    private void SlowChaseMove()
    {
        if (player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        cooldownTimer -= Time.fixedDeltaTime;
        cooldownTimer = Mathf.Max(0f, cooldownTimer);

        Vector2 dir =  ((Vector2)player.position - rb.position).normalized;

        float distanceToPlayer = Vector2.Distance(rb.position, player.position);

        // 너무 가까우면 멈춤
        float stopDistance = 0.7f;

        if (distanceToPlayer > stopDistance)
        {
            rb.linearVelocity = dir * slowChaseSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (cooldownTimer <= 0f &&
            distanceToPlayer <= chargeAttackRange)
        {
            StartChargeReady();
        }
    }

    private void StartChargeReady()
    {
        currentState = GhostState.ChargeReady;
        chargeReadyTimer = chargeReadyTime;

        rb.linearVelocity = Vector2.zero;
    }

    private void ChargeReadyMove()
    {
        rb.linearVelocity = Vector2.zero;

        chargeReadyTimer -= Time.fixedDeltaTime;

        float progress = 1f - (chargeReadyTimer / chargeReadyTime);
        float alpha = Mathf.Lerp(1f, transparentAlpha, progress);

        SetAlpha(alpha);

        if (chargeReadyTimer <= 0f)
        {
            StartCharge();
        }
    }
    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null)
        {
            return;
        }

            Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }
    private void StartCharge()
    {
        if (player == null)
        {
            return;
        }

        currentState = GhostState.Charge;
        chargeTimer = chargeDuration;

        SetTransparent(true);

        Vector2 dir = (Vector2)player.position - rb.position;

        // 플레이어와 겹쳐있을 경우
        if (dir.sqrMagnitude <= 0.01f)
        {
            // 현재 바라보는 방향 사용
            dir = transform.right;
        }

        chargeDirection = dir.normalized;

        rb.linearVelocity = chargeDirection * chargeSpeed;
    }

    private void ChargeMove()
    {
        chargeTimer -= Time.fixedDeltaTime;
        rb.linearVelocity = chargeDirection * chargeSpeed;

        if (chargeTimer <= 0f)
        {
            cooldownTimer = chargeCooldown;
            StartSlowChase();
        }
    }

    private void StartPossessing()
    {
        currentState = GhostState.Possessing;
        rb.linearVelocity = Vector2.zero;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (ghostCollider != null)
        {
            ghostCollider.enabled = false;
        }
    }

    public void EndPossessionAndRetreat()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (ghostCollider != null)
        {
            ghostCollider.enabled = true;
        }

        if (isDead)
        {
            return;
        }
        SetTransparent(false);

        if (player != null)
        {
            retreatDirection = (rb.position - (Vector2)player.position).normalized;
        }
        else
        {
            retreatDirection = -chargeDirection;
        }

        if (retreatDirection == Vector2.zero)
        {
            retreatDirection = -chargeDirection;
        }

        retreatTimer = retreatDuration;
        currentState = GhostState.Retreat;
    }

    private void RetreatMove()
    {
        retreatTimer -= Time.fixedDeltaTime;

        rb.linearVelocity = retreatDirection * retreatSpeed;

        if (retreatTimer <= 0f)
        {
            cooldownTimer = chargeCooldown;
            StartSlowChase();
        }
    }

    private void SetTransparent(bool value)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = value ? transparentAlpha : 1f;
        spriteRenderer.color = color;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (currentState != GhostState.Charge)
        {
            return;
        }

        if (((1 << collision.gameObject.layer) & playerLayer) == 0)
        {
            return;
        }

        player = collision.transform;

        StartPossessing();

        if (ghostEnemyAttack != null)
        {
            ghostEnemyAttack.StartPossession(player);
        }
        else
        {
            EndPossessionAndRetreat();
        }
    }
    public bool OnBeforeTakeDamage(EnemyStatus enemyStatus, int damage) // 돌진 준비 자세, 돌진 중에는 무적
    {
        return currentState == GhostState.ChargeReady || currentState == GhostState.Charge;
    }

    public void OnAfterTakeDamage(EnemyStatus enemyStatus, int damage)
    {
    }
    private void HandleFacingDirection()
    {
        float directionX = 0f;

        if (currentState == GhostState.SlowChase || currentState == GhostState.ChargeReady || currentState == GhostState.Charge)
        {
            if (playerTransform != null)
            {
                directionX = playerTransform.position.x - transform.position.x;
            }
        }
        else
        {
            directionX = rb.linearVelocity.x;
        }

        if (directionX > 0.01f && !isFacingRight)
        {
            Flip();
        }
        else if (directionX < -0.01f && isFacingRight)
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
    public void Die()
    {
        isDead = true;

        rb.linearVelocity = Vector2.zero;
        SetTransparent(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chargeAttackRange);

        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? originPosition : transform.position;
        Gizmos.DrawWireCube(center, new Vector3(patrolWidth * 2f, patrolHeight * 2f, 0f));
    }
}