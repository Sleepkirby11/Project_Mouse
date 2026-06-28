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


    private bool activated;

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
        if (activateDelay > 0)
            yield return new WaitForSeconds(activateDelay);

        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
                enemy.SetActive(true);
        }

        OnTriggerActivated?.Invoke();
    }
}