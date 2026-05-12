using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*행동패턴: 공중에서 배회
공격패턴: 플레이어가 아래를 지나가면 급강하 + 근접 공격
특징: 공격 후 다시 올라가면서 빈틈 발생
*/

[RequireComponent(typeof(Rigidbody2D))] // Rigidbody2D 컴포넌트가 반드시 필요
public class FlyingEnemy : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private float detectionWidth = 1.5f;  // 박스의 가로 길이
    [SerializeField] private float detectionHeight = 8f; // 박스의 세로 길이
    [SerializeField] private LayerMask playerLayer; // 플레이어 레이어

    [Header("배회 설정")]
    [SerializeField] private float patrolRange = 3f; // 배회 범위
    [SerializeField] private float patrolSpeed = 2f; // 배회 속도
    private float patrolTimer;

    [Header("움직임 설정")]
    [SerializeField] private float diveSpeed = 18f; // 급강하 속도
    [SerializeField] private float returnSpeed = 4f; // 원래 위치로 돌아가는 속도
    [SerializeField] private float waitTime = 1.0f; // 공격 후 대기 시간

    private Vector2 originPos; // 초기 위치 저장
    private Rigidbody2D rb; 
    private EnemyState state = EnemyState.Patrol; // 초기 상태는 배회

    // 캐싱 (최적화)
    private WaitForSeconds waitInstruction; // 대기 시간 캐싱
    private ContactFilter2D playerFilter; // 플레이어 감지용 필터
    private readonly List<RaycastHit2D> hitResults = new List<RaycastHit2D>(1); // BoxCast 결과 저장용 리스트 

    private enum EnemyState // 행동 상태 열거형
    { 
        Patrol, Dive, Wait, Return  // 배회, 급강하, 대기, 돌아가기
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originPos = transform.position;
       waitInstruction = new WaitForSeconds(waitTime);

        // 물리 설정 최적화
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
        rb.freezeRotation = true; // 회전 방지

        // 필터 초기화: 가비지 컬렉션을 피하기 위해 미리 설정
        playerFilter.useLayerMask = true;
        playerFilter.layerMask = playerLayer;
        playerFilter.useTriggers = true; // 필요 시 설정
    }

    void Update() 
    {
        switch (state) // 행동 상태에 따른 업데이트
        {
            case EnemyState.Patrol:     // 배회 상태
                UpdatePatrol();                // 배회 움직임 업데이트
                CheckPlayerBox();           // 플레이어 감지
                break; 
            case EnemyState.Return:  // 돌아가기 상태
                UpdateReturn();              // 원래 위치로 돌아가는 움직임 업데이트
                break; 
        }
    }

    private void UpdatePatrol() // 배회 움직임 업데이트
    {
        patrolTimer += Time.deltaTime * patrolSpeed;
        float xOffset = Mathf.Cos(patrolTimer) * patrolRange;

        rb.MovePosition(new Vector2(originPos.x + xOffset, originPos.y));
    }

    private void CheckPlayerBox() // 플레이어 감지
    {
        //  List를 재사용하여 가비지 발생 차단
        int count = Physics2D.BoxCast(
            transform.position,                                  // 박스의 중심은 적의 현재 위치
            new Vector2(detectionWidth, 0.1f),    //박스의 크기
            0f,                                                               // 회전 없음
            Vector2.down,                                         // 아래 방향으로 감지
           playerFilter,                                               // 플레이어 레이어만 감지
            hitResults,                                                // 결과 저장 리스트
            detectionHeight                                     // 감지 거리
        );

        if (count > 0) // 플레이어가 감지되면 급강하 시작
        {
            StartDive(); 
        }
    }

    private void StartDive() // 급강하 시작
    {
       state = EnemyState.Dive;  // 상태 변경
        rb.linearVelocity = Vector2.zero;
        rb.linearVelocity = Vector2.down * diveSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state == EnemyState.Dive) // 급강하 중에 충돌하면 대기 상태로 전환
        {
            StartCoroutine(WaitAndReturnRoutine()); 
        }
    }

    private IEnumerator WaitAndReturnRoutine() // 대기 후 돌아가기 루틴
    {
        state = EnemyState.Wait; // 상태 변경
        rb.linearVelocity = Vector2.zero; // 충돌 후 속도 초기화
        yield return waitInstruction; // 대기 시간 동안 기다림
        state = EnemyState.Return; // 돌아가기 상태로 전환
    }

    private void UpdateReturn() // 원래 위치로 돌아가는 움직임 업데이트
    {
        Vector2 currentPos = rb.position; // 현재 위치 가져오기
        Vector2 nextPos = Vector2.MoveTowards(currentPos, originPos, returnSpeed * Time.deltaTime); // 원래 위치로 이동할 다음 위치 계산
        rb.MovePosition(nextPos); // 다음 위치로 이동

        if (Vector2.SqrMagnitude(originPos - nextPos) < 0.001f) // 원래 위치에 거의 도달하면 배회 상태로 전환
        {
            rb.position = originPos; 
            rb.linearVelocity = Vector2.zero; // 물리 초기화

            // 배회 타이머를 중앙(0)에서 시작하도록 설정 (Cos 90도 = 0)
            patrolTimer = Mathf.PI * 0.5f;
            state = EnemyState.Patrol;
        }
    }

    void OnDrawGizmosSelected() // 감지 영역 시각화
    {
        Gizmos.color = Color.red;
        Vector3 center = transform.position + (Vector3.down * detectionHeight * 0.5f);
        Gizmos.DrawWireCube(center, new Vector3(detectionWidth, detectionHeight, 0));
    }
}