using UnityEngine;

public class HitboxProxy : MonoBehaviour, IDamageable
{
    private IceHammer parentHammer;

    private void Awake()
    {
        parentHammer = GetComponentInParent<IceHammer>();
    }

    public void TakeDamage(int damage)
    {
        if (parentHammer != null)
        {
            parentHammer.TakeDamage(damage);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (parentHammer != null)
        {
            parentHammer.HandlePlayerCollision(collision.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (parentHammer != null)
        {
            parentHammer.HandlePlayerCollision(collision.gameObject);
        }
    }
}