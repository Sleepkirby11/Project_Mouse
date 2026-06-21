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
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    private Coroutine jumpRoutine;
    private float originGravity;
    [SerializeField] private float cancelFallSpeed = 12f;

    private Rigidbody2D rb;
    private JumpEnemyMove moveScript;
    private SpriteRenderer spriteRenderer;

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
    }

    private void Update()
    {
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

        // 준비 끝난 순간의 플레이어 위치로 점프
        Vector2 targetPos = player.position;

        float elapsed = 0f;

        // 포물선 이동 중 직접 제어
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            Vector2 linearPos = Vector2.Lerp(startPos, targetPos, t);
            float arc = jumpHeight * Mathf.Sin(Mathf.PI * t);

            if (rb != null)
            {
                rb.MovePosition(new Vector2(linearPos.x, linearPos.y + arc));
            }

            CheckDirectHit();
            yield return null;
        }

        if (rb != null)
        {
            Vector2 landingStart = rb.position;
            float landingElapsed = 0f;
            float landingSpeedY = jumpHeight * Mathf.PI / jumpDuration; // 포물선 끝 하강 속도
            float landingSpeedX = (targetPos.x - startPos.x) / jumpDuration; // 포물선 X 속도 유지

            // 포물선을 착지할 때까지 계속 연장
            while (!IsGrounded())
            {
                landingElapsed += Time.deltaTime;
                float dx = landingSpeedX * landingElapsed;
                float dy = -landingSpeedY * landingElapsed - 0.5f * 9.8f * rb.gravityScale * landingElapsed * landingElapsed;

                rb.MovePosition(landingStart + new Vector2(dx, dy));

                CheckDirectHit();
                yield return null;
            }

            rb.gravityScale = originGravity;

            // 착지 정지
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

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
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
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    #endregion
}