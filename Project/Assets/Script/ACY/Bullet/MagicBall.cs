using UnityEngine;

/*
 * 마법사가 발사하는 구체
 * 차후 풀링으로 변경 예정
 */
public class MagicBall : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifeTime = 5f;

    private Vector2 direction;
    private bool isInitialized;

    public void Initialize(Vector2 dir)
    {
        direction = dir.normalized;
        isInitialized = true;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}