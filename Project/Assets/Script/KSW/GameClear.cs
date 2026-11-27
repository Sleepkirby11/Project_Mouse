using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameClear : MonoBehaviour
{
    private const string DefaultChromaText =
        "<color=#F5938C>C</color> " +
        "<color=#F7C686>H</color> " +
        "<color=#9FE09B>R</color> " +
        "<color=#94DCE6>O</color> " +
        "<color=#9AB8F5>M</color> " +
        "<color=#CDA6EB>A</color>";

    [Header("Scene Navigation")]
    public string titleSceneName = "MainMenu";

    [Header("Ending References")]
    [SerializeField] private RectTransform backgroundTransform;
    [SerializeField] private Image cinematicScrim;
    [SerializeField] private RectTransform motesContainer;
    [SerializeField] private CanvasGroup chromaGroup;
    [SerializeField] private Text chromaTitleText;
    [SerializeField] private CanvasGroup thankYouGroup;
    [SerializeField] private Text thankYouText;
    [SerializeField] private CanvasGroup buttonGroup;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button quitButton;

    [Header("Timing")]
    [SerializeField] private float initialDelay = 0.45f;
    [SerializeField] private float chromaFadeIn = 1.0f;
    [SerializeField] private float chromaHold = 1.55f;
    [SerializeField] private float chromaFadeOut = 0.9f;
    [SerializeField] private float betweenBeatPause = 0.25f;
    [SerializeField] private float thankYouFadeIn = 0.95f;
    [SerializeField] private float thankYouHold = 1.85f;
    [SerializeField] private float thankYouFadeOut = 0.9f;
    [SerializeField] private float buttonsFadeIn = 0.8f;

    [Header("Motion & Atmosphere")]
    [SerializeField] private float scrimStartAlpha = 0.82f;
    [SerializeField] private float scrimDawnAlpha = 0.48f;
    [SerializeField] private float backgroundStartScale = 1.07f;
    [SerializeField] private float buttonSlideOffset = 44f;
    [SerializeField] private int ambientMoteCount = 22;

    private struct AmbientMote
    {
        public RectTransform rect;
        public Image image;
        public float speedY;
        public float driftAmp;
        public float driftFreq;
        public float phase;
        public float baseAlpha;
        public float startX;
    }

    private readonly List<AmbientMote> ambientMotes = new List<AmbientMote>();
    private Vector2 buttonsTargetAnchoredPos;
    private Vector2 thankYouTargetAnchoredPos;
    private bool sequenceFinished;
    private bool skipRequested;
    private bool isExitingScene;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null && rootCanvas.sortingOrder < 100)
        {
            rootCanvas.sortingOrder = 100;
        }

        if (UI.Instance != null)
        {
            UI.Instance.HideBossHPBar();
            UI.Instance.HideDialogue();
            UI.Instance.ActivePal(false);
        }

        CacheInitialPositions();
        PrepareInitialVisualState();
        AttachButtonFeedback();
        SpawnAmbientMotes();
    }

    private void Start()
    {
        StartCoroutine(PlayEndingSequence());
    }

    private void Update()
    {
        UpdateAmbientMotes();

        if (!sequenceFinished && !skipRequested && WasSkipInputPressed())
        {
            skipRequested = true;
        }
    }

    public void GoToTitle()
    {
        if (isExitingScene) return;
        isExitingScene = true;
        Time.timeScale = 1f;

        if (GameManager.instance != null)
        {
            GameManager.instance.LoadScene(titleSceneName);
        }
        else
        {
            SceneManager.LoadSceneAsync(titleSceneName);
        }
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료!");
        Application.Quit();
    }

    private void CacheInitialPositions()
    {
        if (buttonGroup != null)
        {
            RectTransform rt = buttonGroup.transform as RectTransform;
            if (rt != null)
            {
                buttonsTargetAnchoredPos = rt.anchoredPosition;
            }
        }

        if (thankYouGroup != null)
        {
            RectTransform rt = thankYouGroup.transform as RectTransform;
            if (rt != null)
            {
                thankYouTargetAnchoredPos = rt.anchoredPosition;
            }
        }
    }

    private void PrepareInitialVisualState()
    {
        if (backgroundTransform != null)
        {
            backgroundTransform.localScale = Vector3.one * backgroundStartScale;
        }

        if (cinematicScrim != null)
        {
            Color c = cinematicScrim.color;
            c.a = scrimStartAlpha;
            cinematicScrim.color = c;
            cinematicScrim.raycastTarget = false;
        }

        Font pixelFont = SettingsUIStyler.GetPixelFont(transform.root);

        if (chromaTitleText != null)
        {
            if (pixelFont != null && chromaTitleText.font == null) chromaTitleText.font = pixelFont;
            chromaTitleText.supportRichText = true;
            chromaTitleText.text = DefaultChromaText;
            chromaTitleText.color = Color.white;
            ApplyTextOutline(chromaTitleText, new Color(0.14f, 0.08f, 0.14f, 0.68f), new Vector2(2f, -2f));
            ApplyTextShadow(chromaTitleText, new Color(0.08f, 0.04f, 0.10f, 0.55f), new Vector2(0f, -6f));
        }

        if (thankYouText != null)
        {
            if (pixelFont != null && thankYouText.font == null) thankYouText.font = pixelFont;
            thankYouText.color = new Color(1f, 0.965f, 0.89f, 1f);
            ApplyTextOutline(thankYouText, new Color(0.14f, 0.08f, 0.14f, 0.72f), new Vector2(1.5f, -1.5f));
            ApplyTextShadow(thankYouText, new Color(0.08f, 0.04f, 0.10f, 0.5f), new Vector2(0f, -4f));
        }

        StyleEndingButtonLabel(titleButton, pixelFont, new Color(0.24f, 0.14f, 0.08f, 0.92f));
        StyleEndingButtonLabel(quitButton, pixelFont, new Color(0.22f, 0.08f, 0.08f, 0.92f));

        SetGroupState(chromaGroup, 0f, false);
        SetGroupState(thankYouGroup, 0f, false);
        SetGroupState(buttonGroup, 0f, false);

        if (buttonGroup != null)
        {
            RectTransform rt = buttonGroup.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = buttonsTargetAnchoredPos - new Vector2(0f, buttonSlideOffset);
            }
        }
    }

    private static void StyleEndingButtonLabel(Button btn, Font pixelFont, Color outlineColor)
    {
        if (btn == null) return;
        Text txt = btn.GetComponentInChildren<Text>(true);
        if (txt == null) return;
        if (pixelFont != null && txt.font == null) txt.font = pixelFont;
        ApplyTextOutline(txt, outlineColor, new Vector2(1.5f, -1.5f));
    }

    private static void ApplyTextOutline(Text txt, Color outlineColor, Vector2 distance)
    {
        if (txt == null) return;
        Outline outline = txt.GetComponent<Outline>();
        if (outline == null) outline = txt.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = outlineColor;
        outline.effectDistance = distance;
    }

    private static void ApplyTextShadow(Text txt, Color shadowColor, Vector2 distance)
    {
        if (txt == null) return;
        Shadow shadow = txt.GetComponent<Shadow>();
        if (shadow == null) shadow = txt.gameObject.AddComponent<Shadow>();
        shadow.enabled = true;
        shadow.effectColor = shadowColor;
        shadow.effectDistance = distance;
    }

    private void AttachButtonFeedback()
    {
        EnsureButtonFeedback(titleButton);
        EnsureButtonFeedback(quitButton);
    }

    private static void EnsureButtonFeedback(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<EndingButtonFeedback>() == null)
        {
            btn.gameObject.AddComponent<EndingButtonFeedback>();
        }
    }

    private IEnumerator PlayEndingSequence()
    {
        float bgZoomDuration = initialDelay + chromaFadeIn + chromaHold + chromaFadeOut
            + betweenBeatPause + thankYouFadeIn + thankYouHold + thankYouFadeOut;
        StartCoroutine(AnimateBackgroundZoom(bgZoomDuration));

        yield return WaitOrSkip(initialDelay);

        // 1. CHROMA 페이드 인 -> 홀드 -> 페이드 아웃
        if (!skipRequested)
        {
            yield return AnimateGroupFade(
                chromaGroup,
                fromAlpha: 0f,
                toAlpha: 1f,
                duration: chromaFadeIn,
                fromScale: 0.93f,
                toScale: 1.0f);
        }

        if (!skipRequested)
        {
            yield return AnimateHoldScale(chromaGroup, chromaHold, 1.0f, 1.025f);
        }

        if (!skipRequested)
        {
            StartCoroutine(FadeScrim(scrimStartAlpha, scrimDawnAlpha, chromaFadeOut + betweenBeatPause + 0.35f));
            yield return AnimateGroupFade(
                chromaGroup,
                fromAlpha: 1f,
                toAlpha: 0f,
                duration: chromaFadeOut,
                fromScale: 1.025f,
                toScale: 1.05f);
        }

        SetGroupState(chromaGroup, 0f, false);

        if (!skipRequested)
        {
            yield return WaitOrSkip(betweenBeatPause);
        }

        // 2. Thank you for playing 페이드 인 -> 홀드 -> 페이드 아웃 (동트듯 배경이 완전히 밝아짐)
        if (!skipRequested)
        {
            yield return AnimateThankYouFade(0f, 1f, thankYouFadeIn, -14f, 0f);
        }

        if (!skipRequested)
        {
            yield return WaitOrSkip(thankYouHold);
        }

        if (!skipRequested)
        {
            StartCoroutine(FadeScrim(scrimDawnAlpha, 0f, thankYouFadeOut + 0.25f));
            yield return AnimateThankYouFade(1f, 0f, thankYouFadeOut, 0f, 10f);
        }

        SetGroupState(thankYouGroup, 0f, false);

        if (!skipRequested)
        {
            yield return WaitOrSkip(0.18f);
        }

        // 3. 스킵 시 즉시 최종 상태 정리 후 아래에서 Title / Quit 버튼 등장
        if (skipRequested)
        {
            ApplyFinalRestingAtmosphere();
            yield return AnimateButtonsReveal(0.32f);
        }
        else
        {
            yield return AnimateButtonsReveal(buttonsFadeIn);
        }

        FinishSequence();
    }

    private IEnumerator AnimateBackgroundZoom(float totalDuration)
    {
        if (backgroundTransform == null || totalDuration <= 0.01f) yield break;

        float elapsed = 0f;
        while (elapsed < totalDuration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(backgroundStartScale, 1f, eased);
            backgroundTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        backgroundTransform.localScale = Vector3.one;
    }

    private IEnumerator AnimateGroupFade(
        CanvasGroup group,
        float fromAlpha,
        float toAlpha,
        float duration,
        float fromScale,
        float toScale)
    {
        if (group == null) yield break;

        RectTransform rt = group.transform as RectTransform;
        float elapsed = 0f;
        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            if (rt != null)
            {
                rt.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, eased);
            }
            yield return null;
        }

        if (!skipRequested)
        {
            group.alpha = toAlpha;
            if (rt != null) rt.localScale = Vector3.one * toScale;
        }
    }

    private IEnumerator AnimateHoldScale(CanvasGroup group, float duration, float fromScale, float toScale)
    {
        if (group == null)
        {
            yield return WaitOrSkip(duration);
            yield break;
        }

        RectTransform rt = group.transform as RectTransform;
        float elapsed = 0f;
        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (rt != null)
            {
                rt.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
            }
            yield return null;
        }
    }

    private IEnumerator AnimateThankYouFade(float fromAlpha, float toAlpha, float duration, float fromOffsetY, float toOffsetY)
    {
        if (thankYouGroup == null) yield break;

        RectTransform rt = thankYouGroup.transform as RectTransform;
        float elapsed = 0f;
        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            thankYouGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            if (rt != null)
            {
                rt.anchoredPosition = thankYouTargetAnchoredPos + new Vector2(0f, Mathf.Lerp(fromOffsetY, toOffsetY, eased));
            }
            yield return null;
        }

        if (!skipRequested)
        {
            thankYouGroup.alpha = toAlpha;
            if (rt != null)
            {
                rt.anchoredPosition = thankYouTargetAnchoredPos + new Vector2(0f, toOffsetY);
            }
        }
    }

    private IEnumerator FadeScrim(float fromAlpha, float toAlpha, float duration)
    {
        if (cinematicScrim == null) yield break;

        float elapsed = 0f;
        Color baseColor = cinematicScrim.color;
        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            baseColor.a = Mathf.Lerp(fromAlpha, toAlpha, eased);
            cinematicScrim.color = baseColor;
            yield return null;
        }

        baseColor.a = toAlpha;
        cinematicScrim.color = baseColor;
    }

    private IEnumerator AnimateButtonsReveal(float duration)
    {
        if (buttonGroup == null) yield break;

        RectTransform rt = buttonGroup.transform as RectTransform;
        Vector2 startPos = buttonsTargetAnchoredPos - new Vector2(0f, buttonSlideOffset);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            buttonGroup.alpha = eased;
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.Lerp(startPos, buttonsTargetAnchoredPos, eased);
            }
            yield return null;
        }

        SetGroupState(buttonGroup, 1f, true);
        if (rt != null)
        {
            rt.anchoredPosition = buttonsTargetAnchoredPos;
        }
    }

    private void ApplyFinalRestingAtmosphere()
    {
        SetGroupState(chromaGroup, 0f, false);
        SetGroupState(thankYouGroup, 0f, false);

        if (backgroundTransform != null)
        {
            backgroundTransform.localScale = Vector3.one;
        }

        if (cinematicScrim != null)
        {
            Color c = cinematicScrim.color;
            c.a = 0f;
            cinematicScrim.color = c;
        }
    }

    private void FinishSequence()
    {
        sequenceFinished = true;
        SetGroupState(buttonGroup, 1f, true);
    }

    private IEnumerator WaitOrSkip(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !skipRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static bool WasSkipInputPressed()
    {
        return Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Escape);
    }

    private static void SetGroupState(CanvasGroup group, float alpha, bool interactable)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = interactable;
        group.blocksRaycasts = interactable;
    }

    private void SpawnAmbientMotes()
    {
        if (motesContainer == null || ambientMoteCount <= 0) return;

        Color[] motePalette =
        {
            new Color(1.00f, 0.90f, 0.58f, 1f), // Sunlight gold
            new Color(1.00f, 0.48f, 0.45f, 1f), // Warm red
            new Color(0.45f, 0.92f, 0.62f, 1f), // Meadow green
            new Color(0.45f, 0.85f, 1.00f, 1f), // Sky blue
        };

        for (int i = 0; i < ambientMoteCount; i++)
        {
            GameObject moteObj = new GameObject($"EndingMote_{i}", typeof(RectTransform), typeof(Image));
            moteObj.layer = motesContainer.gameObject.layer;
            moteObj.transform.SetParent(motesContainer, false);

            RectTransform rt = moteObj.GetComponent<RectTransform>();
            float size = Random.Range(6f, 12f);
            rt.sizeDelta = new Vector2(size, size);
            float startX = Random.Range(-920f, 920f);
            float startY = Random.Range(-520f, 520f);
            rt.anchoredPosition = new Vector2(startX, startY);

            Image img = moteObj.GetComponent<Image>();
            img.raycastTarget = false;
            Color col = motePalette[i % motePalette.Length];
            float baseAlpha = Random.Range(0.22f, 0.55f);
            col.a = baseAlpha;
            img.color = col;

            ambientMotes.Add(new AmbientMote
            {
                rect = rt,
                image = img,
                speedY = Random.Range(16f, 36f),
                driftAmp = Random.Range(12f, 34f),
                driftFreq = Random.Range(0.7f, 1.5f),
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = baseAlpha,
                startX = startX
            });
        }
    }

    private void UpdateAmbientMotes()
    {
        if (ambientMotes.Count == 0) return;

        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        for (int i = 0; i < ambientMotes.Count; i++)
        {
            AmbientMote mote = ambientMotes[i];
            if (mote.rect == null || mote.image == null) continue;

            Vector2 pos = mote.rect.anchoredPosition;
            pos.y += mote.speedY * dt;
            if (pos.y > 560f)
            {
                pos.y = -560f;
            }
            pos.x = mote.startX + Mathf.Sin(t * mote.driftFreq + mote.phase) * mote.driftAmp;
            mote.rect.anchoredPosition = pos;

            Color c = mote.image.color;
            float twinkle = 0.55f + 0.45f * Mathf.Sin(t * 2.2f + mote.phase);
            c.a = mote.baseAlpha * twinkle;
            mote.image.color = c;
        }
    }
}

public class EndingButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float HoverScale = 1.06f;
    private const float PressScale = 0.96f;
    private const float LerpSpeed = 16f;

    private bool isHovered;
    private bool isPressed;
    private float targetScale = 1f;
    private Button cachedButton;

    private void Awake()
    {
        cachedButton = GetComponent<Button>();
    }

    private void OnDisable()
    {
        isHovered = false;
        isPressed = false;
        targetScale = 1f;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        float current = transform.localScale.x;
        if (Mathf.Abs(current - targetScale) > 0.001f)
        {
            float next = Mathf.Lerp(current, targetScale, Mathf.Clamp01(Time.unscaledDeltaTime * LerpSpeed));
            transform.localScale = Vector3.one * next;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cachedButton != null && !cachedButton.interactable) return;
        isHovered = true;
        RefreshTargetScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        RefreshTargetScale();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (cachedButton != null && !cachedButton.interactable) return;
        isPressed = true;
        RefreshTargetScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        RefreshTargetScale();
    }

    private void RefreshTargetScale()
    {
        if (isPressed)
            targetScale = PressScale;
        else if (isHovered)
            targetScale = HoverScale;
        else
            targetScale = 1f;
    }
}
