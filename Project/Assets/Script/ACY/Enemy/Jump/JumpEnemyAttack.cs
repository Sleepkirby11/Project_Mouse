using System.Collections;
using UnityEngine;
/*
 * 현재 충격파는 원형이지만 차후 바닥으로 퍼지는 걸로 수정 예정
 */
public class JumpEnemyAttack : MonoBehaviour
{
    [Header("공격 설정")]
    [SerializeField] private float attackRange = 3f;       // 공격 시전 사거리
    [SerializeField] private float attackCooldown = 3f;    // 공격 간 쿨타임

    [Header("점프 설정")]
    [SerializeField] private float jumpHeight = 4f;        // 포물선 최고 높이
    [SerializeField] private float jumpDuration = 1f;      // 점프 소요 시간
    [SerializeField] private float readyDuration = 0.6f;   // 빨갛게 기 모으는 시간

    [Header("충격파 설정")]
    [SerializeField] private int attackDamage = 1;        // 충격파 대미지
    [SerializeField] private float shockwaveRadius = 2.5f; // 충격파 공격 범위
    [SerializeField] private LayerMask playerLayer;

    private Rigidbody2D rb;
    private JumpEnemyMove moveScript;
    private SpriteRenderer spriteRenderer;
    // private Animator anim;

    private bool isAttackingOrReady = false;
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

        // 사거리 이내 진입 && 쿨타임 만료 시 콤보 스타트
        if (distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            StartCoroutine(JumpAttackComboRoutine(player));
        }
    }

    private IEnumerator JumpAttackComboRoutine(Transform player)
    {
        isAttackingOrReady = true;

        // 준비 자세 (색상 변경) 
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red; // (임시)
        }
        // anim?.SetTrigger("Ready"); 
        yield return new WaitForSeconds(readyDuration);

        // 점프 시작 (색상 원복 및 포물선 이동) ───
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
        // anim?.SetTrigger("Jump");

        Vector2 startPos = transform.position;
        Vector2 targetPos = player.position; // 기 모으기가 끝난 시점의 플레이어 위치 타겟팅

        float elapsed = 0f;
        float originGravity = rb.gravityScale;
        rb.gravityScale = 0f; // 잠시 중력 끄기

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpDuration;

            Vector2 linearPos = Vector2.Lerp(startPos, targetPos, t);
            float arc = jumpHeight * Mathf.Sin(Mathf.PI * t);

            rb.MovePosition(new Vector2(linearPos.x, linearPos.y + arc));
            yield return null;
        }

        // 착지 시 물리 법칙 복구
        rb.gravityScale = originGravity;
        rb.linearVelocity = Vector2.zero;

        // 충격파 (범위 공격_
        ExecuteShockwave();

        // 후딜레이 및 쿨타임 지정 후 회귀
        lastAttackTime = Time.time;
        yield return new WaitForSeconds(0.4f); // 착지 후 딜레이 연출

        isAttackingOrReady = false; // 추적 상태로 회귀
    }

    private void ExecuteShockwave()
    {
        Debug.Log($"{gameObject.name} 충격파 공격");
        // anim?.SetTrigger("Shockwave");

        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, shockwaveRadius, playerLayer);
        if (playerCollider != null)
        {
            if (playerCollider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }

    //공격 범위 빨강, 충격파 범위 마젠타(보라)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
    }
}

