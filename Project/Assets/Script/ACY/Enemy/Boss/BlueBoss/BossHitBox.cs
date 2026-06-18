using UnityEngine;

public class BossHitbox : MonoBehaviour
{
    [SerializeField] private int damage;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"충돌: {other.name}, 태그: {other.tag}");
        if (!other.CompareTag("Player"))
        {
            return;
        }
        Debug.Log("플레이어 감지");
        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            Debug.Log("데미지 적용");
            damageable.TakeDamage(damage);
        }
    }
}