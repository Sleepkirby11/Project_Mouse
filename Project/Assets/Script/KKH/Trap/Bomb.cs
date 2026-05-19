using System.Collections;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("폭탄 체력 설정")]
    [SerializeField] private int maxHealth = 1;
    private int currentHealth;

    [Header("폭발 설정")]
    [SerializeField] private float explosionDelay = 1.5f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private int explosionDamage = 1;

    //유니티 인스펙터에서 'Player'와 'Enemy' 레이어를 모두 체크해 주어야 합니다.
    [SerializeField] private LayerMask targetLayer;

    private bool isExploded = false;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void BombTakeDamage(int damage)
    {
        if (isExploded) return;

        currentHealth -= damage;
        Debug.Log($"폭탄이 공격받음! 현재 체력: {currentHealth}");

        if (currentHealth <= 0)
        {
            StartCoroutine(ExplosionSequence());
        }
    }

    private IEnumerator ExplosionSequence()
    {
        isExploded = true;
        yield return new WaitForSeconds(explosionDelay);
        Explode();
    }

    private void Explode()
    {
        Debug.Log("펑!!! 폭발 발생!");

        // 지정된 레이어(Player, Enemy 등)에 속한 모든 콜라이더를 가져옵니다.
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, explosionRadius, targetLayer);

        foreach (Collider2D target in targets)
        {
            // 1. 플레이어인지 확인하고 데미지 주기
            PlayerStatus player = target.GetComponent<PlayerStatus>();
            if (player != null)
            {
                player.TakeDamage(explosionDamage);
                Debug.Log($"폭발 범위 안의 플레이어에게 {explosionDamage}의 피해를 입혔습니다.");
                continue; // 플레이어 상태를 찾았다면 아래 적(Enemy) 검사는 건너뜁니다.
            }

            // 2. 적(Enemy)인지 확인하고 데미지 주기
            // ⚠️ 주의: 프로젝트에 선언된 실제 적 체력 스크립트 이름으로 변경해 주세요! (예: Enemy, EnemyHealth 등)
            EnemyStatus enemy = target.GetComponent<EnemyStatus>();
            if (enemy != null)
            {
                enemy.TakeDamage(explosionDamage);
                Debug.Log($"폭발 범위 안의 적({target.name})에게 {explosionDamage}의 피해를 입혔습니다.");
            }
        }

        // 폭발한 폭탄 오브젝트 제거
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}