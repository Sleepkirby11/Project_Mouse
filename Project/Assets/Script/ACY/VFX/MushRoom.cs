using System.Collections;
using UnityEngine;

public class PoisonMushroom : MonoBehaviour, IDamageable
{
    #region Inspector Fields
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
    #endregion

    #region Private Fields
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D mushroomCollider;

    private bool destroyed;
    private Color originalColor;
    private Coroutine lifeCoroutine;
    private PlayerStatus playerStatus;
    #endregion

    #region Unity Lifecycle
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
    #endregion

    #region Mushroom Routines
    private IEnumerator MushroomLifeRoutine()
    {
        // 1. 대기 시간만큼 대기 (자라나는 중)
        yield return new WaitForSeconds(timeToExplode);

        if (destroyed) yield break;

        // 2. 폭발 시작 (콜라이더 비활성화 및 폭발 애니메이션 재생)
        if (mushroomCollider != null) mushroomCollider.enabled = false;
        if (animator != null) animator.SetTrigger("Explode");

        // 이 후 폭발 및 이펙트 소멸 처리는 애니메이션 클립 내부의 이벤트를 통해 아래 메서드들을 호출합니다.
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
    #endregion

    #region Animation Events
    // 애니메이션 이벤트 1: 폭발 타격 시점에 호출
    public void ApplyExplosionDamage()
    {
        if (destroyed) return; // 플레이어가 적을 파괴했다면 폭발 데미지 없음

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

    // 애니메이션 이벤트 2: 폭발 애니메이션이 종료될 때 호출
    public void FinishExplosion()
    {
        if (destroyed) return;

        destroyed = true;
        SpawnPoisonArea(); // 독 장판을 깔며 풀로 반환
    }
    #endregion

    #region IDamageable Implementation
    public void TakeDamage(int damage)
    {
        if (destroyed) return;

        // 플레이어의 스탠스가 Red 상태일 때만 파괴 가능
        if (playerStatus == null || playerStatus.currentStance != PlayerStatus.Stance.Red) return;

        destroyed = true;

        if (lifeCoroutine != null)
        {
            StopCoroutine(lifeCoroutine);
            lifeCoroutine = null;
        }

        if (mushroomCollider != null) mushroomCollider.enabled = false;

        // 애니메이터 비활성화하여 폭발 애니메이션 재생 차단
        if (animator != null) animator.enabled = false;

        StartCoroutine(FadeAndReturnRoutine());
    }
    #endregion

    #region Editor Editor Gizmos
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
    #endregion
}