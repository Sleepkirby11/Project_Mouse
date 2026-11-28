using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hand-pixeled Boss HP bar & Name controller.
/// Uses the game's midnight-indigo & silver-slate pixel aesthetic with a borderless ink scrim +
/// tapered pixel underline for the boss name, compact prism crest, hit flash, and delayed damage trail.
/// </summary>
public class BossUIStyler : MonoBehaviour
{
    private const float BarWidth = 660f;
    private const float BarHeight = 34f;
    private const float BarPosY = -46f;

    private const float PlateHeight = 36f;
    private const float PlatePosY = -8f;
    private const float PlateMinWidth = 260f;
    private const float PlateMaxWidth = 560f;
    private const float PlateHorizontalPadding = 128f;

    private const float CrestSize = 44f;
    private const float CrestPosX = -4f;

    private const float TrailHoldDuration = 0.18f;
    private const float TrailDrainSpeed = 0.85f;
    private const float HitFlashDuration = 0.09f;

    private static readonly Color DefaultCrimson = new Color(0.88f, 0.22f, 0.26f, 1f);
    private static readonly Color DefaultIvoryText = new Color(0.95f, 0.94f, 0.98f, 1f);
    private static readonly Color TrailColor = new Color(0.94f, 0.91f, 0.98f, 0.86f);
    private static readonly Color TextOutlineColor = new Color(0.04f, 0.03f, 0.08f, 0.96f);

    private Slider bossSlider;
    private Text bossNameText;
    private Image namePlateImage;
    private Image barFillImage;
    private Image damageTrailImage;
    private RectTransform damageTrailRect;
    private Image crestImage;
    private Image gemImage;

    private Color activeFillColor = DefaultCrimson;
    private float currentRatio = 1f;
    private float trailRatio = 1f;
    private float trailHoldTimer = 0f;
    private float hitFlashTimer = 0f;

    public static BossUIStyler AttachOrGet(Slider slider, Text nameText)
    {
        if (slider == null) return null;

        BossUIStyler styler = slider.GetComponent<BossUIStyler>();
        if (styler == null)
        {
            styler = slider.gameObject.AddComponent<BossUIStyler>();
        }
        styler.Bind(slider, nameText);
        return styler;
    }

    public void Bind(Slider slider, Text nameText)
    {
        bossSlider = slider;
        bossNameText = nameText;

        if (bossSlider == null) return;

        EnsureSliderHierarchy();
        EnsureNamePlateHierarchy();
    }

    public void Show(string bossName, Color themeColor, float initialRatio)
    {
        if (bossSlider == null) return;

        EnsureSliderHierarchy();
        EnsureNamePlateHierarchy();

        bool isNeutral = IsNeutralColor(themeColor);
        Color baseColor = isNeutral ? DefaultCrimson : themeColor;
        activeFillColor = BoostFillReadability(baseColor);

        currentRatio = Mathf.Clamp01(initialRatio);
        trailRatio = currentRatio;
        trailHoldTimer = 0f;
        hitFlashTimer = 0f;

        bossSlider.gameObject.SetActive(true);
        bossSlider.value = currentRatio;

        if (barFillImage != null)
        {
            barFillImage.color = activeFillColor;
        }

        if (gemImage != null)
        {
            gemImage.color = Color.Lerp(baseColor, Color.white, 0.16f);
        }

        ApplyTrailAnchor(trailRatio);

        if (bossNameText != null)
        {
            bossNameText.gameObject.SetActive(true);
            bossNameText.text = bossName;
            bossNameText.color = isNeutral
                ? DefaultIvoryText
                : Color.Lerp(baseColor, Color.white, 0.34f);

            UpdateNamePlateLayout();
        }

        if (namePlateImage != null)
        {
            namePlateImage.color = Color.white;
            namePlateImage.gameObject.SetActive(true);
        }
    }

    public void UpdateHP(float currentHP, float maxHP)
    {
        if (bossSlider == null || maxHP <= 0f) return;

        float nextRatio = Mathf.Clamp01(currentHP / maxHP);
        bossSlider.value = nextRatio;

        if (nextRatio < currentRatio - 0.0001f)
        {
            trailHoldTimer = TrailHoldDuration;
            hitFlashTimer = HitFlashDuration;
        }
        else if (nextRatio > trailRatio)
        {
            trailRatio = nextRatio;
            ApplyTrailAnchor(trailRatio);
        }

        currentRatio = nextRatio;
    }

    public void Hide()
    {
        if (bossSlider != null)
        {
            bossSlider.gameObject.SetActive(false);
        }
        if (bossNameText != null)
        {
            bossNameText.gameObject.SetActive(false);
        }
        if (namePlateImage != null)
        {
            namePlateImage.gameObject.SetActive(false);
        }
        hitFlashTimer = 0f;
    }

