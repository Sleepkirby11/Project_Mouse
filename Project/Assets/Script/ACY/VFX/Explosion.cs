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
            // 프로젝트 내 플레이어 피격 로직에 맞게 호출
            // 예: IDamageable 인터페이스나 PlayerStatus 스크립트 사용
            PlayerStatus playerStatus = collision.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                // playerStatus.TakeDamage(damage); // 실제 사용하시는 함수로 변경하세요!
                Debug.Log($"[Explosion] 플레이어 피격! 데미지: {damage}");
            }

            // 데미지를 1회만 주길 원한다면 맞춘 즉시 콜라이더를 끕니다.
            DisableHitbox();
        }
    }
}