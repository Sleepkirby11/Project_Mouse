using System;
using UnityEngine;

public class CastingSpell : MonoBehaviour
{
    private Action onComplete;
    private Animator anim;

    public void Init(Action onCompleteCallback)
    {
        onComplete = onCompleteCallback;
        anim = GetComponent<Animator>();
    }

    // �ִϸ��̼� ������ �����ӿ� �̺�Ʈ�� ȣ��
    public void OnEffectEnd()
    {
        onComplete?.Invoke();
        Destroy(gameObject);
    }
}
