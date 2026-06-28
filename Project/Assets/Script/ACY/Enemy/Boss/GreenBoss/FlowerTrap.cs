using System.Collections;
using UnityEngine;

/// <summary>
/// 땅에서 자라나 플레이어를 속박하는 덩굴 함정
/// GreenBossAttack에서 PoolingManager.Instance.Get()으로 꺼낸 후 Init() 호출
/// 흐름 : bloom → 꽃Idle(감지 대기) → Grow(자라남) → Vined(속박 유지) → Vanish → Return
/// </summary>
public class FlowerTrap : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private Vector2 detectBoxSize = new Vector2(1.5f, 0.5f);
    [SerializeField] private Vector2 detectBoxOffset = new Vector2(0f, 0.3f);
    [SerializeField] private LayerMask playerLayer;

    [Header("속박 설정")]
    [SerializeField] private float bindDuration = 3f;   // 속박 지속 시간(초)

    [Header("자물쇠 아이콘")]
    [SerializeField] private string lockIconPoolKey = "FlowerLockIcon"; // PoolingManager 키
    [SerializeField] private Vector2 lockIconOffset = new Vector2(0f, 2f);

    [Header("나무 오프셋")]
    [SerializeField] private float grownOffsetY = 0.5f;  // 나무 상태 Y 오프셋

    [Header("지속 데미지")]
    [SerializeField] private int dotDamage = 1;        // 틱당 데미지
    [SerializeField] private float dotInterval = 0.5f;

    [Header("꽃 대기 설정")]
    [SerializeField] private float flowerIdleTimeout = 5f;

    // PoolingManager 반납용 키 (Init()에서 받음)
    private string myPoolKey;

    private Animator anim;
    private bool isDetecting = false;
    private bool isReturning = false;
    private GameObject lockIconInstance;
    private PlayerStatus boundPlayer;

    // 애니메이션 State 
    private const string S_BLOOM = "bloom";
    private const string S_FLOWERIDL = "idle";
    private const string S_GROW = "grow";   // 자라나며 속박 진입 (1회)
    private const string S_VINED = "vined";  // 속박 유지 (반복)
    private const string S_VANISH = "vanish";
    private const string S_LOCK = "Lock";
    private const string S_LOCKOFF = "LockOff";
    private const string S_FLOWERVANISH = "flowerVanish"; // 꽃 상태에서 사라짐

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }
    // ────────────────────────────────────────────
    // 외부 진입점

    public void Init(string poolKey)
    {
        myPoolKey = poolKey;
        isDetecting = false;
        isReturning = false;
        boundPlayer = null;

        if (lockIconInstance != null)
        {
            lockIconInstance.SetActive(false);
            lockIconInstance = null;
        }

        StartCoroutine(BloomSequence());
    }

    private IEnumerator VanishSequence(bool isFlower = false)
    {
        if (isReturning)
        {
            yield break;
        }
        isReturning = true;

        string vanishClip = isFlower ? S_FLOWERVANISH : S_VANISH;
        anim.Play(vanishClip);
        yield return new WaitForSeconds(GetClipLength(vanishClip));
        ReturnToPool();
    }

    // ────────────────────────────────────────────
    // 시퀀스
    // ────────────────────────────────────────────

    private IEnumerator BloomSequence()
    {
        // 꽃 자라남
        anim.Play(S_BLOOM);
        yield return new WaitForSeconds(GetClipLength(S_BLOOM));

        // 꽃 Idle — 감지 시작
        anim.Play(S_FLOWERIDL);
        isDetecting = true;

        yield return new WaitForSeconds(flowerIdleTimeout);

        // 아직 감지 중이면 꽃 상태로 사라짐
        if (isDetecting)
        {
            isDetecting = false;
            StartCoroutine(VanishSequence(true));
        }
    }

    private IEnumerator TriggerSequence(Transform playerTransform)
    {
        isDetecting = false;

        Animator playerAnim = playerTransform.GetComponentInParent<Animator>();

        transform.position += new Vector3(0f, grownOffsetY, 0f);

        // 나무 자라남
        anim.Play(S_GROW);
        yield return new WaitForSeconds(GetClipLength(S_GROW));

        // Grow 끝난 후 박스 안에 플레이어가 있는지 재확인
        Vector2 origin = (Vector2)transform.position + detectBoxOffset;
        Collider2D hit = Physics2D.OverlapBox(origin, detectBoxSize, 0f, playerLayer);

        if (hit == null)
        {
            // 플레이어가 피했으면 그냥 사라짐
            anim.Play(S_VANISH);
            yield return new WaitForSeconds(GetClipLength(S_VANISH));
            ReturnToPool();
            yield break;
        }

        IBindable bindable = hit.GetComponentInParent<IBindable>();
        if (bindable == null)
        {
            anim.Play(S_VANISH);
            yield return new WaitForSeconds(GetClipLength(S_VANISH));
            ReturnToPool();
            yield break;
        }

        boundPlayer = hit.GetComponentInParent<PlayerStatus>();

        // 속박
        anim.Play(S_VINED);
        bindable.ApplyBind(bindDuration);
        ShowLockIcon(hit.transform);


        IDamageable damageable = hit.GetComponentInParent<IDamageable>();
        Coroutine dotRoutine = null;
        if (damageable != null)
        {
            dotRoutine = StartCoroutine(DotRoutine(damageable, playerAnim));
        }

        yield return new WaitForSeconds(bindDuration);

        if (dotRoutine != null)
        {
            StopCoroutine(dotRoutine);
        }

        HideLockIcon();

        anim.Play(S_VANISH);
        yield return new WaitForSeconds(GetClipLength(S_VANISH));

        ReturnToPool();
    }
    private IEnumerator DotRoutine(IDamageable damageable, Animator playerAnim)
    {
        while (true)
        {
            yield return new WaitForSeconds(dotInterval);

            damageable.TakeDamage(dotDamage);
        }
    }
    // ────────────────────────────────────────────
    // 감지 (FixedUpdate)
    // ────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (!isDetecting)
        {
            return;
        }

        Vector2 origin = (Vector2)transform.position + detectBoxOffset;
        Collider2D hit = Physics2D.OverlapBox(origin, detectBoxSize, 0f, playerLayer);

        if (hit == null)
        {
            return;
        }

        isDetecting = false;
        StartCoroutine(TriggerSequence(hit.transform));
    }

    // ────────────────────────────────────────────
    // 자물쇠 아이콘
    // ────────────────────────────────────────────

    private void ShowLockIcon(Transform playerTransform)
    {
        Vector3 spawnPos = playerTransform.position + (Vector3)lockIconOffset;
        lockIconInstance = PoolingManager.Instance.Get(lockIconPoolKey, spawnPos, Quaternion.identity);

        if (lockIconInstance == null)
        {
            return;
        }
        lockIconInstance.transform.SetParent(playerTransform);

        Animator lockAnim = lockIconInstance.GetComponent<Animator>();
        if (lockAnim != null)
        {
            lockAnim.Play(S_LOCK);
        }
    }

    private void HideLockIcon()
    {
        if (lockIconInstance == null)
        {
            return;
        }

        Animator lockAnim = lockIconInstance.GetComponent<Animator>();
        if (lockAnim != null)
        {
            lockAnim.Play(S_LOCKOFF);
            StartCoroutine(ReturnLockIconDelayed(GetClipLength(S_LOCKOFF)));
        }
        else
        {
            PoolingManager.Instance.Return(lockIconPoolKey, lockIconInstance);
            lockIconInstance = null;
        }
    }

    private IEnumerator ReturnLockIconDelayed(float delay)
    {
        GameObject icon = lockIconInstance;
        lockIconInstance = null;

        yield return new WaitForSeconds(delay);

        if (icon != null)
        {
            icon.transform.SetParent(PoolingManager.Instance.transform);
            PoolingManager.Instance.Return(lockIconPoolKey, icon);
        }
    }

    // ────────────────────────────────────────────
    // 풀 반납
    // ────────────────────────────────────────────

    public void ForceClear()
    {
        if (isReturning) return;

        if (boundPlayer != null)
        {
            boundPlayer.ReleaseBind();
            boundPlayer = null;
        }
        HideLockIcon();

        bool isFlower = isDetecting;
        isDetecting = false;

        StopAllCoroutines();

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(VanishSequence(isFlower));
        }
        else
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        StopAllCoroutines();
        isDetecting = false;
        isReturning = false;
        boundPlayer = null;
        if (PoolingManager.Instance != null)
        {
            PoolingManager.Instance.Return(myPoolKey, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private float GetClipLength(string clipName)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
        {
            return 0.5f;
        }

        foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip.length;
            }
        }
        return 0.5f;
    }

    // ────────────────────────────────────────────
    // 에디터 기즈모
    // ────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.4f);
        Gizmos.DrawCube((Vector2)transform.position + detectBoxOffset, detectBoxSize);
    }
}