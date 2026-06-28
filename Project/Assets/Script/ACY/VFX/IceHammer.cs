using System.Collections;
using UnityEngine;

public class IceHammer : MonoBehaviour, IDamageable
{
    #region Inspector Fields
    [Header("Ice Spawn")]
    [SerializeField] private float iceSpacing = 1.5f; // 얼음 기둥 간격
    [SerializeField] private int iceCount = 5; // 얼음 생성 개수
    [SerializeField] private Vector2 iceOffset; // 생성 위치 오프셋

    [Header("Hitbox Setup")]
    [SerializeField] private Collider2D hammerHitbox; // 타격 판정 콜라이더 (플레이어가 닿는 영역)
    [SerializeField] private float fadeDuration = 0.5f; // 페이드아웃 지속 시간

    [Header("Pool Settings")]
    [SerializeField] private string poolKey = "IceHammer";
    [SerializeField] private string icePoolKey = "Ice";
    #endregion

    #region Private Fields
    private bool destroyed;
    private PlayerStatus player;
    private Animator anim;
    private SpriteRenderer[] srs;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        player = GameObject.FindWithTag("Player")?.GetComponent<PlayerStatus>();
        anim = GetComponentInChildren<Animator>();
        srs = GetComponentsInChildren<SpriteRenderer>();
        if (hammerHitbox == null)
        {
            hammerHitbox = GetComponent<Collider2D>();
        }
    }

    private void OnEnable()
    {
        destroyed = false;
        srs = GetComponentsInChildren<SpriteRenderer>();

        if (hammerHitbox != null)
        {
            hammerHitbox.enabled = true;
        }
        if (anim != null) anim.enabled = true;
        ResetAlpha();
    }
    #endregion

    #region IDamageable Implementation
    public void TakeDamage(int damage)
    {
        if (destroyed)
            return;

        if (player == null)
            return;

        // 초록(Green) 스탠스인 상태의 플레이어만 얼음망치를 때려 파괴할 수 있음
        if (player.currentStance != PlayerStatus.Stance.Green)
            return;

        destroyed = true;

        DisableHitbox();
        if (anim != null)
        {
            anim.enabled = false; // 파괴 시 애니메이션 재생 즉시 중단
        }
        StartCoroutine(FadeAndReturnRoutine());
    }
    #endregion

    #region Public Utility Methods
    public void DisableHitbox() // 히트박스 비활성화 (애니메이션 이벤트)
    {
        if (hammerHitbox != null)
        {
            hammerHitbox.enabled = false;
        }
    }
    #endregion

    #region Ice Spawn & Return Routines
    // 애니메이션 이벤트에서 호출 (얼음망치가 바닥을 칠 때)
    public void SpawnIce()
    {
        if (destroyed)
            return;

        destroyed = true;

        StartCoroutine(SpawnIceAndReturn());
    }

    private IEnumerator SpawnIceAndReturn()
    {
        int half = iceCount / 2;

        Vector3 center =
            transform.position +
            (Vector3)iceOffset;

        for (int i = -half; i <= half; i++)
        {
            Vector3 pos =
                center +
                Vector3.right * (i * iceSpacing);

            PoolingManager.Instance.Get(
                icePoolKey,
                pos,
                Quaternion.identity);

            yield return new WaitForSeconds(0.1f);
        }

        PoolingManager.Instance.Return(
            poolKey,
            gameObject);
    }

    private IEnumerator FadeAndReturnRoutine()
    {
        float elapsed = 0f;

        // 모든 자식 스프라이트의 초기 알파 상태를 기록
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

        // 완전히 페이드아웃되면 오브젝트 풀에 반환
        PoolingManager.Instance.Return(poolKey, gameObject);
    }

    private void ResetAlpha()
    {
        if (srs == null) return;
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f); // 알파값을 다시 1로 초기화
        }
    }
    #endregion
}