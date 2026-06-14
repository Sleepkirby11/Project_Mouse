using UnityEngine;

public enum PlantIntent { Approach, InMeleeRange, InRangedRange } // Idle 상태 제거

public class KillerPlantMove : MonoBehaviour
{
    [Header("공격 범위")]
    public float rangedRange = 6f;
    public float meleeRange = 1.8f;

    [Header("이동")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float stopDistance = 1.2f;

    [Header("Run 설정")]
    public float runChance = 0.3f;
    public float runDuration = 2f;
    public float runCooldown = 5f;

    [Header("원거리 공격 발동 확률")]
    [Range(0f, 1f)] public float rangedAttackChance = 0.4f;

    [Header("타겟 설정")]
    [SerializeField] private Transform playerTransform;
    public bool isFacingRight = true;

    [HideInInspector] public PlantIntent intent = PlantIntent.Approach; // 기본 상태를 '추적'으로 고정
    [HideInInspector] public bool isAttacking;

    private Rigidbody2D rb;
    private Animator anim;

    private bool isRunning;
    private float runTimer;
    private float runCooldownTimer;
    private bool wasInRangedZone;
    private float currentDistToPlayer; // Update와 FixedUpdate 공유용

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        runCooldownTimer = runCooldown;
    }

    void Update()
    {
        if (playerTransform == null) return;

        FlipToTarget();

        currentDistToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        UpdateRunTimer(Time.deltaTime);
        UpdateIntent(currentDistToPlayer);
        UpdateAnim();
    }

    void FixedUpdate()
    {
        if (playerTransform == null) return;

        HandleMovement(currentDistToPlayer);
    }

    // 💡 무한 추적에 맞게 상태 전환 로직 대폭 축소 및 수정
    void UpdateIntent(float dist)
    {
        // 🔥 [핵심 수정 1] 공격 중(코루틴 실행 중)일 때는 절대로 상태를 변경하지 않고 리턴합니다.
        // 이 자물쇠가 있어야 공격 도중에 걷는 상태로 바뀌지 않습니다.
        if (isAttacking) return;

        bool inMelee = dist <= meleeRange;
        bool inRanged = dist <= rangedRange && !inMelee;

        bool justEnteredRanged = inRanged && !wasInRangedZone;
        wasInRangedZone = inRanged;

        switch (intent)
        {
            case PlantIntent.Approach:
                if (inMelee)
                    intent = PlantIntent.InMeleeRange;
                else if (inRanged && justEnteredRanged)
                    intent = (Random.value < rangedAttackChance) ? PlantIntent.InRangedRange : PlantIntent.Approach;
                break;

            case PlantIntent.InMeleeRange:
                // 공격 중이 아닐 때(isAttacking이 false일 때)만 거리를 체크해서 탈출하도록 둠
                if (!inMelee) intent = PlantIntent.Approach;
                break;

            case PlantIntent.InRangedRange:
                // 원거리는 KillerPlantAttack 코루틴 끝에서 직접 Approach로 바꿔줄 때까지 이 상태를 유지합니다.
                break;
        }
    }

    void UpdateRunTimer(float deltaTime)
    {
        if (isRunning)
        {
            runTimer -= deltaTime;
            if (runTimer <= 0f) isRunning = false;
        }
        else
        {
            runCooldownTimer -= deltaTime;
            if (runCooldownTimer <= 0f)
            {
                if (Random.value < runChance)
                {
                    isRunning = true;
                    runTimer = runDuration;
                }
                runCooldownTimer = runCooldown;
            }
        }
    }

    void HandleMovement(float dist)
    {
        // 🔥 [핵심 수정 2] intent 상태와 상관없이 'isAttacking' 플래그가 켜져 있다면 
        // 묻지도 따지지도 않고 X축 속도를 0으로 만들어 물리 이동을 완벽히 차단합니다.
        if (isAttacking || intent == PlantIntent.InMeleeRange || intent == PlantIntent.InRangedRange || dist <= stopDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float speed = isRunning ? runSpeed : walkSpeed;

        float directionX = playerTransform.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(directionX * speed, rb.linearVelocity.y);
    }

    void UpdateAnim()
    {
        // 🔥 [핵심 수정 3] 공격 중일 때는 Walk/Run 애니메이션을 강제로 끕니다.
        // 공격 애니메이션과 걷기 애니메이션이 꼬여서 스케이트 타는 현상을 막아줍니다.
        if (isAttacking)
        {
            anim.SetBool("Walk", false);
            anim.SetBool("Run", false);
            return;
        }

        bool isApproach = intent == PlantIntent.Approach;
        anim.SetBool("Walk", isApproach && !isRunning);
        anim.SetBool("Run", isApproach && isRunning);
    }

    private void FlipToTarget()
    {
        if (playerTransform == null) return;

        float xDiff = Mathf.Abs(playerTransform.position.x - transform.position.x);

        if (xDiff > 0.15f)
        {
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
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // 💡 기즈모에서도 초록색(detectRange) 선을 삭제했습니다.
    private void OnDrawGizmosSelected()
    {
        // 1. 원거리 공격 범위 (노란색 원)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangedRange);

        // 2. 근접 공격 범위 (빨간색 원)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        // 3. 정지 거리 (하늘색 원)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }

    public void ResetRangedZone() => wasInRangedZone = false;
}