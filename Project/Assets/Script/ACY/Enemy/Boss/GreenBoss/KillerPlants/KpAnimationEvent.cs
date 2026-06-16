using UnityEngine;

public class KpAnimationEvent : MonoBehaviour
{
    private KillerPlantAttack parentAttack;

    void Start()
    {
        // 부모 오브젝트에 있는 KillerPlantAttack를 가져옴
        parentAttack = GetComponentInParent<KillerPlantAttack>();
    }

    // 자식의 애니메이션 이벤트가 이 함수들을 호출하면 부모에게 보냄
    public void OnMeleeHit()
    {
        if (parentAttack != null)
        {
            parentAttack.OnMeleeHit();
        }
    }

    public void OnMeleeEnd()
    {
        if (parentAttack != null)
        {
            parentAttack.OnMeleeEnd();

        }
    }
}
