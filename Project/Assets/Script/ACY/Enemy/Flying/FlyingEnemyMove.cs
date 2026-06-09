using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*행동패턴: 공중에서 배회
공격패턴: 플레이어가 아래를 지나가면 급강하 + 근접 공격
특징: 공격 후 다시 올라가면서 빈틈 발생
*/

[RequireComponent(typeof(Rigidbody2D))] // Rigidbody2D 컴포넌트가 반드시 필요
public class FlyingEnemyMove : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private float detectionWidth = 1.5f;  // 박스의 가로 길이
    [SerializeField] private float detectionHeight = 8f; // 박스의 세로 길이
    [SerializeField] private LayerMask playerLayer; // 플레이어 레이어

    [Header("배회 설정")]
    [SerializeField] private float patrolRange = 3f; // 배회 범위
    [SerializeField] private float patrolSpeed = 2f; // 배회 속도
    [SerializeField] private float returnSpeed = 4f;

    private Vector2 originPos; // 초기 위치 저장
    private Rigidbody2D rb;
    private Animator anim;
    private float patrolTimer;
    private EnemyState state = EnemyState.Patrol; // 초기 상태는 배회
    private float prevX;

    private ContactFilter2D playerFilter; // 플레이어 감지용 필터
    private readonly List<RaycastHit2D> hitResults = new List<RaycastHit2D>(1); // BoxCast 결과 저장용 리스트 
    private FlyingEnemyAttack FlyingAttack;

    private enum EnemyState // 행동 상태 열거형
    { 
        Patrol, Action, Return  // 배회, 급강하, 대기, 돌아가기
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        FlyingAttack = GetComponent<FlyingEnemyAttack>();
        anim = GetComponentInChildren<Animator>();
        originPos = rb.position;

        // 물리 설정 최적화
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
        rb.freezeRotation = true; // 회전 방지

        prevX = originPos.x + Mathf.Cos(0) * patrolRange;  //방향 계산
        playerFilter.useLayerMask = true;
        playerFilter.layerMask = playerLayer;
    }

    void Update() 
    {
        UpdateAnimator();

        if (state == EnemyState.Patrol)
        {
            UpdatePatrol();
            CheckPlayerBelow();
        }
        else if (state == EnemyState.Return)
        {
            UpdateReturn();
        }
    }

    private void UpdateAnimator()
    {
        if (anim == null)
        {
            return;
        }
        anim.SetBool("IsMoving", state == EnemyState.Patrol);
        anim.SetBool("IsDiving", state == EnemyState.Action);
        anim.SetBool("IsReturning", state == EnemyState.Return);
    }

    private void UpdatePatrol()
    {
        patrolTimer += Time.deltaTime * patrolSpeed;
        float newX = originPos.x + Mathf.Cos(patrolTimer) * patrolRange;

        Flip(newX - prevX);

        prevX = newX;
        rb.MovePosition(new Vector2(newX, originPos.y));
    }
    private void CheckPlayerBelow()
    {
        if (Physics2D.BoxCast(rb.position, new Vector2(detectionWidth, 0.1f), 0f, Vector2.down, playerFilter, hitResults, detectionHeight) > 0)
        {
            state = EnemyState.Action;
            FlyingAttack.StartDive();
        }
    }

    public void OnAttackProcessFinished()
    {
        state = EnemyState.Return;
    }
    private void UpdateReturn()
    {
        Vector2 nextPos = Vector2.MoveTowards(rb.position, originPos, returnSpeed * Time.deltaTime);
        rb.MovePosition(nextPos);

        if (Vector2.SqrMagnitude(originPos - nextPos) < 0.001f)
        {
            rb.position = originPos;
            patrolTimer = Mathf.PI * 0.5f;
            state = EnemyState.Patrol;
            Flip(Mathf.Cos(patrolTimer)); // 원점 도착 후 패트롤 시작 방향으로 플립
        }
    }
    private void Flip(float dirX)
    {
        if (dirX > 0.001f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (dirX < -0.001f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 배회 범위 — 파란색
        Gizmos.color = Color.blue;
        Vector3 patrolCenter = Application.isPlaying ? new Vector3(originPos.x, originPos.y, 0) : transform.position;
        Gizmos.DrawLine(patrolCenter + Vector3.left * patrolRange, patrolCenter + Vector3.right * patrolRange);

        // 감지 범위 - 빨간색
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.down * detectionHeight * 0.5f, new Vector3(detectionWidth, detectionHeight, 0));
    }
}