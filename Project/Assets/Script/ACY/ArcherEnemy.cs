using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

/*
 행동패턴: 좌우를 배회, 플레이어 감지 시 위치 고정 
공격패턴: 일정 간격으로 플레이어에게 화살을 발사
특징[백스텝]: 플레이어가 근처로 접근 시 뒤로 이동하며 거리를 벌림
 */
public class ArcherEnemy : MonoBehaviour
{
    [Header("움직임 설정")]
    [SerializeField] private float patrolRange = 5f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float detectRange = 10f;
    [SerializeField] private float escapeRange = 3f; // 백스텝 트리거 거리
    [SerializeField] private float attackCooldown = 2f;

    [Header("공격 설정")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float arrowHeight = 3f; // 포물선 높이
    [SerializeField] private float arrowDuration = 1.2f; // 날아가는 시간

    private Rigidbody2D rb;
    private Transform player;
    private Vector2 originPos;
    private float nextAttackTime;
    private bool isPlayerDetected;

    private enum State { Patrol, Attack, Backstep }
    private State _currentState = State.Patrol;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originPos = transform.position;
        // 성능 최적화: Rigidbody2D 설정 강제
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        // 플레이어 감지 (물리 레이어 기반)
        CheckDetection();

        // 상태 결정 및 실행
        switch (_currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Attack:
                UpdateAttack();
                break;
            case State.Backstep:
                UpdateBackstep();
                break;
        }
    }

    private void CheckDetection()
    {
        if (player == null)
        {
            Collider2D col = Physics2D.OverlapCircle(transform.position, detectRange, playerLayer);
            if (col != null)
            {
                player = col.transform;
            }
            return;
        }

        float sqrDist = (player.position - transform.position).sqrMagnitude;

        if (sqrDist < escapeRange * escapeRange) _currentState = State.Backstep;
        else if (sqrDist < detectRange * detectRange) _currentState = State.Attack;
        else _currentState = State.Patrol;
    }

    private void UpdatePatrol()
    {
        float xOffset = Mathf.Sin(Time.time * patrolSpeed) * patrolRange;
       rb.MovePosition(new Vector2(originPos.x + xOffset, rb.position.y));
    }

    private void UpdateAttack()
    {
        rb.linearVelocity = Vector2.zero; // 위치 고정

        if (Time.time >=nextAttackTime)
        {
            FireArrow();
           nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void UpdateBackstep()
    {
        // 플레이어 반대 방향으로 이동
        Vector2 dir = (transform.position - player.position).normalized;
        rb.MovePosition(rb.position + dir * (patrolSpeed * 1.5f * Time.deltaTime));
    }

    private void FireArrow()
    {
        if (player == null) return;

        // 화살 생성
        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        //Arrow arrow = arrowObj.GetComponent<Arrow>();

        // 플레이어의 현재 위치로 발사
        //arrow.Initialize(firePoint.position, player.position, arrowHeight, arrowDuration);
    }
}
