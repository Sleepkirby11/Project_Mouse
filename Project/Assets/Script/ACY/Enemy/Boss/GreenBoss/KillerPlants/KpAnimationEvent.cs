using UnityEngine;

public class KpAnimationEvent : MonoBehaviour
{
    private KillerPlantAttack parentAttack;

    void Start()
    {
        // �θ� ������Ʈ�� �ִ� KillerPlantAttack�� ������
        parentAttack = GetComponentInParent<KillerPlantAttack>();
    }

    // �ڽ��� �ִϸ��̼� �̺�Ʈ�� �� �Լ����� ȣ���ϸ� �θ𿡰� ����
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
