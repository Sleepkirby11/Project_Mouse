using UnityEngine;

public class Ice : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [Header("Player Collision Setup")]
    [SerializeField] private float stunDuration = 0.2f;
    [SerializeField] private Vector2 knockbackForce = new Vector2(10f, 3f);

    private void OnEnable()
    {
        // 오브젝트 풀 생성 시(초기화) 사운드 재생 방지
        if (transform.parent != null && transform.parent.GetComponent<PoolingManager>() != null)
        {
            return;
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.Ice);
        }
    }

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