    private void Update()
    {
        if (bossSlider == null) return;

        if (hitFlashTimer > 0f && barFillImage != null)
        {
            hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.unscaledDeltaTime);
            float flashT = hitFlashTimer / HitFlashDuration;
            barFillImage.color = Color.Lerp(activeFillColor, Color.white, flashT * 0.65f);
        }

        if (trailRatio > currentRatio)
        {
            if (trailHoldTimer > 0f)
            {
                trailHoldTimer -= Time.unscaledDeltaTime;
            }
            else
            {
                trailRatio = Mathf.MoveTowards(trailRatio, currentRatio, TrailDrainSpeed * Time.unscaledDeltaTime);
                ApplyTrailAnchor(trailRatio);
            }
        }
    }

    private void EnsureSliderHierarchy()
    {
        RectTransform sliderRt = bossSlider.GetComponent<RectTransform>();
        if (sliderRt != null)
        {
            sliderRt.anchorMin = new Vector2(0.5f, 1f);
            sliderRt.anchorMax = new Vector2(0.5f, 1f);
            sliderRt.pivot = new Vector2(0.5f, 1f);
            sliderRt.anchoredPosition = new Vector2(0f, BarPosY);
            sliderRt.sizeDelta = new Vector2(BarWidth, BarHeight);
            sliderRt.localScale = Vector3.one;
        }

        Transform bgTf = bossSlider.transform.Find("Background");
        if (bgTf != null)
        {
            RectTransform bgRt = bgTf as RectTransform;
            if (bgRt != null)
            {
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
            }

            Image bgImg = bgTf.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = Color.white;
                bgImg.type = Image.Type.Sliced;
                bgImg.pixelsPerUnitMultiplier = 1f;
                bgImg.raycastTarget = false;
            }
        }

        Transform fillAreaTf = bossSlider.transform.Find("Fill Area");
        if (fillAreaTf != null)
        {
            RectTransform faRt = fillAreaTf as RectTransform;
            if (faRt != null)
            {
                faRt.anchorMin = Vector2.zero;
                faRt.anchorMax = Vector2.one;
                faRt.offsetMin = new Vector2(20f, 8f);
                faRt.offsetMax = new Vector2(-20f, -8f);
            }

            Transform fillTf = fillAreaTf.Find("Fill");
            if (fillTf != null)
            {
                RectTransform fillRt = fillTf as RectTransform;
                if (fillRt != null)
                {
                    fillRt.anchorMin = Vector2.zero;
                    fillRt.anchorMax = new Vector2(bossSlider.value, 1f);
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;
                }

                barFillImage = fillTf.GetComponent<Image>();
                if (barFillImage != null)
                {
                    barFillImage.type = Image.Type.Sliced;
                    barFillImage.pixelsPerUnitMultiplier = 1f;
                    barFillImage.raycastTarget = false;
                }
            }

            EnsureDamageTrail(fillAreaTf, fillTf);
        }

        Transform crestTf = bossSlider.transform.Find("Crest");
        if (crestTf != null)
        {
            RectTransform crestRt = crestTf as RectTransform;
            if (crestRt != null)
            {
                crestRt.anchorMin = new Vector2(0f, 0.5f);
                crestRt.anchorMax = new Vector2(0f, 0.5f);
                crestRt.pivot = new Vector2(0.5f, 0.5f);
                crestRt.anchoredPosition = new Vector2(CrestPosX, 0f);
                crestRt.sizeDelta = new Vector2(CrestSize, CrestSize);
                crestRt.localScale = Vector3.one;
            }

            crestImage = crestTf.GetComponent<Image>();
            if (crestImage != null)
            {
                crestImage.color = Color.white;
                crestImage.type = Image.Type.Simple;
                crestImage.raycastTarget = false;
            }

            Transform gemTf = crestTf.Find("Gem");
            if (gemTf != null)
            {
                RectTransform gemRt = gemTf as RectTransform;
                if (gemRt != null)
                {
                    gemRt.anchorMin = Vector2.zero;
                    gemRt.anchorMax = Vector2.one;
                    gemRt.offsetMin = Vector2.zero;
                    gemRt.offsetMax = Vector2.zero;
                }

                gemImage = gemTf.GetComponent<Image>();
                if (gemImage != null)
                {
                    gemImage.type = Image.Type.Simple;
                    gemImage.raycastTarget = false;
                }
            }
        }
    }

    private void EnsureDamageTrail(Transform fillAreaTf, Transform fillTf)
    {
        Transform trailTf = fillAreaTf.Find("DamageTrail");
        if (trailTf == null)
        {
            GameObject trailGo = new GameObject("DamageTrail", typeof(RectTransform), typeof(Image));
            trailGo.layer = fillAreaTf.gameObject.layer;
            trailGo.transform.SetParent(fillAreaTf, false);
            trailGo.transform.SetAsFirstSibling();
            trailTf = trailGo.transform;
        }
        else if (fillTf != null && trailTf.GetSiblingIndex() > fillTf.GetSiblingIndex())
        {
            trailTf.SetSiblingIndex(fillTf.GetSiblingIndex());
        }

        damageTrailRect = trailTf as RectTransform;
        if (damageTrailRect != null)
        {
            damageTrailRect.anchorMin = Vector2.zero;
            damageTrailRect.anchorMax = new Vector2(trailRatio, 1f);
            damageTrailRect.offsetMin = Vector2.zero;
            damageTrailRect.offsetMax = Vector2.zero;
        }

        damageTrailImage = trailTf.GetComponent<Image>();
        if (damageTrailImage != null)
        {
            if (barFillImage != null)
            {
                damageTrailImage.sprite = barFillImage.sprite;
            }
            damageTrailImage.type = Image.Type.Sliced;
            damageTrailImage.pixelsPerUnitMultiplier = 1f;
            damageTrailImage.color = TrailColor;
            damageTrailImage.raycastTarget = false;
        }
    }

    private void EnsureNamePlateHierarchy()
    {
        if (bossNameText == null) return;

        Transform canvasTf = bossSlider != null ? bossSlider.transform.parent : bossNameText.transform.parent;
        if (canvasTf != null)
        {
            Transform plateTf = canvasTf.Find("BossNamePlate");
            if (plateTf != null)
            {
                namePlateImage = plateTf.GetComponent<Image>();
                if (namePlateImage != null)
                {
                    namePlateImage.color = Color.white;
                    namePlateImage.type = Image.Type.Sliced;
                    namePlateImage.pixelsPerUnitMultiplier = 1f;
                    namePlateImage.raycastTarget = false;
                }
            }
        }

        Font pixelFont = SettingsUIStyler.GetPixelFont(bossNameText.transform.root);
        if (pixelFont != null && bossNameText.font == null)
        {
            bossNameText.font = pixelFont;
        }

        bossNameText.fontSize = 24;
        bossNameText.fontStyle = FontStyle.Bold;
        bossNameText.alignment = TextAnchor.MiddleCenter;
        bossNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bossNameText.verticalOverflow = VerticalWrapMode.Overflow;
        bossNameText.raycastTarget = false;

        Outline outline = bossNameText.GetComponent<Outline>();
        if (outline == null) outline = bossNameText.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = TextOutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        UpdateNamePlateLayout();

        if (bossSlider != null && namePlateImage != null && namePlateImage.transform.parent == bossSlider.transform.parent)
        {
            int barIdx = bossSlider.transform.GetSiblingIndex();
            if (namePlateImage.transform.GetSiblingIndex() < barIdx)
            {
                namePlateImage.transform.SetSiblingIndex(barIdx);
            }
            if (bossNameText.transform.parent == namePlateImage.transform.parent)
            {
                int plateIdx = namePlateImage.transform.GetSiblingIndex();
                if (bossNameText.transform.GetSiblingIndex() < plateIdx)
                {
                    bossNameText.transform.SetSiblingIndex(plateIdx);
                }
            }
        }
    }

    private void UpdateNamePlateLayout()
    {
        if (bossNameText == null) return;

        float width = PlateMinWidth;
        float preferred = bossNameText.preferredWidth;
        if (preferred > 0f)
        {
            width = Mathf.Clamp(preferred + PlateHorizontalPadding, PlateMinWidth, PlateMaxWidth);
            width = Mathf.Ceil(width / 4f) * 4f;
        }

        if (namePlateImage != null)
        {
            RectTransform plateRt = namePlateImage.rectTransform;
            plateRt.anchorMin = new Vector2(0.5f, 1f);
            plateRt.anchorMax = new Vector2(0.5f, 1f);
            plateRt.pivot = new Vector2(0.5f, 1f);
            plateRt.anchoredPosition = new Vector2(0f, PlatePosY);
            plateRt.sizeDelta = new Vector2(width, PlateHeight);
            plateRt.localScale = Vector3.one;
        }

        RectTransform textRt = bossNameText.rectTransform;
        textRt.anchorMin = new Vector2(0.5f, 1f);
        textRt.anchorMax = new Vector2(0.5f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.anchoredPosition = new Vector2(0f, PlatePosY);
        textRt.sizeDelta = new Vector2(width - 40f, PlateHeight - 6f);
        textRt.localScale = Vector3.one;
    }

    private void ApplyTrailAnchor(float ratio)
    {
        if (damageTrailRect == null) return;
        damageTrailRect.anchorMin = Vector2.zero;
        damageTrailRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        damageTrailRect.offsetMin = Vector2.zero;
        damageTrailRect.offsetMax = Vector2.zero;
    }

    private static bool IsNeutralColor(Color c)
    {
        return c == Color.clear || (c.r > 0.94f && c.g > 0.94f && c.b > 0.94f);
    }

    private static Color BoostFillReadability(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        v = Mathf.Max(v, 0.88f);
        s = Mathf.Clamp(s, 0.55f, 0.92f);
        Color result = Color.HSVToRGB(h, s, v);
        result.a = 1f;
        return result;
    }
}
