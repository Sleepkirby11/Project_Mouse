using System.Collections;
using UnityEngine;

public class RgbBullet : MonoBehaviour, IDamageable
{
    [Header("탄환 고유 속성 설정")]
    [SerializeField] private EnemyStatus.EnemyElement myElement;

    [Header("대미지 설정")]
    [SerializeField] private int bulletDamage = 15;
    [SerializeField] private float stunDuration = 1f;

    [Header("이동 및 페이드 설정")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float pauseDuration = 0.8f; // 중간 경직 시간
    [SerializeField] private float fadeDuration = 0.3f;  // 페이드아웃 지속 시간

    [Header("반복 설정")]
    [SerializeField] private int maxAttackCount = 3;

    private EnemyStatus.EnemyElement weakElement;
    private string myPoolName;
    private Transform playerTransform;

    private PlayerStatus playerStatus;
    private SpriteRenderer[] srs;
    private Collider2D bulletCollider;
    private bool destroyed = false;

    public System.Action onHitPlayer;
    private void Awake()
    {
        playerStatus = GameObject.FindWithTag("Player")?.GetComponent<PlayerStatus>();
        srs = GetComponentsInChildren<SpriteRenderer>();
        bulletCollider = GetComponent<Collider2D>();

        // 상성 공식 설정
        switch (myElement)
        {
            case EnemyStatus.EnemyElement.Red:
                weakElement = EnemyStatus.EnemyElement.Blue;
                break;
            case EnemyStatus.EnemyElement.Blue:
                weakElement = EnemyStatus.EnemyElement.Green;
                break;
            case EnemyStatus.EnemyElement.Green:
                weakElement = EnemyStatus.EnemyElement.Red;
                break;
        }
    }

    public void Initialize(Transform player, string poolName)
    {
        // 이전 코루틴 완벽 정지
        StopAllCoroutines();

        playerTransform = player;
        myPoolName = poolName;
        destroyed = false;

        if (bulletCollider != null) bulletCollider.enabled = true; // 콜라이더 복구
        ResetAlpha(); // 알파값 복구

        StartCoroutine(BulletMovementRoutine());
    }

    private IEnumerator BulletMovementRoutine()
    {
        if (playerTransform == null) yield break;

        // 설정한 횟수만큼 [일직선 직진 -> 경직] 반복
        for (int i = 0; i < maxAttackCount; i++)
        {
            if (destroyed || playerTransform == null) yield break;

            // 경직이 풀린 '이 순간'의 플레이어 방향을 조준
            Vector3 fixedDirection = (playerTransform.position - transform.position).normalized;

            // --- 일직선 발사 단계 ---
            float moveTime = 1.0f;
            float elapsed = 0f;

            while (elapsed < moveTime && !destroyed)
            {
                transform.Translate(fixedDirection * moveSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // --- 잠깐 경직 단계 ---
            if (i < maxAttackCount - 1 && !destroyed)
            {
                yield return new WaitForSeconds(pauseDuration);
            }
        }

        if (!destroyed)
        {
            StartFadeOut();
        }
    }

    // 플레이어 공격을 받았을 때 (IDamageable)
    public void TakeDamage(int damage)
    {
        if (destroyed) return;
        if (playerStatus == null) return;

        bool isWeaknessHit = false;

        switch (weakElement)
        {
            case EnemyStatus.EnemyElement.Red:
                if (playerStatus.currentStance == PlayerStatus.Stance.Red) isWeaknessHit = true;
                break;
            case EnemyStatus.EnemyElement.Green:
                if (playerStatus.currentStance == PlayerStatus.Stance.Green) isWeaknessHit = true;
                break;
            case EnemyStatus.EnemyElement.Blue:
                if (playerStatus.currentStance == PlayerStatus.Stance.Blue) isWeaknessHit = true;
                break;
        }

        if (isWeaknessHit)
        {
            StartFadeOut();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (destroyed) return;

        if (collision.CompareTag("Player"))
        {
            IDamageable damageable = collision.GetComponent<IDamageable>();
            damageable?.TakeDamage(bulletDamage);

            onHitPlayer?.Invoke();

            // 블루 - 스턴
            if (myElement == EnemyStatus.EnemyElement.Blue)
            {
                if (collision.TryGetComponent(out IStunnable stunnable))
                    stunnable.ApplyStun(stunDuration);
            }

            destroyed = true;
            StopAllCoroutines();
            PoolingManager.Instance.Return(myPoolName, gameObject);
        }
    }

    private void StartFadeOut()
    {
        destroyed = true;
        if (bulletCollider != null) bulletCollider.enabled = false; // 추가 충돌 차단

        StopAllCoroutines(); // 이동 관련 루틴 모두 정지
        StartCoroutine(FadeAndReturnRoutine()); // 제자리에 멈춰서 페이드아웃 시작
    }

    // 망치 스크립트 스타일의 페이드아웃 루틴
    private IEnumerator FadeAndReturnRoutine()
    {
        float elapsed = 0f;
        Color[] originalColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null) originalColors[i] = srs[i].color;
        }

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] == null) continue;
                srs[i].color = new Color(originalColors[i].r, originalColors[i].g, originalColors[i].b, alpha);
            }
            yield return null;
        }

        // 완전히 투명해지면 오브젝트 풀로 반환
        PoolingManager.Instance.Return(myPoolName, gameObject);
    }

    private void ResetAlpha()
    {
        if (srs == null) return;
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f);
        }
    }
}