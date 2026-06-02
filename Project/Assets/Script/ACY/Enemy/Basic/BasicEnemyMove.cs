using System.Collections;
using UnityEngine;

/*
 행동패턴: 좌우를 배회 (인스펙터에서 수치 조절 가능)
 플레이어 감지 시 플레이어를 추적
 */
public class BasicEnemyMove : MonoBehaviour
{
    public enum EnemyState { Patrol, Chase } // 배회상태, 추적상태

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

    private Animator animator; // 추가
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");   // 추가
    private static readonly int IsChasing = Animator.StringToHash("IsChasing"); // 추가

    private WaitForSeconds scanIntervalWFS;

    private void Awake()
    {
        myTransform = transform;
        startPosition = myTransform.position;

        detectionRadiusSqr = detectionRadius * detectionRadius;

        scanIntervalWFS = new WaitForSeconds(0.2f); // 0.2초 주기 스캔
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 최초 배회 목표 지점 설정
        UpdatePatrolTarget();
        StartCoroutine(EnvironmentScanRoutine()); // 감지 코루틴 시작
    }

    private void Update()
    {
        switch (currentState) // 상태에 따른 행동 분기
        {
            case EnemyState.Patrol: // 배회 상태에서는 좌우로 이동
                PatrolMovement();
                break;
            case EnemyState.Chase: // 추적 상태에서는 플레이어를 향해 이동
                ChaseMovement();
                break;
        }
    }
    private void SetState(EnemyState newState)
    {
        currentState = newState;

        bool isChasing = newState == EnemyState.Chase;
        animator.SetBool(IsMoving, !isChasing);   // Patrol일 때만 IsMoving
        animator.SetBool(IsChasing, isChasing);    // Chase일 때만 IsChasing
    }

    private IEnumerator EnvironmentScanRoutine()
    {
        while (true)
        {
            // 0.2초 대기
            yield return scanIntervalWFS;

            if (targetTransform == null)
            {
                // 플레이어가 타겟팅되지 않은 상태일 때만 주변 반경 레이어 검사
                Collider2D hit = Physics2D.OverlapCircle(myTransform.position, detectionRadius, targetLayer); 
                if (hit != null) // 감지되면 추적
                {
                    targetTransform = hit.transform;
                    SetState(EnemyState.Chase);
                }
            }
            else
            {
                float sqrDistance = (targetTransform.position - myTransform.position).sqrMagnitude; // 타겟과의 거리 계산

                if (sqrDistance > detectionRadiusSqr) // 감지 범위를 벗어나면 배회 상태로
                {
                    targetTransform = null;
                    SetState(EnemyState.Patrol); 
                    UpdatePatrolTarget();
                }
            }
        }
    }

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
        myTransform.position = Vector3.MoveTowards(myTransform.position, patrolTarget, moveSpeed * Time.deltaTime); // 배회 목표 지점으로 이동

        // 목표 지점에 도달했는지 확인
        if ((patrolTarget - myTransform.position).sqrMagnitude < 0.01f)
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

        // 방향 전환
        FlipToTarget();

        // X축 거리 계산
        float xDistance = Mathf.Abs(targetTransform.position.x - myTransform.position.x);

        // 너무 가까우면 멈춤
        if (xDistance <= stopDistance)
        {
            return;
        }

        Vector3 targetPos = new Vector3(targetTransform.position.x, myTransform.position.y, myTransform.position.z); //x 축으로만 이동

        myTransform.position = Vector3.MoveTowards // 타겟을 향해 이동
        ( 
            myTransform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    private void UpdatePatrolTarget() // 배회 목표 지점 갱신
    {
        float randomX = Random.Range(-patrolRange, patrolRange); 
        patrolTarget = new Vector3(startPosition.x + randomX, myTransform.position.y, myTransform.position.z); 
    }
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
    // 감지 범위 시각화
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}