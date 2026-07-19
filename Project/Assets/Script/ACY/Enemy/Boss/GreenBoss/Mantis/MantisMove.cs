using UnityEngine;

public enum MantisIntent { Approach, InMeleeRange, InRangedRange }

public class MantisMove : MonoBehaviour
{
    #region Settings & Variables

    [Header("공격 범위")]
    public float rangedRange = 6f;
    public float meleeRange = 1.8f;

    [Header("이동")]
    public float walkSpeed = 2f;
    public float stopDistance = 1.2f;

    [Header("타겟 설정")]
    [SerializeField] private Transform playerTransform;
    public bool isFacingRight = true;

    [HideInInspector] public MantisIntent intent = MantisIntent.Approach; // 기본 상태
    [HideInInspector] public bool isAttacking;

    private Rigidbody2D rb;
    private Animator anim;
    private MantisAttack attack;

    private float currentDistToPlayer; // Update와 FixedUpdate 공유용
    private bool wasMoving;
    private float flipCooldownTimer;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        attack = GetComponent<MantisAttack>();

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        wasMoving = false;
    }

    private void Update()
    {
        if (playerTransform == null) return;

        if (flipCooldownTimer > 0f) flipCooldownTimer -= Time.deltaTime;
        FlipToTarget();

        currentDistToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        UpdateIntent(currentDistToPlayer);
        UpdateAnim(currentDistToPlayer);
    }

    private void FixedUpdate()
    {
        if (playerTransform == null) return;

        HandleMovement(currentDistToPlayer);
    }

    private void OnDisable()
    {
        wasMoving = false;
        if (anim != null)
        {
            anim.SetBool("IsMoving", false);
        }
    }

    #endregion

    #region AI Intent & Movement Logic

    // 거리에 따라 의도(Intent) 판단 및 상태 전환
    private void UpdateIntent(float dist)
    {
        // 공격 중(코루틴 실행 중)일 때는 절대로 상태를 변경하지 않고 리턴
        if (isAttacking) return;

        bool inMelee = dist <= meleeRange;
        bool inRanged = dist <= rangedRange && !inMelee;

        if (inMelee)
        {
            intent = MantisIntent.InMeleeRange;
        }
        else if (inRanged)
        {
            // 원거리 쿨타임이 끝났다면 원거리 공격 모드 진입
            if (attack != null && attack.IsRangedReady())
            {
                intent = MantisIntent.InRangedRange;
            }
            else
            {
                // 원거리 쿨타임 중이라면 계속 접근(Approach)
                intent = MantisIntent.Approach;
            }
        }
        else
        {
            intent = MantisIntent.Approach;
        }
    }

    // 물리적 이동 속도 결정 및 대입
    private void HandleMovement(float dist)
    {
        if (rb == null) return;

        bool isAnimatorAttacking = false;
        if (anim != null)
        {
            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            isAnimatorAttacking = state.IsName("Slam") || state.IsName("Stab") || state.IsName("AttackCut");
        }

        float xDiff = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 공격 중이거나 공격 범위 진입, 수평 정지 거리 도달 시 속도 0 (머리 위 정지 및 Idle 복귀용)
        if (isAttacking || isAnimatorAttacking || intent == MantisIntent.InMeleeRange || intent == MantisIntent.InRangedRange || xDiff <= stopDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float speed = walkSpeed;

        float directionX = playerTransform.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(directionX * speed, rb.linearVelocity.y);
    }

    #endregion

    #region Animation & Direction Controls

    // 상태에 따른 이동 애니메이션 제어 (Start -> Loop -> End)
    private void UpdateAnim(float dist)
    {
        if (anim == null) return;

        bool isAnimatorAttacking = false;
        if (anim != null)
        {
            AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
            isAnimatorAttacking = state.IsName("Slam") || state.IsName("Stab") || state.IsName("AttackCut") || state.IsName("Hurt") || state.IsName("Death");
        }

        float xDiff = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 움직여야 하는 조건 (X축 수평 거리가 정지 거리보다 클 때만)
        bool isMovingNow = !isAttacking && !isAnimatorAttacking && intent == MantisIntent.Approach && xDiff > stopDistance;

        if (isMovingNow && !wasMoving)
        {
            anim.SetBool("IsMoving", true);
            anim.SetTrigger("MoveStart");
            wasMoving = true;
        }
        else if (!isMovingNow && wasMoving)
        {
            anim.SetBool("IsMoving", false);
            anim.SetTrigger("MoveEnd");
            wasMoving = false;
        }
    }

    // 타겟을 바라보도록 플립 처리
    private void FlipToTarget()
    {
        if (playerTransform == null || flipCooldownTimer > 0f) return;

        float xDiff = Mathf.Abs(playerTransform.position.x - transform.position.x);

        // 머리 위 등 너무 밀착했을 때의 좌우 뜀을 방지하기 위해 임계값을 0.4로 상향
        if (xDiff > 0.4f)
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

        // 회전 후 0.3초 동안 재회전 방지 쿨다운 적용 (떨림 방지)
        flipCooldownTimer = 0.3f;
    }

    #endregion

    #region Utility Methods

    public void ResetRangedZone() {}

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

    #endregion
}
