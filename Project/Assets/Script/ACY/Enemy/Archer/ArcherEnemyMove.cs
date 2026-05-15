using UnityEngine;
/*
행동패턴: 자리에 고정 
플레이어가 근처로 접근 시 뒤로 이동하며 거리를 벌림
 */
public class ArcherEnemyMove : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private float detectRange = 5f; // 플레이어 감지 범위
    [SerializeField] private float escapeRange = 2f; // 백스텝 트리거 범위
    [SerializeField] private LayerMask playerLayer;  // 플레이어 레이어

    [Header("백스텝 설정")]
    [SerializeField] private float backstepSpeed = 8f; // 백스텝 이동 속도
    [SerializeField] private float backstepDuration = 0.3f; // 백스텝 지속 시간
    [SerializeField] private float backstepCooldown = 2f; // 백스텝 재사용 대기 시간

    private Rigidbody2D rb; 
    private Transform player; 
    private float backstepCooldownTimer = 0f; // 백스텝 쿨타임 타이머
    private float backstepEndTimer;
    private bool isBackstepping = false; // 백스텝 여부

    // 공격 컴포넌트에서 참조
    public Transform TargetPlayer => player;
    public bool IsBackstepping => isBackstepping;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); //리지드바디 기초 설정
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        if (backstepCooldownTimer > 0f)
        {
            backstepCooldownTimer -= Time.deltaTime;
        }

        // 백스텝 타이머 제어 (Invoke 대체)
        if (isBackstepping)
        {
            backstepEndTimer -= Time.deltaTime;
            if (backstepEndTimer <= 0f)
            {
                isBackstepping = false;
            }
        }

        CheckDetection();
    }

    void FixedUpdate()
    {
        // 백스텝 상태일 때만 이동, 나머지는 정지
        if (isBackstepping && player != null)
        {
            float moveDirX = transform.position.x > player.position.x ? 1f : -1f;
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

    private void CheckDetection()
    {
        // 플레이어 유효성 및 거리 체크 (sqrMagnitude로 최적화)
        if (player != null)
        {
            float sqrDist = (player.position - transform.position).sqrMagnitude; // 제곱 거리 계산

            if (sqrDist > detectRange * detectRange) // 감지 범위를 벗어나면 플레이어 초기화
            {
                player = null; // 플레이어가 멀어지면 초기화
                return; 
            }

            // 백스텝 트리거 체크
            if (!isBackstepping && backstepCooldownTimer <= 0f && sqrDist < escapeRange * escapeRange) 
            {
                isBackstepping = true;
                backstepEndTimer = backstepDuration;
                backstepCooldownTimer = backstepCooldown;
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
