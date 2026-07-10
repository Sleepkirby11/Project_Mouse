using System.Collections;
using UnityEngine;

public class Frog : MonoBehaviour
{
    [Header("대미지 설정")]
    [SerializeField] private int damage = 1;

    private Animator anim;
    private const string POOL_KEY = "GreenBossFrog";

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void OnDisable()
    {
        if (anim != null)
        {
            anim.Play("Frog", 0, 0f); // 반환 시 애니메이션 처음으로 리셋
            anim.Update(0f);
            anim.enabled = false;     // 애니메이터 비활성화
        }
    }

    private void OnEnable()
    {
        if (anim != null)
        {
            anim.enabled = true;
        }

        StartCoroutine(FrogRoutine());
    }

    private IEnumerator FrogRoutine()
    {
        float duration = 1.6f; // 기본 폴백값
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
            if (clips.Length > 0)
            {
                duration = clips[0].length;
            }
        }

        // 애니메이션 재생 시간 동안 대기한 후 풀로 반환
        yield return new WaitForSeconds(duration);

        PoolingManager.Instance.Return(POOL_KEY, gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);
        }
    }
}