using UnityEngine;

public class Explosion : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private int damage = 2; // 폭발 데미지
    [SerializeField] private string poolKey = "Explosion"; // 풀링 키

    private Collider2D damageCollider;

    private void Awake()
    {
        damageCollider = GetComponent<Collider2D>();

        // 초기 설정: 콜라이더는 꺼둔 상태로 대기
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }
    }

    private void OnEnable()
    {
        // 풀에서 다시 꺼내질 때 콜라이더가 켜져있는 버그 방지
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }
    }

    // ─── 애니메이션 이벤트 연동 함수 ──────────────────────────────

    public void EnableHitbox()
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }
    }

    public void DisableHitbox()
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }
    }

    public void ReturnToPool()
    {
        PoolingManager.Instance.Return(poolKey, gameObject);
    }

    // ─── 데미지 처리 ──────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus playerStatus = collision.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                playerStatus.TakeDamage(damage);
            }

            DisableHitbox();
        }
    }
}