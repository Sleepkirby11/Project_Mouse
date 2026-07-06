using System.Collections;
using UnityEngine;

/*
 * 플레이어 감지 시 준비자세
 * 플레이어의 위치로 점프
 * (몸체 및 착지 시 충격파에 공격 판정)
 */
public class JumpEnemyAttack : MonoBehaviour, IHitReaction
{
    #region Settings & Variables

    [Header("점프 공격 설정")]
    [SerializeField] private float attackRange = 3f;       // 공격 시전 사거리
    [SerializeField] private float attackCooldown = 3f;    // 공격 간 쿨타임

    [Header("점프 설정")]
    [SerializeField] private float jumpHeight = 4f;        // 포물선 최고 높이
    [SerializeField] private float jumpDuration = 1f;      // 점프 소요 시간
    [SerializeField] private float readyDuration = 0.6f;   // 빨갛게 기 모으는 시간

    [Header("접촉 공격 설정")]
    [SerializeField] private int directHitDamage = 1;      // 몸통박치기 대미지
    [SerializeField] private float directHitRadius = 0.6f; // 몸통박치기 판정 범위

    [Header("충격파 설정")]
    [SerializeField] private int shockwaveDamage = 1;
    [SerializeField] private Vector2 shockwaveBoxSize = new Vector2(4f, 0.6f); // 가로, 세로
    [SerializeField] private Vector2 shockwaveBoxOffset = new Vector2(0f, -0.4f); // 발밑 위치 보정
    [SerializeField] private LayerMask playerLayer;

    [Header("충격파 VFX")]
    [SerializeField] private string shockwavePoolKey = "Shockwave";

    [Header("착지 체크")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    private Coroutine jumpRoutine;
    private float originGravity;
    [SerializeField] private float cancelFallSpeed = 12f;

    private Rigidbody2D rb;
    private JumpEnemyMove moveScript;
    private SpriteRenderer spriteRenderer;
    private EnemyStatus enemyStatus;

    private bool isAttackingOrReady = false;
    private bool hasDirectHit = false;
    private float lastAttackTime = -99f;
    private bool isCharging = false;

    // 이동 스크립트에서 읽음
    public float AttackRange => attackRange;
    public bool IsAttackingOrReady => isAttackingOrReady;
    public bool IsCharging => isCharging;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            originGravity = rb.gravityScale;
        }
        moveScript = GetComponent<JumpEnemyMove>();

        // 자식 객체(스프라이트)에서 컴포넌트들을 찾음
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Update()
    {
        if (enemyStatus != null && enemyStatus.isStunned)
        {
            if (isAttackingOrReady)
            {
                CancelJumpAttackByHit();
            }
            return;
        }

        // 이미 공격 중이거나, 아직 플레이어를 발견하지 못했다면 리턴
        if (isAttackingOrReady || moveScript == null || !moveScript.FoundPlayer)
        {
            return;
        }

        Transform player = moveScript.TargetPlayer;
        if (player == null)
        {
            return;
        }

        float distanceToPlayer = Mathf.Abs(player.position.x - transform.position.x);

        // 사거리 이내 진입 && 쿨타임 만료 시 공격 시작
        if (distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            jumpRoutine = StartCoroutine(JumpAttackComboRoutine(player));
        }
    }

    #endregion

    #region Jump Attack Logic

