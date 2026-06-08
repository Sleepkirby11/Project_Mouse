using System.Collections;
using UnityEngine;

public class Frog : MonoBehaviour
{
    [Header("대미지 설정")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float hitboxActivateDelay = 0.5f;  // 침 공격 타이밍
    [SerializeField] private float hitboxDuration = 0.3f;       // 판정 유지 시간
    [SerializeField] private float animDuration = 2.0f;


    private Animator anim;
    private const string POOL_KEY = "GreenBossFrog";
    private BoxCollider2D hitbox;

    private void Awake()
    {
        hitbox = GetComponent<BoxCollider2D>();
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

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
        StartCoroutine(FrogRoutine());
    }

    private IEnumerator FrogRoutine()
    {
        // 공격 타이밍에 판정 ON
        yield return new WaitForSeconds(hitboxActivateDelay);
        if (hitbox != null)
        {
            hitbox.enabled = true;
        }
        // 판정 유지
        yield return new WaitForSeconds(hitboxDuration);
        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
        // 애니메이션 끝까지 대기
        yield return new WaitForSeconds(animDuration - hitboxActivateDelay - hitboxDuration);

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