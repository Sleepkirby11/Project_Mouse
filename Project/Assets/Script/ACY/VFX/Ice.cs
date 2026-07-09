using UnityEngine;

public class Ice : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [Header("Player Collision Setup")]
    [SerializeField] private float stunDuration = 0.2f;
    [SerializeField] private Vector2 knockbackForce = new Vector2(10f, 3f);

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStatus player = other.GetComponent<PlayerStatus>();

        if (player != null)
        {
            player.ApplyStun(stunDuration);
            player.TakeHit(knockbackForce);
            player.TakeDamage(damage);
        }
    }
    public void ReturnToPool()
    {
        PoolingManager.Instance.Return("Ice", gameObject);
    }
}