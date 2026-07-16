using System.Collections;
using UnityEngine;

/*
 * 행동패턴: 좌우를 배회 (인스펙터에서 수치 조절 가능)
 * 플레이어 감지 시 플레이어를 추적
 */
public class BasicEnemyMove : MonoBehaviour
{
    #region Types

    public enum EnemyState { Patrol, Chase } // 배회상태, 추적상태

    #endregion

    #region Settings & Variables

    [Header("현재 상태")]
    public EnemyState currentState = EnemyState.Patrol; // 초기 상태는 배회

    public bool isFacingRight = true; // 초기 방향 설정 (오른쪽)

    [Header("움직임 설정")]
    public float moveSpeed = 1f; // 속도
    public float patrolRange = 5f; // 좌우 배회 반경

    [Header("추적 설정")]
    public float stopDistance = 0.8f;   // 플레이어와 최소 유지 거리

    [Header("감지 설정")]
    public float detectionRadius = 6f; // 감지 범위
    public LayerMask targetLayer;      // 타겟 레이어

    private Transform myTransform;
    private Transform targetTransform;
    private Vector3 startPosition;
    private Vector3 patrolTarget;
    private float detectionRadiusSqr;

    private Animator animator;
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");   
    private static readonly int IsChasing = Animator.StringToHash("IsChasing"); 

    private WaitForSeconds scanIntervalWFS;
    private EnemyStatus enemyStatus;
    private Rigidbody2D rb;
    private BasicEnemyAttack enemyAttack;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        myTransform = transform;

        detectionRadiusSqr = detectionRadius * detectionRadius;

        scanIntervalWFS = new WaitForSeconds(0.2f); // 0.2초 주기 스캔
        animator = GetComponentInChildren<Animator>();
        enemyStatus = GetComponent<EnemyStatus>();
        rb = GetComponent<Rigidbody2D>();
        enemyAttack = GetComponent<BasicEnemyAttack>();
    }

    private void Start()
    {
        startPosition = myTransform.position;
        // 최초 상태 설정 (애니메이터 파라미터 연동)
        SetState(currentState);
        // 최초 배회 목표 지점 설정
        UpdatePatrolTarget();
        StartCoroutine(EnvironmentScanRoutine()); // 감지 코루틴 시작
    }

    private void Update()
    {
        if (enemyStatus != null && enemyStatus.isStunned)
        {
            if (animator != null)
            {
                animator.SetBool(IsMoving, false);
                animator.SetBool(IsChasing, false);
            }
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        // 공격 중일 때는 움직이지 않고 멈춤
        if (enemyAttack != null && enemyAttack.IsAttacking)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            if (animator != null)
            {
                animator.SetBool(IsMoving, false);
                animator.SetBool(IsChasing, false);
            }
            return;
        }

        switch (currentState) // 상태에 따른 행동 분기
        {
            case EnemyState.Patrol: // 배회 상태에서는 좌우로 이동
                PatrolMovement();
                break;
            case EnemyState.Chase: // 추적 상태에서는 플레이어를 향해 이동
                ChaseMovement();
                break;
        }

        // 실제 이동 속도에 따라 애니메이션 상태를 업데이트 (공중 플랫폼 아래 멈췄을 때 Idle 전환용)
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // X축 속도가 어느 정도 있을 때만 걷기/달리기 애니메이션 재생
        bool isMovingReal = rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.1f;

        if (currentState == EnemyState.Patrol)
        {
            animator.SetBool(IsMoving, isMovingReal);
            animator.SetBool(IsChasing, false);
        }
        else if (currentState == EnemyState.Chase)
        {
            animator.SetBool(IsMoving, false);
            animator.SetBool(IsChasing, isMovingReal);
        }
    }

    #endregion

    #region State & Scan Logic

    private void SetState(EnemyState newState)
    {
        currentState = newState;
        UpdateAnimator();
    }

    private IEnumerator EnvironmentScanRoutine()
    {
        while (true)
        {
            yield return scanIntervalWFS;

            if (targetTransform == null)
            {
                // 플레이어가 타겟팅되지 않은 상태일 때만 주변 반경 레이어 검사
                Collider2D hit = Physics2D.OverlapCircle(myTransform.position, detectionRadius, targetLayer); 
                if (hit != null) // 감지되면 추적
                {
                    PlayerStatus playerStatus = hit.GetComponentInParent<PlayerStatus>();
                    if (playerStatus != null && playerStatus.HP > 0)
                    {
                        targetTransform = hit.transform;
                        SetState(EnemyState.Chase);
                    }
                }
            }
            else
            {
                PlayerStatus playerStatus = targetTransform.GetComponentInParent<PlayerStatus>();
                float sqrDistance = (targetTransform.position - myTransform.position).sqrMagnitude; // 타겟과의 거리 계산

                // 감지 범위를 벗어나거나 플레이어가 사망한 경우 배회 상태로
                if (sqrDistance > detectionRadiusSqr || (playerStatus != null && playerStatus.HP <= 0))
                {
                    targetTransform = null;
                    SetState(EnemyState.Patrol); 
                    UpdatePatrolTarget();
                }
            }
        }
    }

    #endregion

    #region Movement Logic

    private void PatrolMovement()
    {
        float direction = patrolTarget.x - myTransform.position.x;

        if (direction > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (direction < 0 && isFacingRight)
        {
            Flip();
        }

        float dirX = direction > 0 ? 1f : -1f;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(dirX * moveSpeed * 0.6f, rb.linearVelocity.y);
        }

        // 목표 지점에 도달했는지 확인 (X축 거리 기준)
        if (Mathf.Abs(patrolTarget.x - myTransform.position.x) < 0.2f)
        {
            UpdatePatrolTarget(); // 새로운 배회 목표 지점 설정
        }
    }

    private void ChaseMovement()
    {
        if (targetTransform == null) // 타겟이 없으면 추적 중지
        {
            return;
        }

        PlayerStatus playerStatus = targetTransform.GetComponentInParent<PlayerStatus>();
        if (playerStatus != null && playerStatus.HP <= 0)
        {
            targetTransform = null;
            SetState(EnemyState.Patrol);
            UpdatePatrolTarget();
            return;
        }

        // 방향 전환
        FlipToTarget();

        // X축 거리 계산
        float xDistance = Mathf.Abs(targetTransform.position.x - myTransform.position.x);

        // 너무 가까우면 멈춤
        if (xDistance <= stopDistance)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            return;
        }

        float dirX = targetTransform.position.x > myTransform.position.x ? 1f : -1f;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(dirX * moveSpeed, rb.linearVelocity.y);
        }
    }

    #endregion

    #region Direction & Flips

    private void FlipToTarget() // 타겟을 향해 방향 전환
    {
        if (targetTransform == null)
        {
            return;
        }

        float direction = targetTransform.position.x - myTransform.position.x;

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

        Vector3 scale = myTransform.localScale;
        scale.x *= -1;
        myTransform.localScale = scale;
    }

    #endregion

    #region Utility Methods

    private void UpdatePatrolTarget() // 배회 목표 지점 갱신
    {
        float randomX = Random.Range(-patrolRange, patrolRange); 
        patrolTarget = new Vector3(startPosition.x + randomX, myTransform.position.y, myTransform.position.z); 
    }

    // 감지 범위 시각화
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    #endregion
}