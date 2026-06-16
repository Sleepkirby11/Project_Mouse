using UnityEngine;

public class LaserDamage : MonoBehaviour
{
    [SerializeField] private int damage = 1;

    private void OnTriggerStay2D(Collider2D collision)
    {
        // ������ ����
        if (collision.GetComponent<RedBossAttack>() != null)
        {
            return;
        }

        IDamageable damageable = collision.GetComponent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }
    }
}