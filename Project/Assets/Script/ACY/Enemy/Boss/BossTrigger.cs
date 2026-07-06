using UnityEngine;

public class BossTrigger : MonoBehaviour
{
    [SerializeField] private GameObject boss;

    private bool activated = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[BossTrigger] 플레이어가 트리거에 진입했습니다. 보스 활성화: {boss.name}");
            activated = true;
            boss.SetActive(true);

            EnemyStatus enemyStatus = boss.GetComponent<EnemyStatus>();
            if (enemyStatus == null)
            {
                enemyStatus = boss.GetComponentInChildren<EnemyStatus>(true);
            }

            if (enemyStatus != null)
            {
                if (UI.Instance != null)
                {
                    Debug.Log($"[BossTrigger] UI.Instance를 찾았습니다. 보스 '{enemyStatus.gameObject.name}'의 체력바를 활성화합니다. 비율: {enemyStatus.GetHPRatio()}");
                    UI.Instance.ShowBossHPBar(enemyStatus.GetHPRatio());
                }
                else
                {
                    Debug.LogError("[BossTrigger] UI.Instance가 null입니다! UIManager가 초기화되지 않았을 수 있습니다.");
                }
            }
            else
            {
                Debug.LogError($"[BossTrigger] 보스 '{boss.name}' 또는 그 하위 오브젝트에서 EnemyStatus 컴포넌트를 찾을 수 없습니다!");
            }
        }
    }
}