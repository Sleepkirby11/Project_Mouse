using System.Collections;
using UnityEngine;

/*
 근접 공격 스크립트
 - 공격 범위 안에 플레이어가 들어오면 공격
 - 공격 쿨타임 적용
 - 공격 판정은 Animation Event로 실행 예정
 */
public class BasicEnemyAttack : MonoBehaviour
{
    [Header("공격 설정")]
    public int attackDamage = 1;          // 공격 대미지
    public float attackCooldown = 1.5f;    // 공격 쿨타임
    public Transform attackPoint;          // 공격 위치
    public float attackRadius = 0.7f;      // 공격 범위
    public LayerMask playerLayer;          // 플레이어 레이어

    private Animator anim;
    private bool canAttack = true;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        CheckAttackRange();
    }

    private void CheckAttackRange()
    {
        if (!canAttack)
        {
            return;
        }

        Collider2D hit = Physics2D.OverlapCircle(attackPoint.position, attackRadius, playerLayer);

        if (hit != null)
        {
            StartCoroutine(AttackRoutine());
        }
    }
    // 애니메이션 완성 전 테스트 코드, 애니메이션 완성하면 지우고 아래 주석 해제
    private IEnumerator AttackRoutine()
    {
        canAttack = false;

        if (anim != null)
            {
                anim.SetTrigger("Attack");
            }

            Debug.Log("적 공격 시도");
        yield return new WaitForSeconds(0.5f); //약간 딜레이
        AttackHit();

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }
    // 애니메이션 완성 시 AttackHit() 함수를 Animation Event로 호출하도록 변경
    //private IEnumerator AttackRoutine()
    //{
    //    canAttack = false;

    //    if (anim != null)
    //    {
    //        anim.SetTrigger("Attack");
    //    }

    //    yield return new WaitForSeconds(attackCooldown);

    //    canAttack = true;
    //}
    // Animation Event로 호출할 함수
    public void AttackHit()
    {
        Collider2D hit = Physics2D.OverlapCircle(attackPoint.position, attackRadius, playerLayer);

        if (hit != null)
        {
            Debug.Log("공격 성공");

            PlayerStatus playerStatus = hit.GetComponentInParent<PlayerStatus>();

            if (playerStatus != null)
            {
                playerStatus.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmosSelected() // 공격 범위 시각화
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}