using UnityEngine;

public class Ice : MonoBehaviour
{
    [SerializeField] private int damage = 1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStatus player = other.GetComponent<PlayerStatus>();

        if (player != null)
        {
            player.TakeDamage(damage);
        }
    }
    public void ReturnToPool()
    {
        PoolingManager.Instance.Return("Ice", gameObject);
    }
}