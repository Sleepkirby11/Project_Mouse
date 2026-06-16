using System.Collections;
using UnityEngine;

/*
 ���� ���� ��ũ��Ʈ
 - ���� ���� �ȿ� �÷��̾ ������ ����
 - ���� ��Ÿ�� ����
 - ���� ������ Animation Event�� ���� ����
 */
public class BasicEnemyAttack : MonoBehaviour
{
    [Header("���� ����")]
    public int attackDamage = 1;          // ���� �����
    public float attackCooldown = 1.5f;    // ���� ��Ÿ��
    public Transform attackPoint;          // ���� ��ġ
    public float attackRadius = 0.7f;      // ���� ����
    public LayerMask playerLayer;          // �÷��̾� ���̾�

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
    // �ִϸ��̼� �ϼ� �� �׽�Ʈ �ڵ�, �ִϸ��̼� �ϼ��ϸ� ����� �Ʒ� �ּ� ����
    private IEnumerator AttackRoutine()
    {
        canAttack = false;

        if (anim != null)
            {
                anim.SetTrigger("Attack");
            }

            Debug.Log("�� ���� �õ�");
        yield return new WaitForSeconds(0.5f); //�ణ �����
        AttackHit();

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }
    // �ִϸ��̼� �ϼ� �� AttackHit() �Լ��� Animation Event�� ȣ���ϵ��� ����
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
    // Animation Event�� ȣ���� �Լ�
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