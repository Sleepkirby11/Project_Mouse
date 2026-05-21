using UnityEngine;

/*
 포물선으로 이동하는 화살
 */
public class Arrow : MonoBehaviour
{
    private Vector2 startPos;
    private Vector2 targetPos;
    private float height;
    private float duration;
    private float elapsed;
    private bool isInitialized;

    [Header("대미지")]
    public int damage = 1;

    private const string POOL_KEY = "Arrow";

    public void Initialize(Vector2 start, Vector2 target, float arrowHeight, float arrowDuration)
    {
        startPos = start;
        targetPos = target;
        height = arrowHeight;
        duration = arrowDuration;
        elapsed = 0f;
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = elapsed / duration; 

        // 목표 도달 시 제거
        if (t >= 1f)
        {
            ReturnToPool();
            return;
        }

        // 포물선 이동
        Vector2 linear = Vector2.Lerp(startPos, targetPos, t);
        float arc = height * Mathf.Sin(Mathf.PI * t); // 높이
        transform.position = new Vector2(linear.x, linear.y + arc);

        // 화살 방향 회전
        if (elapsed > Time.deltaTime)
        {
            Vector2 prevPos = transform.position;
            Vector2 nextLinear = Vector2.Lerp(startPos, targetPos, t + 0.01f);
            float nextArc = height * Mathf.Sin(Mathf.PI * (t + 0.01f));
            Vector2 nextPos = new Vector2(nextLinear.x, nextLinear.y + nextArc);

            Vector2 dir = (nextPos - prevPos).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // 플레이어 충돌 처리
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
            }
        ReturnToPool();
        }
    }
    private void ReturnToPool()
    {
        isInitialized = false;
        PoolingManager.Instance.Return(POOL_KEY, gameObject);
    }
}
