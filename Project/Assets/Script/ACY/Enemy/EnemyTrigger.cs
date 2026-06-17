using UnityEngine;

public class EnemyTrigger : MonoBehaviour
{
    // 인스펙터창에서 여러마리 등록
    [SerializeField] private GameObject[] enemies;

    private bool activated = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 이미 발동했다면 무시
        if (activated)
        {
            return;
        }

        // 플레이어가 부딪혔을 때
        if (other.CompareTag("Player"))
        {
            activated = true;

            // 배열에 등록된 모든 적을 하나씩 활성화
            foreach (GameObject enemy in enemies)
            {
                // 예외 처리
                if (enemy != null)
                {
                    enemy.SetActive(true);
                }
            }
        }
    }
}