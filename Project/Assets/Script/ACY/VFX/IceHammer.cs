using System.Collections;
using UnityEngine;

public class IceHammer : MonoBehaviour, IDamageable
{
    [Header("Ice Spawn")]
    [SerializeField] private float iceSpacing = 1.5f; // 얼음 사이 간격
    [SerializeField] private int iceCount = 5; // 얼음 개수
    [SerializeField] private Vector2 iceOffset; // 망치와 얼음 사이 간격

    [Header("Hitbox Setup")]
    [SerializeField] private Collider2D hammerHitbox; // 히트박스 (플레이어의 공격이 맞는 부분)
    [SerializeField] private float fadeDuration = 0.5f; // 페이드아웃 시간

    private bool destroyed;
    private PlayerStatus player;
    private Animator anim;     
    private SpriteRenderer[] srs;
    private void Awake()
    {
        player = GameObject.FindWithTag("Player") ?.GetComponent<PlayerStatus>();
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

    public void TakeDamage(int damage)
    {
        Debug.Log("망치가 맞긴 맞았음!");
        if (destroyed)
            return;

        if (player == null)
            return;

        // Red 상태에서만 파괴 가능
        if (player.currentStance != PlayerStatus.Stance.Red)
            return;

        destroyed = true;

        DisableHitbox();
        {
            anim.enabled = false; // 애니메이터 컴포넌트를 꺼서 즉시 중단
        }
        StartCoroutine(FadeAndReturnRoutine());
    }
    public void DisableHitbox() // 히트박스 비활성화 (애니메이션 이벤트)
    {
        if (hammerHitbox != null)
        {
            hammerHitbox.enabled = false;
        }
    }

    // 애니메이션 마지막 프레임 이벤트
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
                "Ice",
                pos,
                Quaternion.identity);

            yield return new WaitForSeconds(0.1f);
        }

        PoolingManager.Instance.Return(
            "IceHammer",
            gameObject);
    }
    private IEnumerator FadeAndReturnRoutine()
    {
        float elapsed = 0f;

        // 초기 알파 상태 저장용 배열
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

        // 투명화 완료 후 오브젝트 풀로 반환
        PoolingManager.Instance.Return("IceHammer", gameObject);
    }

    private void ResetAlpha()
    {
        if (srs == null) return;
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            Color c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 1f); // 알파값을 다시 1로
        }
    }
}