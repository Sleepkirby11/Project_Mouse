using UnityEngine;
using UnityEngine.Events;

public class EnemyTrigger : MonoBehaviour
{
    // 인스펙터창에서 여러마리 등록
    [SerializeField] private GameObject[] enemies;

    // 트리거 발동 시 실행할 이벤트 (인스펙터에서 Wall.Rebuild를 연결할 곳)
    [SerializeField] private UnityEvent OnTriggerActivated;

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

            // 1. 배열에 등록된 모든 적을 하나씩 활성화
            foreach (GameObject enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.SetActive(true);
                }
            }

            // 2. 등록된 이벤트(벽 복구 등)를 실행
            OnTriggerActivated?.Invoke();
        }
    }
}