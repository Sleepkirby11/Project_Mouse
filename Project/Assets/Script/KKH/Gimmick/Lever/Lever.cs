using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour, IInteractable
{
    [Header("레버 작동 시 실행할 이벤트 (인스펙터에서 벽 지정)")]
    [SerializeField] private UnityEvent onLeverActivate;

    private bool isActivated;
    public bool IsActivated => isActivated;

    public void RestoreActivated(bool activated)
    {
        isActivated = activated;
    }

    public void Interact()
    {
        Debug.Log($"{gameObject.name} 레버가 가동되었습니다.");
        isActivated = true;
        SaveManager.NotifyLeverActivated(this);

        if (onLeverActivate != null)
        {
            onLeverActivate.Invoke();
        }
    }
}