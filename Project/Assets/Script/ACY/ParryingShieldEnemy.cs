using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
/*
행동패턴 1. 좌우배회 2. 플레이어를 인식하면 플레이어 방향으로 이동 3. 인식 후 일정 시간마다 패링 시도(패링 시도중엔 멈춤)
공격패턴: 플레이어와 접촉 시 플레이어에게 대미지를 주고 살짝 밀쳐냄(공중으로 뜨는 효과)
특징[패링]: 패링 시도 중 플레이어로부터 공격받을 시 대미지를 무효화하고 플레이어의 위치로 빠른 돌진.
히트 성공 시 플레이어는 공중에 떠오르며(일반 공격보다 더 높게) 잠시동안 조작불능 상태
 */

/// <summary>
/// 패링 방패 적 AI
///
/// 행동패턴
///   1. Patrol  : 좌우 배회
///   2. Tracking: 플레이어 감지 후 추적
///   3. Parrying: 일정 주기마다 멈추고 패링 판정 활성화
///
/// 공격패턴
///   - 일반 접촉 : 플레이어에게 데미지 + 약한 넉백(공중으로 살짝)
///   - 카운터 히트: 패링 중 피격 시 돌진 → 높은 공중부양 + 스턴
///
/// 플레이어 연동
///   IHittable  : 일반 접촉 데미지·넉백 전달
///   IStunnable : 카운터 히트 시 스턴 전달
///   OnHitByPlayer() : 플레이어 공격 스크립트에서 직접 호출
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ParryingShieldEnemy : MonoBehaviour
{
    // ───────────────────────────── Inspector ─────────────────────────────

    [Header("이동")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float patrolDistance = 4f;   // 배회 반경

    [Header("감지")]
    [SerializeField] private float detectRange = 6f;
    [SerializeField] private LayerMask playerLayer;

    [Header("패링")]
    [SerializeField] private float parryDuration = 1.2f;  // 패링 판정 유지 시간
    [SerializeField] private float parryInterval = 4f;    // 패링 시도 주기

    [Header("일반 접촉 공격")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float contactKnockbackX = 6f;   // 옆으로 밀려나는 힘
    [SerializeField] private float contactKnockbackY = 5f;   // 약하게 뜨는 힘
    [SerializeField] private float contactCooldown = 0.8f; // 재접촉 무적 시간

    [Header("카운터 공격")]
    [SerializeField] private float counterDashForce = 22f;  // 돌진 힘
    [SerializeField] private float counterDashTime = 0.4f; // 돌진 유지 시간
    [SerializeField] private float counterLaunchY = 18f;  // 카운터 히트 공중부양 힘
    [SerializeField] private float counterStunTime = 1.5f; // 스턴 지속 시간

    // ───────────────────────────── 내부 상태 ─────────────────────────────

    private enum State { Patrol, Tracking, Parrying, Countering }

    private Rigidbody2D rb;
    private Transform target;
    private State state = State.Patrol;

    // Patrol
    private Vector2 patrolOrigin;
    private float patrolDir = 1f;

    // 접촉 공격 쿨다운
    private float lastContactTime = -99f;

    // 캐싱
    private WaitForSeconds parryWait;
    private WaitForSeconds parryIntervalWait;
    private ContactFilter2D contactFilter;
    private readonly List<Collider2D> overlapBuffer = new List<Collider2D>(1);

    // ───────────────────────────── Unity ─────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        parryWait = new WaitForSeconds(parryDuration);
        parryIntervalWait = new WaitForSeconds(parryInterval);

        contactFilter.useLayerMask = true;
        contactFilter.layerMask = playerLayer;

        patrolOrigin = transform.position;
    }

    private void Start() => StartCoroutine(ParryRoutine());

    private void Update()
    {
        if (state == State.Parrying || state == State.Countering) return;

        DetectPlayer();

        if (state == State.Tracking && target != null)
            MoveToward(target.position);
        else
            Patrol();
    }

    // ───────────────────────────── 감지 ──────────────────────────────────

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
            }
        }
        else
        {
            if (Physics2D.OverlapCircle(transform.position, detectRange, contactFilter, overlapBuffer) > 0)
            {
                target = overlapBuffer[0].transform;
                state = State.Tracking;
            }
        }
    }

    // ───────────────────────────── 이동 ──────────────────────────────────

    private void MoveToward(Vector3 destination)
    {
        float dirX = destination.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dirX * moveSpeed, rb.linearVelocity.y);
    }

    private void Patrol()
    {
        // 배회 반경 도달 시 방향 전환
        float distFromOrigin = transform.position.x - patrolOrigin.x;
        if (distFromOrigin > patrolDistance) patrolDir = -1f;
        if (distFromOrigin < -patrolDistance) patrolDir = 1f;

        rb.linearVelocity = new Vector2(patrolDir * moveSpeed * 0.6f, rb.linearVelocity.y);
    }

    // ───────────────────────────── 패링 루틴 ─────────────────────────────

    private IEnumerator ParryRoutine()
    {
        while (true)
        {
            yield return parryIntervalWait;

            // 플레이어를 추적 중일 때만 패링 시도
            if (target != null)
            {
                state = State.Parrying;
                rb.linearVelocity = Vector2.zero;
                yield return parryWait;

                // 카운터가 발동되지 않았다면 추적으로 복귀
                if (state == State.Parrying)
                    state = State.Tracking;
            }
        }
    }

    // ───────────────────────────── 외부 호출 API ─────────────────────────

    /// <summary>
    /// 플레이어 공격 스크립트에서 이 적을 타격할 때 호출하세요.
    /// 패링 상태면 카운터를 발동하고, 아니면 false를 반환합니다.
    /// </summary>
    /// <returns>패링 성공(true) / 일반 피격(false)</returns>
    public bool OnHitByPlayer(Transform attacker)
    {
        if (state != State.Parrying) return false;

        StartCoroutine(CounterRoutine(attacker));
        return true;
    }

    // ───────────────────────────── 카운터 ────────────────────────────────

    private IEnumerator CounterRoutine(Transform attacker)
    {
        state = State.Countering;

        // 플레이어 방향으로 즉각 돌진
        Vector2 dashDir = ((Vector2)(attacker.position - transform.position)).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dashDir * counterDashForce, ForceMode2D.Impulse);

        yield return new WaitForSeconds(counterDashTime);

        rb.linearVelocity = Vector2.zero;
        state = State.Tracking;
    }

    // ───────────────────────────── 충돌 처리 ─────────────────────────────

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        if (state == State.Countering)
        {
            // 카운터 히트: 높이 뜨게 + 스턴
            ApplyCounterHit(collision.gameObject);
        }
        else if (Time.time - lastContactTime > contactCooldown)
        {
            // 일반 접촉 공격
            ApplyContactHit(collision.gameObject);
        }
    }

    private void ApplyContactHit(GameObject player)
    {
        lastContactTime = Time.time;

        // 플레이어가 이 적의 어느 방향에 있는지 계산
        float dirX = player.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 knockback = new Vector2(dirX * contactKnockbackX, contactKnockbackY);

        // IHittable 인터페이스로 전달
       // if (player.TryGetComponent(out IHittable hittable))
           // hittable.TakeHit(contactDamage, knockback);
    }

    private void ApplyCounterHit(GameObject player)
    {
        float dirX = player.transform.position.x > transform.position.x ? 1f : -1f;
        Vector2 launchForce = new Vector2(dirX * contactKnockbackX, counterLaunchY);

        // IHittable + IStunnable 둘 다 전달
      //  if (player.TryGetComponent(out IHittable hittable))
         //   hittable.TakeHit(contactDamage, launchForce);

      //  if (player.TryGetComponent(out IStunnable stunnable))
       //     stunnable.ApplyStun(counterStunTime, launchForce);
    }

    // ───────────────────────────── Gizmos ────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 감지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 배회 범위
        Gizmos.color = Color.cyan;
        Vector3 origin = Application.isPlaying ? (Vector3)patrolOrigin : transform.position;
        Gizmos.DrawLine(origin + Vector3.left * patrolDistance,
                        origin + Vector3.right * patrolDistance);
    }
#endif
}