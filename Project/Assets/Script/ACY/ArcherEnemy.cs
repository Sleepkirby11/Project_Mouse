using UnityEngine;
/*
 행동패턴: 자리에 고정 
공격패턴: 일정 간격으로 플레이어에게 화살을 발사
특징[백스텝]: 플레이어가 근처로 접근 시 뒤로 이동하며 거리를 벌림
 */
public class ArcherEnemy : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private float detectRange = 5f; // 플레이어 감지 범위
    [SerializeField] private float escapeRange = 2f; // 백스텝 트리거 범위
    [SerializeField] private LayerMask playerLayer;  // 플레이어 레이어

    [Header("공격 설정")]
    [SerializeField] private GameObject arrowPrefab; // 화살 프리팹
    [SerializeField] private Transform firePoint; // 화살 발사 위치
    [SerializeField] private float attackCooldown = 2f; // 공격 간격
    [SerializeField] private float arrowHeight = 3f; // 화살 포물선 높이
    [SerializeField] private float arrowDuration = 1.2f; // 화살 이동 시간

    [Header("백스텝 설정")]
    [SerializeField] private float backstepSpeed = 8f; // 백스텝 이동 속도
    [SerializeField] private float backstepDuration = 0.3f; // 백스텝 지속 시간
    [SerializeField] private float backstepCooldown = 2f; // 백스텝 재사용 대기 시간

    private float backstepCooldownTimer = 0f; // 백스텝 쿨타임 타이머
    private bool isBackstepping = false; // 백스텝 여부

    private Rigidbody2D rb; 
    private Transform player; 
    private float nextAttackTime; // 다음 공격 가능 시간

    private enum State { Idle, Attack, Backstep } // 상태 열거
    private State currentState = State.Idle; // 초기 상태

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); //리지드바디 기초 설정
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        if (backstepCooldownTimer > 0f) // 백스텝 쿨타임 감소
        {
            backstepCooldownTimer -= Time.deltaTime;
        }

        CheckDetection();

        if (currentState == State.Attack) // 공격 상태면 공격 실행
        {
            UpdateAttack();
        }
    }

    void FixedUpdate()
    {
        // 물리 이동은 FixedUpdate에서
        if (currentState == State.Backstep) 
        {
            BackstepMove();
        }
        else if (!isBackstepping)
        {
            rb.linearVelocity = Vector2.zero; // 물리적 정지
        }
    }

    private void CheckDetection()
    {
        // 플레이어 유효성 및 거리 체크 (sqrMagnitude로 최적화)
        if (player != null)
        {
            float sqrDist = (player.position - transform.position).sqrMagnitude; // 제곱 거리 계산

            if (sqrDist > detectRange * detectRange) // 감지 범위를 벗어나면 플레이어 초기화
            {
                player = null; // 플레이어가 멀어지면 초기화
                currentState = State.Idle; // 상태 초기화
                return; 
            }

            // 백스텝 트리거 체크
            if (!isBackstepping && backstepCooldownTimer <= 0f && sqrDist < escapeRange * escapeRange) 
            {
                StartBackstep();
                return;
            }
        }
        else
        {
            // 플레이어 탐색 
            Collider2D col = Physics2D.OverlapCircle(transform.position, detectRange, playerLayer); //player가 없을때만 탐색하여 최적화
            if (col != null)
            {
                player = col.transform;
            }
        }

        if (player != null && !isBackstepping) // 플레이어가 감지되고 백스텝 중이 아니면 공격 상태로 전환
        {
            currentState = State.Attack;
        }
    }

    private void StartBackstep() // 백스텝 시작
    {
        isBackstepping = true; 
        currentState = State.Backstep;

        Invoke(nameof(StopBackstep), backstepDuration); // 백스텝 지속 시간 후 정지
    }

    private void BackstepMove() // 백스텝 처리
    {
        if (player == null)
        {
            return;
        }

        Vector2 dir = ((Vector2)transform.position - (Vector2)player.position).normalized; 
        rb.linearVelocity = dir * backstepSpeed; 
    }

    private void StopBackstep()
    {
        isBackstepping = false;
        backstepCooldownTimer = backstepCooldown;
        rb.linearVelocity = Vector2.zero;
        currentState = State.Attack;
    }

    private void UpdateAttack()
    {
        if (Time.time >= nextAttackTime && player != null)
        {
            FireArrow();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    private void FireArrow()
    {
        // 차후 풀링으로 변경
        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (arrowObj.TryGetComponent<Arrow>(out var arrow))
        {
            arrow.Initialize(firePoint.position, player.position, arrowHeight, arrowDuration);
        }
    }
    private void OnDrawGizmosSelected() // 디버그용 코드
    {
        // 감지 범위 — 노란색
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 백스텝 범위 — 빨간색
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, escapeRange);
    }
}
