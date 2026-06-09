using System.Collections;
using UnityEngine;

public class Shockwave : MonoBehaviour
{
    [SerializeField] private string poolKey = "Shockwave";
    private Animator anim;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        StartCoroutine(PlayAndReturn());
    }

    private IEnumerator PlayAndReturn()
    {
        if (anim != null)
        {
            anim.Play(0, -1, 0f); // 애니메이션 처음부터 재생
            yield return null; // 한 프레임 대기
            float length = anim.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(length);
        }

        PoolingManager.Instance.Return(poolKey, gameObject);
    }
}