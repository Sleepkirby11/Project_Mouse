using System.Collections;
using UnityEngine;

public class FlyingEnemyAttack : MonoBehaviour
{
    [Header("공격 설정")]
    [SerializeField] private float diveSpeed = 18f;
    [SerializeField] private float waitTime = 1.0f; // 공격 후 후딜레이

    private Rigidbody2D rb;
    private FlyingEnemyMove FlyingMove;
    private WaitForSeconds waitInstruction; // [최적화] 캐싱
    private bool isDiving;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        FlyingMove = GetComponent<FlyingEnemyMove>();
        waitInstruction = new WaitForSeconds(waitTime);
    }

    public void StartDive()
    {
        isDiving = true;
        rb.linearVelocity = Vector2.down * diveSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDiving)
        {
            isDiving = false;
            StartCoroutine(WaitRoutine());
        }
    }

    private IEnumerator WaitRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        yield return waitInstruction; // 캐싱된 객체 사용으로 GC 방지

        // 공격 프로세스가 끝났음을 이동 스크립트에 알림
        FlyingMove.OnAttackProcessFinished();
    }
}
