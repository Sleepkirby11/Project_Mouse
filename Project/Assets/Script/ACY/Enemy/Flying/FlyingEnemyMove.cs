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
    private float patrolTimer;
    private EnemyState state = EnemyState.Patrol; // 초기 상태는 배회

    // 캐싱 (최적화)
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
        originPos = rb.position;

        // 물리 설정 최적화
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
        rb.freezeRotation = true; // 회전 방지

        // 필터 초기화: 가비지 컬렉션을 피하기 위해 미리 설정
        playerFilter.useLayerMask = true;
        playerFilter.layerMask = playerLayer;
    }

    void Update() 
    {
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

    private void UpdatePatrol()
    {
        patrolTimer += Time.deltaTime * patrolSpeed;
        rb.MovePosition(new Vector2(originPos.x + Mathf.Cos(patrolTimer) * patrolRange, originPos.y));
    }
    private void CheckPlayerBelow()
    {
        // 최적화된 BoxCast: 리스트 재사용으로 GC 방지
        if (Physics2D.BoxCast(rb.position, new Vector2(detectionWidth, 0.1f), 0f, Vector2.down, playerFilter, hitResults, detectionHeight) > 0)
        {
            state = EnemyState.Action;
            FlyingAttack.StartDive();
        }
    }

    private void UpdateReturn() // 원래 위치로 돌아가는 움직임 업데이트
    {
        Vector2 nextPos = Vector2.MoveTowards(rb.position, originPos, returnSpeed * Time.deltaTime);
        rb.MovePosition(nextPos);

        if (Vector2.SqrMagnitude(originPos - nextPos) < 0.001f)
        {
            rb.position = originPos;
            patrolTimer = Mathf.PI * 0.5f; // 중앙에서 시작하도록 보정
            state = EnemyState.Patrol;
        }
    }
    public void OnAttackProcessFinished() => state = EnemyState.Return;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.down * detectionHeight * 0.5f, new Vector3(detectionWidth, detectionHeight, 0));
    }
}