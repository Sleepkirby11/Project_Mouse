using System.Collections;
using UnityEngine;

public class PoisonMushroom : MonoBehaviour, IDamageable
{
    [Header("Pool Keys")]
    [SerializeField] private string mushroomKey = "PoisonMushroom";
    [SerializeField] private string poisonAreaKey = "PoisonArea";

    [Header("Life Settings")]
    [SerializeField] private float timeToExplode = 3f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float poisonAreaOffsetY = 0f;

    [Header("Explosion Damage")]
    [SerializeField] private float explosionRadius = 1.5f;
    [SerializeField] private int explosionDamage = 15;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D mushroomCollider;

    private bool destroyed;
    private Color originalColor;
    private Coroutine lifeCoroutine;
    private PlayerStatus playerStatus;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        mushroomCollider = GetComponent<Collider2D>();

        playerStatus = GameObject.FindWithTag("Player")?.GetComponent<PlayerStatus>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void OnEnable()
    {
        destroyed = false;

        if (mushroomCollider != null) mushroomCollider.enabled = true;
        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        lifeCoroutine = StartCoroutine(MushroomLifeRoutine());
    }

    private IEnumerator MushroomLifeRoutine()
    {
        // 1. 설정한 시간만큼 대기 (자라나는 중)
        yield return new WaitForSeconds(timeToExplode);

        if (destroyed) yield break;

        // 2. 폭발 시작 (히트박스 무적 처리 및 애니메이션 재생)
        if (mushroomCollider != null) mushroomCollider.enabled = false;
        if (animator != null) animator.SetTrigger("Explode");

        // ★ 코루틴은 여기서 끝!
        // 이후의 대미지와 장판 생성은 애니메이션 이벤트가 알아서 호출합니다.
    }

    // ==============================================================
    // ★ 애니메이션 이벤트 1: 파편이 튀는 (대미지를 주고 싶은) 정확한 프레임에 호출
    // ==============================================================
    public void ApplyExplosionDamage()
    {
        if (destroyed) return; // 플레이어가 때려서 이미 파괴 중이면 무시

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(explosionDamage);
                }
            }
        }
    }

    // ==============================================================
    // ★ 애니메이션 이벤트 2: 터지는 애니메이션의 "맨 마지막 프레임"에 호출
    // ==============================================================
    public void FinishExplosion()
    {
        if (destroyed) return;

        destroyed = true;
        SpawnPoisonArea(); // 장판 생성 후 풀로 자동 반납
    }

    public void TakeDamage(int damage)
    {
        if (destroyed) return;

        if (playerStatus == null || playerStatus.currentStance != PlayerStatus.Stance.Red) return;

        destroyed = true;

        if (lifeCoroutine != null)
        {
            StopCoroutine(lifeCoroutine);
            lifeCoroutine = null;
        }

        if (mushroomCollider != null) mushroomCollider.enabled = false;

        // 애니메이터를 아예 꺼버리기 때문에, 진행 중이던 애니메이션 이벤트도 자동으로 취소되어 안전합니다.
        if (animator != null) animator.enabled = false;

        StartCoroutine(FadeAndReturnRoutine());
    }

    private IEnumerator FadeAndReturnRoutine()
    {
        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;

            while (elapsed < fadeDuration)
            {
                if (!gameObject.activeSelf) yield break;

                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }
        PoolingManager.Instance.Return(mushroomKey, gameObject);
    }

    private void SpawnPoisonArea()
    {
        Vector3 areaSpawnPos = transform.position + new Vector3(0f, poisonAreaOffsetY, 0f);
        PoolingManager.Instance.Get(poisonAreaKey, areaSpawnPos, Quaternion.identity);
        PoolingManager.Instance.Return(mushroomKey, gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}