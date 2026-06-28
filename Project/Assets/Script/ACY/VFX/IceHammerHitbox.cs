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
}