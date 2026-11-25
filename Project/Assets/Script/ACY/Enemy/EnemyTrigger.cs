using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class EnemyTrigger : MonoBehaviour
{
    // 인스펙터창에서 여러마리 등록
    [SerializeField] private GameObject[] enemies;

    // 트리거 발동 시 실행할 이벤트 (인스펙터에서 Wall.Rebuild를 연결할 곳)
    [SerializeField] private UnityEvent OnTriggerActivated;

    [SerializeField] private float activateDelay = 0f;

    [Header("보스 컷씬")]
    [SerializeField] private bool playBossCutscene = true;

    private bool activated;

    private void Awake()
    {
        if (!playBossCutscene || activated || enemies == null)
            return;

        if (FindBossInEnemies() == null)
            return;

        // 프리팹 수정 등으로 보스가 활성 상태로 저장되어도 트리거 진입 전에는 비활성 유지
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].activeSelf)
            {
                enemies[i].SetActive(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated)
            return;

        if (!other.CompareTag("Player"))
            return;

        activated = true;
        StartCoroutine(ActivateRoutine());
    }

    private IEnumerator ActivateRoutine()
    {
        EnemyStatus boss = FindBossInEnemies();

        if (playBossCutscene && boss != null)
        {
            // 보스방 문 닫기 등 트리거 이벤트 선반영
            OnTriggerActivated?.Invoke();

            if (activateDelay > 0)
                yield return new WaitForSeconds(activateDelay);

            BossIntroCutscene.Play(boss, enemies);
            yield break;
        }

        if (activateDelay > 0)
            yield return new WaitForSeconds(activateDelay);

        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
                enemy.SetActive(true);
        }

        OnTriggerActivated?.Invoke();
    }

    private EnemyStatus FindBossInEnemies()
    {
        if (enemies == null) return null;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            EnemyStatus status = enemies[i].GetComponent<EnemyStatus>();
            if (status != null && status.IsBoss)
            {
                return status;
            }
        }

        return null;
    }
}