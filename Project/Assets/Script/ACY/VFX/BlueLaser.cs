using UnityEngine;

public class BlueLaser : MonoBehaviour
{
    private Vector2 fireDirection;
    private float range;
    private int damage;
    private LayerMask playerLayer;
    private bool isInitialized = false;
    private bool hasHitPlayer = false;

    public void InitializeLaser(Vector2 direction, float range, int damage, LayerMask layer)
    {
        this.fireDirection = direction;
        this.range = range;
        this.damage = damage;
        this.playerLayer = layer;
        this.hasHitPlayer = false;
        this.isInitialized = true;
    }

    private void OnEnable()
    {
        hasHitPlayer = false;
    }

    private void Update()
    {
        if (!isInitialized || hasHitPlayer) return;

        // 플레이어 레이캐스트 감지
        RaycastHit2D hit = Physics2D.Raycast(transform.position, fireDirection, range, playerLayer);
        if (hit.collider != null)
        {
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage);
                hasHitPlayer = true;
            }
        }
    }

    public void ReturnToPool() // 애니메이션 이벤트로 호출
    {
        // 부모가 있으면 부모인 BlueLaser(루트)를 반환하고, 없으면 본인을 반환
        GameObject objToReturn = transform.parent != null ? transform.parent.gameObject : gameObject;
        PoolingManager.Instance.Return("BlueLaser", objToReturn);
    }
}
