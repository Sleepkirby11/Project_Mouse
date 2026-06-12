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

    // 애니메이션 마지막 프레임에 이벤트로 호출
    public void OnEffectEnd()
    {
        onComplete?.Invoke();
        Destroy(gameObject);
    }
}
