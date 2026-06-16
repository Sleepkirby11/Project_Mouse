using UnityEngine;
using UnityEngine.EventSystems;

public class Pal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int stateNum;
    public void OnPointerExit(PointerEventData eventData)
    {

    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        UI.Instance.ChangeStance(stateNum);
    }
}
