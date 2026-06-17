using System.Collections;
using UnityEngine;


public class BasicEnemyAttack : MonoBehaviour
{
    [Header("공격 설정")]
    public int attackDamage = 1;        
    public float attackCooldown = 1.5f;    
    public Transform attackPoint;         
    public float attackRadius = 0.7f;     
    public LayerMask playerLayer;          

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
    private IEnumerator AttackRoutine()
    {
        canAttack = false;

        if (anim != null)
            {
                anim.SetTrigger("Attack");
            }

            Debug.Log("�� ���� �õ�");
        yield return new WaitForSeconds(0.5f); 
        AttackHit();

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }
    public void AttackHit()
    {
        Collider2D hit = Physics2D.OverlapCircle(attackPoint.position, attackRadius, playerLayer);

        if (hit != null)
        {
            Debug.Log("���� ����");

            PlayerStatus playerStatus = hit.GetComponentInParent<PlayerStatus>();

            if (playerStatus != null)
            {
                playerStatus.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmosSelected() // ���� ���� �ð�ȭ
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}