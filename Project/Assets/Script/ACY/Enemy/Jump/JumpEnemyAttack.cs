using System.Collections;
using UnityEngine;

/*
 * 플레이어의 Y축이 낮게 변했을때
 * 점프 공격이 끝난 후 땅으로 내려갈때 움직임 부자연스러운 문제 있음
 */
public class JumpEnemyAttack : MonoBehaviour
{
    [Header("점프 공격 설정")]
    [SerializeField] private float attackRange = 3f;       // 공격 시전 사거리
    [SerializeField] private float attackCooldown = 3f;    // 공격 간 쿨타임

    [Header("점프 설정")]
    [SerializeField] private float jumpHeight = 4f;        // 포물선 최고 높이
    [SerializeField] private float jumpDuration = 1f;      // 점프 소요 시간
    [SerializeField] private float readyDuration = 0.6f;   // 빨갛게 기 모으는 시간

    [Header("낙하 설정")]
    [SerializeField] private float fallStartSpeed = 10f;   // 점프 종료 후 아래로 떨어지는 시작 속도
    [SerializeField] private float fallForwardSpeed = 3f;  // 낙하 중 앞으로 유지할 속도

    [Header("접촉 공격 설정")]
    [SerializeField] private int directHitDamage = 1;      // 몸통박치기 대미지
    [SerializeField] private float directHitRadius = 0.6f; // 몸통박치기 판정 범위

    [Header("충격파 설정")]
    [SerializeField] private int shockwaveDamage = 1;
    [SerializeField] private Vector2 shockwaveBoxSize = new Vector2(4f, 0.6f); // 가로, 세로
    [SerializeField] private Vector2 shockwaveBoxOffset = new Vector2(0f, -0.4f); // 발밑 위치 보정
    [SerializeField] private LayerMask playerLayer;

    [Header("착지 체크")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private JumpEnemyMove moveScript;
    private SpriteRenderer spriteRenderer;
    // private Animator anim;

    private bool isAttackingOrReady = false;
    private bool hasDirectHit = false;
    private float lastAttackTime = -99f;

    // 이동 스크립트가 멈춰야 할 타이밍을 알려주기 위한 프로퍼티
    public float AttackRange => attackRange;
    public bool IsAttackingOrReady => isAttackingOrReady;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        moveScript = GetComponent<JumpEnemyMove>();

        // 자식 객체(스프라이트)에서 컴포넌트들을 찾음
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // anim = GetComponentInChildren<Animator>();
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
            StartCoroutine(JumpAttackComboRoutine(player));
        }
    }

    private IEnumerator JumpAttackComboRoutine(Transform player)
    {
        isAttackingOrReady = true;
        hasDirectHit = false;

        // 준비 자세 (색상 변경)
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }

        // anim?.SetTrigger("Ready");
        yield return new WaitForSeconds(readyDuration);

        // 점프 시작
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        // anim?.SetTrigger("Jump");

        Vector2 startPos = transform.position;

        // 준비 끝난 순간의 플레이어 위치로 점프
        Vector2 targetPos = player.position;

        float elapsed = 0f;
        float originGravity = rb.gravityScale;

        // 진행 방향 저장
        float fallDirectionX = Mathf.Sign(targetPos.x - startPos.x);

        // 포물선 이동 중 직접 제어
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpDuration;

            Vector2 linearPos = Vector2.Lerp(startPos, targetPos, t);
            float arc = jumpHeight * Mathf.Sin(Mathf.PI * t);

            rb.MovePosition(new Vector2(linearPos.x, linearPos.y + arc));

            // 점프 중 몸통박치기 판정
            CheckDirectHit();

            yield return null;
        }

        // 중력 복구
        rb.gravityScale = originGravity;

        // 자연스러운 전진 낙하
        rb.linearVelocity = new Vector2(
            fallDirectionX * fallForwardSpeed,
            -fallStartSpeed
        );

        // 착지 대기
        while (!IsGrounded())
        {
            CheckDirectHit(); // 낙하 중에도 직접 충돌 가능
            yield return null;
        }

        // 착지 정지
        rb.linearVelocity = Vector2.zero;

        // 착지 충격파
        ExecuteShockwave();

        lastAttackTime = Time.time;

        yield return new WaitForSeconds(0.4f);

        isAttackingOrReady = false;
    }

    private void CheckDirectHit()
    {
        if (hasDirectHit)
        {
            return;
        }

        Collider2D playerCollider = Physics2D.OverlapCircle(
            transform.position,
            directHitRadius,
            playerLayer
        );

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

        Collider2D playerCollider = Physics2D.OverlapBox(
            shockwaveCenter,
            shockwaveBoxSize,
            0f,
            playerLayer
        );

        if (playerCollider != null)
        {
            if (playerCollider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(shockwaveDamage);
            }
        }
    }

    private bool IsGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );
    }

    // 공격 범위 빨강
    // 직접 충돌 범위 시안
    // 충격파 범위 마젠타
    // 착지 체크 노랑
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
}