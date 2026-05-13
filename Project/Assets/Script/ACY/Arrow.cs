using UnityEngine;

/*
 * 포물선으로 이동하는 화살
 * 차후 풀링으로 변경 예정
 */
public class Arrow : MonoBehaviour
{
    private Vector2 startPos;
    private Vector2 targetPos;
    private float height;
    private float duration;
    private float elapsed;
    private bool isInitialized;

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
        if (!isInitialized) return;

        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            // 목표 도달 시 제거
            Destroy(gameObject);
            return;
        }

        // 포물선 이동
        Vector2 linear = Vector2.Lerp(startPos, targetPos, t);
        float arc = height * Mathf.Sin(Mathf.PI * t); // 포물선 높이
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
        if (other.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
    }
}
