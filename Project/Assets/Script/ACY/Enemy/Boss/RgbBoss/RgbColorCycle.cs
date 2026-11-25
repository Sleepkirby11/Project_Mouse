using System.Collections;
using UnityEngine;

public class RgbColorCycle : MonoBehaviour
{
    #region Inspector Fields
    [Header("Animator Overrides")]
    [SerializeField] private RuntimeAnimatorController baseController; // Red
    [SerializeField] private AnimatorOverrideController greenOverride;
    [SerializeField] private AnimatorOverrideController blueOverride;
    [SerializeField] private AnimatorOverrideController magentaOverride;
    #endregion

    #region Private Fields
    private bool isFinalPhase = false;
    private bool pendingIntroTransition = false;
    private bool usedIntroTransition = false;
    private EnemyStatus enemyStatus;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Coroutine introTransitionCoroutine;

    private const float IntroCastSwapNormalizedTime = 0.45f;
    private const float IntroCastFallbackDuration = 2.6f;

    public bool IsTransitioning { get; private set; }
    public bool IsTransitioningOrPending => IsTransitioning || pendingIntroTransition;
    public bool UsedIntroTransition => usedIntroTransition;

    private static readonly int CastingTrigger = Animator.StringToHash("Casting");
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        CacheComponents();

        EnemyStatus.EnemyElement first = (EnemyStatus.EnemyElement)Random.Range(0, 3);
        if (pendingIntroTransition)
        {
            pendingIntroTransition = false;
            usedIntroTransition = true;
            introTransitionCoroutine = StartCoroutine(TransitionFromMagentaRoutine(first));
            return;
        }

        enemyStatus.SetElement(first);
        ApplyController(GetController(first));
    }

    private void OnDisable()
    {
        StopIntroTransition();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 컷씬 중 보라색(마젠타) 대기 외형으로 고정 표시하고 컷씬 종료 후 변환 연출을 예약합니다.
    /// </summary>
    public void ShowCutsceneMagentaIdle()
    {
        CacheComponents();
        StopIntroTransition();

        pendingIntroTransition = true;
        if (enemyStatus != null)
        {
            enemyStatus.SetElement(EnemyStatus.EnemyElement.None);
        }
        ApplyController(GetController(EnemyStatus.EnemyElement.None), "Idle");
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }

    public void ChangeElementRandom()
    {
        StopIntroTransition();

        EnemyStatus.EnemyElement current = enemyStatus.CurrentElement;
        EnemyStatus.EnemyElement next;

        do
        {
            next = (EnemyStatus.EnemyElement)Random.Range(0, 3);
        }
        while (next == current);

        enemyStatus.SetElement(next);
        ApplyController(GetController(next), "Casting");
    }

    public void EnterFinalPhase()
    {
        if (isFinalPhase)
            return;

        StopIntroTransition();
        isFinalPhase = true;

        enemyStatus.SetElement(EnemyStatus.EnemyElement.None);
        ApplyController(GetController(EnemyStatus.EnemyElement.None), "Casting");
    }

    public void ExitFinalPhase()
    {
        if (!isFinalPhase)
            return;

        isFinalPhase = false;

        // 발악 끝나면 즉시 RGB 하나 선택하며 사이클 재개
        ChangeElementRandom();
    }
    #endregion

    #region Private Methods
    private RuntimeAnimatorController GetController(EnemyStatus.EnemyElement element)
    {
        return element switch
        {
            EnemyStatus.EnemyElement.Red => baseController,
            EnemyStatus.EnemyElement.Green => greenOverride,
            EnemyStatus.EnemyElement.Blue => blueOverride,
            EnemyStatus.EnemyElement.None => magentaOverride, // 발악 상태
            _ => baseController
        };
    }

    private void CacheComponents()
    {
        if (enemyStatus == null) enemyStatus = GetComponent<EnemyStatus>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void StopIntroTransition()
    {
        if (introTransitionCoroutine != null)
        {
            StopCoroutine(introTransitionCoroutine);
            introTransitionCoroutine = null;
        }
        IsTransitioning = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }

    private IEnumerator TransitionFromMagentaRoutine(EnemyStatus.EnemyElement targetElement)
    {
        IsTransitioning = true;

        if (enemyStatus != null)
        {
            enemyStatus.SetElement(EnemyStatus.EnemyElement.None);
        }

        ApplyController(GetController(EnemyStatus.EnemyElement.None), "Casting", 0f);

        Color bridgeTint = GetTransitionTint(targetElement);
        float elapsed = 0f;
        bool swapped = false;

        while (elapsed < IntroCastFallbackDuration)
        {
            elapsed += Time.deltaTime;
            float normTime = GetCastingNormalizedTime(elapsed);

            if (!swapped)
            {
                if (spriteRenderer != null)
                {
                    float t = Mathf.Clamp01((normTime - 0.2f) / (IntroCastSwapNormalizedTime - 0.2f));
                    spriteRenderer.color = Color.Lerp(Color.white, bridgeTint, t);
                }

                if (normTime >= IntroCastSwapNormalizedTime)
                {
                    swapped = true;
                    if (enemyStatus != null)
                    {
                        enemyStatus.SetElement(targetElement);
                    }
                    ApplyController(GetController(targetElement), "Casting", normTime);
                }
            }
            else
            {
                if (spriteRenderer != null)
                {
                    float settle = Mathf.Clamp01((normTime - IntroCastSwapNormalizedTime) / 0.22f);
                    spriteRenderer.color = Color.Lerp(bridgeTint, Color.white, settle);
                }

                if (animator == null || (elapsed > 1.2f && !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")))
                {
                    break;
                }
            }

            yield return null;
        }

        if (!swapped && enemyStatus != null)
        {
            enemyStatus.SetElement(targetElement);
            ApplyController(GetController(targetElement), "Idle", 0f);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        IsTransitioning = false;
        introTransitionCoroutine = null;
    }

    private float GetCastingNormalizedTime(float elapsed)
    {
        if (animator != null)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Casting"))
            {
                return state.normalizedTime;
            }
        }
        return elapsed / 2.4166665f;
    }

    private static Color GetTransitionTint(EnemyStatus.EnemyElement element)
    {
        return element switch
        {
            EnemyStatus.EnemyElement.Red => new Color(1f, 0.72f, 0.85f, 1f),
            EnemyStatus.EnemyElement.Green => new Color(0.8f, 1f, 0.9f, 1f),
            EnemyStatus.EnemyElement.Blue => new Color(0.75f, 0.85f, 1f, 1f),
            _ => Color.white
        };
    }

    private void ApplyController(RuntimeAnimatorController targetController, string stateName = "Idle", float normalizedTime = 0f)
    {
        if (animator == null || targetController == null) return;

        animator.runtimeAnimatorController = targetController;
        animator.Update(0f);
        animator.Play(stateName, 0, normalizedTime);
        animator.Update(0f);
    }
    #endregion
}