using System.Collections;
using UnityEngine;

/*
 근접 공격 스크립트
 - 공격 범위 안에 플레이어가 들어오면 공격
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
    private EnemyStatus enemyStatus;

    private Coroutine attackRoutineInstance;
    private bool isAttacking = false;
    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Update()
    {
        if (enemyStatus != null && enemyStatus.isStunned)
        {
            if (isAttacking && attackRoutineInstance != null)
            {
                StopCoroutine(attackRoutineInstance);
                attackRoutineInstance = null;
                isAttacking = false;
                canAttack = true; // 스턴으로 시전이 끊겼으므로 다음 기회에 바로 공격할 수 있도록 쿨타임 초기화
            }
            return;
        }
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
            attackRoutineInstance = StartCoroutine(AttackRoutine());
        }
    }
    // 애니메이션 완성 전 테스트 코드, 애니메이션 완성하면 지우고 아래 주석 해제
    private IEnumerator AttackRoutine()
    {
        canAttack = false;
        isAttacking = true;

        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.BasicEnemyAttack);
        }

        Debug.Log("적 공격 시도");
        yield return new WaitForSeconds(1f); //선딜레이
        
        isAttacking = false; // 선딜레이가 끝나고 타격을 시도하므로 다시 움직일 수 있도록 함

        if (enemyStatus == null || !enemyStatus.isStunned)
        {
            AttackHit();
        }

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
        attackRoutineInstance = null;
    }

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