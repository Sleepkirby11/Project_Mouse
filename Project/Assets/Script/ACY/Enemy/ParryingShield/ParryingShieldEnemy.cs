using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
/*
행동패턴 
1. 좌우배회 (Patrol)
2. 플레이어를 인식하면 플레이어 방향으로 이동 (Tracking)
3. 인식 후 일정 시간마다 패링 시도 (Parrying) 패링중엔 멈춤

공격패턴
1. 일반 접촉 : 플레이어에게 데미지 + 약한 넉백(공중으로 살짝)
2. 카운터 히트(패링): 패링 중 피격 시 돌진 → 높은 공중부양 + 스턴

IHittable  : 일반 접촉 데미지·넉백 전달
IStunnable : 카운터 히트 시 스턴 전달
OnHitByPlayer() : 플레이어 공격 스크립트에서 직접 호출 예정
 */

[RequireComponent(typeof(Rigidbody2D))]
public class ParryingShieldEnemy : MonoBehaviour, IHitReaction
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolDistance = 4f;   // 배회 반경

    [Header("감지")]
    [SerializeField] private float detectRange = 6f;
    [SerializeField] private LayerMask playerLayer;

    [Header("패링")]
    [SerializeField] private float parryDuration = 1.2f;  // 패링 판정 유지 시간
    [SerializeField] private float parryInterval = 1f;    // 패링 시도 주기

    [Header("일반 접촉 공격")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float contactKnockbackX = 6f;   // 옆으로 밀려나는 힘
    [SerializeField] private float contactKnockbackY = 5f;   // 약하게 뜨는 힘
    [SerializeField] private float contactCooldown = 0.8f; // 재접촉 무적 시간

    [Header("카운터 공격")]
    [SerializeField] private float counterDashForce = 22f;  // 돌진 힘
    [SerializeField] private float counterDashTime = 0.6f; // 돌진 유지 시간
    [SerializeField] private float counterLaunchY = 18f;  // 카운터 히트 공중부양 힘
    [SerializeField] private float counterStunTime = 1.5f; // 스턴 지속 시간
    [SerializeField] private float counterRecoverTime = 0.3f;

    [Header("타겟 설정")]
    [SerializeField] private Transform playerTransform;
    public bool isFacingRight = true;

    private enum State { Patrol, Tracking, Parrying, Countering,  Recovering } 

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    private Color originalColor;
    private Transform target;
    private State state = State.Patrol; // 초기 상태는 배회

    private Vector2 patrolOrigin; // 배회 시작 위치
    private float patrolDir = 1f; // 배회 방향 (1: 오른쪽, -1: 왼쪽)

    // 접촉 공격 쿨다운
    private float lastContactTime = -99f;

    // 캐싱
    private WaitForSeconds parryWait;
    private WaitForSeconds parryIntervalWait;
    private ContactFilter2D contactFilter;
    private readonly List<Collider2D> overlapBuffer = new List<Collider2D>(1);

    private void Awake()
    {
        //리지드바디 설정
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // 패링 타이머 캐싱
        parryWait = new WaitForSeconds(parryDuration);
        parryIntervalWait = new WaitForSeconds(parryInterval);

        // 접촉 감지 필터 설정
        contactFilter.useLayerMask = true;
        contactFilter.layerMask = playerLayer;

        // 배회 원점 설정
        patrolOrigin = transform.position;
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
        StartCoroutine(ParryRoutine()); // 패링 루틴 시작
    }

    private void Update()
    {
        if (state != State.Patrol)
        {
            FlipToTarget();
        }

        UpdateAnimator();

        if (state == State.Parrying || state == State.Countering || state == State.Recovering)
        {
            return;
        }

        DetectPlayer();

        if (state == State.Tracking && target != null)
        {
            MoveToward(target.position);
        }
        else
        {
            Patrol();
        }
    }

    private void DetectPlayer()
    {
        if (target != null)
        {
            // 범위 이탈 시 추적 해제 → 배회 복귀
            float sqrDist = (target.position - transform.position).sqrMagnitude;
            if (sqrDist > detectRange * detectRange)
            {
                target = null;
                state = State.Patrol;
                patrolOrigin = transform.position; // 현재 위치를 배회 기준점으로 설정
            }
        }
        else
        {
            if (Physics2D.OverlapCircle(transform.position, detectRange, contactFilter, overlapBuffer) > 0) // 플레이어 감지 시 추적 시작
            {
                target = overlapBuffer[0].transform; // 가장 가까운 콜라이더의 위치를 타겟으로 설정
                state = State.Tracking; // 추적 상태로 전환
            }
        }
    }

    private void MoveToward(Vector3 destination) // 플레이어 방향으로 이동
    {
        float dirX = destination.x > transform.position.x ? 1f : -1f; // 이동 방향 계산
        rb.linearVelocity = new Vector2(dirX * moveSpeed, rb.linearVelocity.y);
    }

    private void Patrol()
    {
        // 배회 반경 도달 시 방향 전환
        float distFromOrigin = transform.position.x - patrolOrigin.x;
        if (distFromOrigin > patrolDistance && patrolDir == 1f)
        {
            patrolDir = -1f;
            Flip();
        }
        if (distFromOrigin < -patrolDistance && patrolDir == -1f)
        {
            patrolDir = 1f;
            Flip();
        }

        rb.linearVelocity = new Vector2(patrolDir * moveSpeed * 0.6f, rb.linearVelocity.y);
    }

    private IEnumerator ParryRoutine() // 일정 간격으로 패링 시도
    {
        while (true)
        {
            yield return parryIntervalWait;

            // 플레이어를 추적 중일 때만 패링 시도
            if (target != null)
            {
                Debug.Log("패링 시도");
                state = State.Parrying;
                rb.linearVelocity = Vector2.zero;
                yield return parryWait;

                // 카운터가 발동되지 않았다면 추적으로 복귀
                if (state == State.Parrying)
                {
                    state = State.Tracking;
                }
            }
        }
    }

    public bool OnBeforeTakeDamage(EnemyStatus enemyStatus, int damage)
    {
        return OnHitByPlayer(); // 패링 성공 시 데미지 무효화
    }

    public void OnAfterTakeDamage(EnemyStatus enemyStatus, int damage)
    {
        // 방패병은 데미지 받은 뒤 반응 없음
    }

    // 패링 상태면 카운터를 발동하고, 아니면 false 반환
    public bool OnHitByPlayer()
    {
        if (state != State.Parrying)
        {
            return false;
        }
        if (target == null)
        {
            return false;
        }
        StartCoroutine(CounterRoutine());
        return true;
    }

    private IEnumerator CounterRoutine() //카운터
    {
        state = State.Countering;
        if (anim != null)
        {
            anim.SetBool("IsParrying", false); // 패링 해제
            anim.SetTrigger("Counter");
        }
        // 플레이어 방향으로 즉각 돌진
        Vector2 dashDir = ((Vector2)(target.position - transform.position)).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dashDir * counterDashForce, ForceMode2D.Impulse);

        yield return new WaitForSeconds(counterDashTime);

        rb.linearVelocity = Vector2.zero;
        if (state == State.Countering)
        {
            state = State.Tracking;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision) //충돌처리
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        if (state == State.Countering)
        {
            rb.linearVelocity = Vector2.zero; // 충돌 시 제동력 부여

            ApplyCounterHit(collision.gameObject);
            StartCoroutine(CounterRecoverRoutine());
        }
        else if (Time.time - lastContactTime > contactCooldown)
        {
            ApplyContactHit(collision.gameObject);
        }
    }
    private IEnumerator CounterRecoverRoutine()
    {
        state = State.Recovering;

        yield return new WaitForSeconds(counterRecoverTime);

        state = State.Tracking;
    }
    private void ApplyContactHit(GameObject player)
    {
        lastContactTime = Time.time;

        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }
        float dirX = player.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 knockback = new Vector2(dirX * contactKnockbackX, contactKnockbackY);

        // 대미지 감소 
        if (player.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(contactDamage);
        }

        // 넉백 
        if (player.TryGetComponent(out IHittable hittable))
        {
            hittable.TakeHit(knockback);
        }
    }
 
    private void ApplyCounterHit(GameObject player) //카운터 성공 시 호출
    {
        float dirX = player.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 launchForce = new Vector2(dirX * contactKnockbackX, counterLaunchY);

        // 카운터 공격 성공 시 대미지 + 넉백 + 스턴
        if (player.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(contactDamage);
        }

        if (player.TryGetComponent(out IHittable hittable))
        {
            hittable.TakeHit(launchForce);
        }

        if (player.TryGetComponent(out IStunnable stunnable))
        {
            stunnable.ApplyStun(counterStunTime);
        }
    }
    private void UpdateAnimator()
    {
        if (anim == null)
        {
            return;
        }
        anim.SetBool("IsParrying", state == State.Parrying);
        anim.SetBool("IsMoving", state == State.Tracking || state == State.Patrol);
        anim.SetBool("IsFalling", rb.linearVelocity.y < -0.1f);
    }
    private void FlipToTarget()
    {
        if (playerTransform == null)
        {
            return;
        }

        float direction = playerTransform.position.x - transform.position.x;

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
    private void OnDrawGizmosSelected()
    {
        // 감지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 배회 범위
        Gizmos.color = Color.cyan;
        Vector3 origin = Application.isPlaying ? (Vector3)patrolOrigin : transform.position;
        Gizmos.DrawLine(origin + Vector3.left * patrolDistance, origin + Vector3.right * patrolDistance);
    }
}