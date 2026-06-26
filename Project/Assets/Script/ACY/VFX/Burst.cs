using UnityEngine;

public class Burst : MonoBehaviour
{
    [Header("대미지 설정")]
    [SerializeField] private int explosionDamage = 10;

    private string myPoolName;

    public void InitializeBurst(string poolName)
    {
        myPoolName = poolName;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            IDamageable damageable = collision.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(explosionDamage);
            }
        }
    }

    // 애니메이션 맨 마지막 프레임에 이벤트로 등록
    public void ReturnToPoolEvent()
    {
        PoolingManager.Instance.Return(myPoolName, gameObject);
    }
}
