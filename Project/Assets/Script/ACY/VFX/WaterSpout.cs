using System.Collections;
using UnityEngine;

public class WaterSpout : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private string poolKey = "WaterSpout";

    [Header("Hitbox")]
    [SerializeField] private Collider2D hitbox;

    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float launchForceY = 15f;
    [SerializeField] private float stunTime = 0.3f;

    private bool hasHit;

    private void OnEnable()
    {
        hasHit = false;

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    // ─────────────────────────────
    // Animation Events
    // ─────────────────────────────

    public void HitboxOn()
    {
        hasHit = false;
        hitbox.enabled = true;
    }

    public void HitboxOff()
    {
        hitbox.enabled = false;
    }

    public void AnimationEnd()
    {
        hitbox.enabled = false;

        PoolingManager.Instance.Return(poolKey, gameObject);
    }

    // ─────────────────────────────
    // Attack
    // ─────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hitbox.enabled) return;
        if (hasHit) return;
        if (!other.CompareTag("Player")) return;

        hasHit = true;

        if (other.TryGetComponent(out IDamageable damageable))
            damageable.TakeDamage(damage);

        if (other.TryGetComponent(out PlayerStatus playerStatus))
            playerStatus.LaunchByWater(launchForceY);

        if (other.TryGetComponent(out IStunnable stunnable))
            stunnable.ApplyStun(stunTime);
    }
}