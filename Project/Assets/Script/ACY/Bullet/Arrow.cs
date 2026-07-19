using UnityEngine;

public class Arrow : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float gravity = -20f;

    [SerializeField] private float timeToTarget = 0.8f;

    [SerializeField] private float maxLifeTime = 3f;

    [Header("대미지")]
    [SerializeField] private int damage = 1;

    [Header("바닥 체크")]
    [SerializeField] private LayerMask groundLayer;

    private Vector2 velocity;

    private float timer;

    private bool isInitialized;

    private const string POOL_KEY = "Arrow";

    public void Initialize(Vector2 start, Vector2 target)
    {
        transform.position = start;

        Vector2 diff = target - start;

        velocity.x = diff.x / timeToTarget;

        velocity.y = (diff.y - (0.5f * gravity * timeToTarget * timeToTarget)) / timeToTarget;

        timer = 0f;

        isInitialized = true;

        RotateArrow();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        timer += Time.deltaTime;

        // 중력 적용
        velocity.y += gravity * Time.deltaTime;

        // 이동
        transform.position += (Vector3)(velocity * Time.deltaTime);

        // 회전
        RotateArrow();

        // 너무 오래 날아가면 제거
        if (timer >= maxLifeTime)
        {
            ReturnToPool();
        }
    }

    private void RotateArrow()
    {
        float angle =
            Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInitialized)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
            }

            ReturnToPool();
            return;
        }

        if (((1 << other.gameObject.layer) & groundLayer) != 0)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        isInitialized = false;

        if (PoolingManager.Instance != null)
        {
            PoolingManager.Instance.Return(POOL_KEY, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}