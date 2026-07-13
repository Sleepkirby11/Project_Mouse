using UnityEngine;

public class TutorialTriggerZone : MonoBehaviour
{
    [Header("트리거할 튜토리얼 단계 인덱스")]
    public int stepIndex;

    [Header("한 번만 실행할지 여부")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 씬 내 플레이어 레이어나 태그 검사
        if (other.CompareTag("Player"))
        {
            if (triggerOnce && hasTriggered)
                return;

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.TriggerTutorialStep(stepIndex);
                hasTriggered = true;
            }
        }
    }
}
