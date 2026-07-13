using UnityEngine;

public class TutorialTriggerZone : MonoBehaviour
{
    [Header("트리거할 튜토리얼 단계 이름 (인덱스보다 우선)")]
    public string stepName;

    [Header("트리거할 튜토리얼 단계 인덱스 (이름이 비어있을 때 사용)")]
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
                if (!string.IsNullOrEmpty(stepName))
                {
                    // 이미 완료된 단계면 발동 안 함
                    if (TutorialManager.Instance.IsStepCompleted(stepName))
                        return;

                    TutorialManager.Instance.TriggerTutorialStep(stepName);
                }
                else
                {
                    // 이미 완료된 단계면 발동 안 함
                    if (TutorialManager.Instance.IsStepCompleted(stepIndex))
                        return;

                    TutorialManager.Instance.TriggerTutorialStep(stepIndex);
                }
                hasTriggered = true;
            }
        }
    }
}