    private IEnumerator JumpAttackComboRoutine(Transform player)
    {
        isAttackingOrReady = true;
        isCharging = true;
        hasDirectHit = false;

        yield return new WaitForSeconds(readyDuration);

        // 점프 시작
        isCharging = false;

        Vector2 startPos = transform.position;
        Vector2 targetPos = player.position;

        // 물리 점프 속도 및 중력 계산
        float T = jumpDuration;
        float dy = targetPos.y - startPos.y;
        float dx = targetPos.x - startPos.x;

        // 9.81f 대신 Physics2D.gravity.y 크기 사용 (음수일 수 있으므로 절대값 처리)
        float gravityMagnitude = Mathf.Abs(Physics2D.gravity.y);
        if (gravityMagnitude < 0.1f) gravityMagnitude = 9.81f;

        // 원하는 최고 높이(jumpHeight)에 도달하기 위한 가상 중력 및 중력 배율 계산
        float desiredGravity = (8f * jumpHeight) / (T * T);
        float customGravityScale = desiredGravity / gravityMagnitude;

        // Y축 초기 속도: dy와 T를 이용한 포물선 공식 + 최소 점프 높이 보장
        float vy = (dy / T) + (0.5f * desiredGravity * T);
        float minVy = (4f * jumpHeight) / T;
        if (vy < minVy)
        {
            vy = minVy;
        }

        // X축 초기 속도
        float vx = dx / T;

        if (rb != null)
        {
            rb.gravityScale = customGravityScale;
            rb.linearVelocity = new Vector2(vx, vy);
        }

        // 아주 짧은 시간(예: 0.15초) 동안은 바닥 체크를 무시하여 발사 직후 착지 처리되는 것을 방지
        float minJumpTime = 0.15f;
        float elapsed = 0f;
        while (elapsed < minJumpTime)
        {
            elapsed += Time.deltaTime;
            CheckDirectHit();
            yield return null;
        }

        // 착지할 때까지 혹은 최대 비행 시간(3초)을 넘을 때까지 대기
        float maxAirTime = 3.0f;
        elapsed = 0f;
        while (!IsGrounded() && elapsed < maxAirTime)
        {
            elapsed += Time.deltaTime;
            CheckDirectHit();
            yield return null;
        }

        if (rb != null)
        {
            // 착지 시 기존 중력 복원 및 정지
            rb.gravityScale = originGravity;
            rb.linearVelocity = Vector2.zero;
        }

        // 착지 충격파
        ExecuteShockwave();

        lastAttackTime = Time.time;
        isAttackingOrReady = false;
        jumpRoutine = null;
    }

    public void CancelJumpAttackByHit()
    {
        if (!isAttackingOrReady)
        {
            return;
        }

        // 기 모으는 중(지상)에 공격받은 경우에는 취소하지 않음
        if (isCharging)
        {
            return;
        }

        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
            jumpRoutine = null;
        }

        if (rb != null)
        {
            rb.gravityScale = originGravity;
            rb.linearVelocity = new Vector2(0f, -cancelFallSpeed);
        }

        lastAttackTime = Time.time;

        hasDirectHit = false;
        isCharging = false;

        moveScript?.PlayHurtJump();
        isAttackingOrReady = false;
    }

    #endregion

    #region Combat Callbacks

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        return false; // 대미지는 그대로 받음
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        CancelJumpAttackByHit();
    }

    #endregion

    #region Collision & Ground Check

    private void CheckDirectHit()
    {
        if (hasDirectHit)
        {
            return;
        }

        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, directHitRadius, playerLayer);

        if (playerCollider != null)
        {
            if (playerCollider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(directHitDamage);
                hasDirectHit = true;
            }
        }
    }

    private void ExecuteShockwave()
    {
        Debug.Log($"{gameObject.name} 충격파 공격");

        Vector2 shockwaveCenter = (Vector2)transform.position + shockwaveBoxOffset;
        GameObject swObj = PoolingManager.Instance.Get(shockwavePoolKey, shockwaveCenter, Quaternion.identity); 
        if (swObj == null)
        {
            Debug.LogWarning("Failed to pool shockwave object!");
        }

        Collider2D playerCollider = Physics2D.OverlapBox(shockwaveCenter, shockwaveBoxSize, 0f, playerLayer);

        if (playerCollider != null)
        {
            if (playerCollider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(shockwaveDamage);
            }
        }
    }

    public bool IsGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    #endregion

    #region Utility Methods

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, directHitRadius);

        Gizmos.color = Color.magenta;
        Vector2 shockwaveCenter = (Vector2)transform.position + shockwaveBoxOffset;
        Gizmos.DrawWireCube(shockwaveCenter, shockwaveBoxSize);

        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
    }

    #endregion
}