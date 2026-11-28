using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Styles the Player's top-left HP, Ink, and SpecialInk gauges into clean minimal pixel bars
/// matching the brush icon and Boss HUD, with a delayed damage trail on HP.
/// </summary>
public class PlayerHudStyler : MonoBehaviour
{
    private const float BarsStartX = 64f;
    private const float BarWidth = 500f;
    private const float HpHeight = 26f;
    private const float ThinBarHeight = 14f;

    private const float TrailHoldDuration = 0.14f;
    private const float TrailDrainSpeed = 0.95f;
    private const float HitFlashDuration = 0.08f;

    private static readonly Color TrailColor = new Color(0.95f, 0.93f, 0.98f, 0.88f);

    private Slider hpSlider;
    private Image hpFillImage;
    private RectTransform hpTrailRect;
    private Color baseHpFillColor = new Color(0.21583721f, 0.7830189f, 0f, 1f);

    private float currentHpRatio = 1f;
    private float trailHpRatio = 1f;
    private float trailHoldTimer = 0f;
    private float hitFlashTimer = 0f;

    public static PlayerHudStyler AttachAndStyle(Slider hpSlider, Slider inkSlider, Slider specialInkSlider, Slider coolTimeSlider)
    {
        if (hpSlider == null) return null;

        PlayerHudStyler styler = hpSlider.GetComponent<PlayerHudStyler>();
        if (styler == null)
        {
            styler = hpSlider.gameObject.AddComponent<PlayerHudStyler>();
        }
        styler.Configure(hpSlider, inkSlider, specialInkSlider, coolTimeSlider);
        return styler;
    }

    public void Configure(Slider hp, Slider ink, Slider specialInk, Slider coolTime)
    {
        hpSlider = hp;

        StyleBar(hp, BarsStartX, -2f, BarWidth, HpHeight, new Vector2(2f, 2f), new Vector2(-2f, -2f), createTrail: true);
        StyleBar(ink, BarsStartX, -30f, BarWidth, ThinBarHeight, new Vector2(2f, 2f), new Vector2(-2f, -2f), createTrail: false);
        StyleBar(specialInk, BarsStartX, -46f, BarWidth, ThinBarHeight, new Vector2(2f, 2f), new Vector2(-2f, -2f), createTrail: false);

        if (coolTime != null && coolTime.transform.parent != null)
        {
            Transform legacyBacking = coolTime.transform.parent.Find("IconBacking");
            if (legacyBacking != null)
            {
                legacyBacking.gameObject.SetActive(false);
            }
        }
    }

    public void SetHPImmediate(float ratio)
    {
        currentHpRatio = Mathf.Clamp01(ratio);
        trailHpRatio = currentHpRatio;
        trailHoldTimer = 0f;
        hitFlashTimer = 0f;
        if (hpFillImage != null)
        {
            hpFillImage.color = baseHpFillColor;
        }
        ApplyTrailAnchor(trailHpRatio);
    }

    public void NotifyHPChanged(float ratio)
    {
        float nextRatio = Mathf.Clamp01(ratio);
        if (nextRatio < currentHpRatio - 0.0001f)
        {
            trailHoldTimer = TrailHoldDuration;
            hitFlashTimer = HitFlashDuration;
        }
        else if (nextRatio > trailHpRatio)
        {
            trailHpRatio = nextRatio;
            ApplyTrailAnchor(trailHpRatio);
        }
        currentHpRatio = nextRatio;
    }

    private void Update()
    {
        if (hpSlider == null) return;

        if (hitFlashTimer > 0f && hpFillImage != null)
        {
            hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.unscaledDeltaTime);
            float t = hitFlashTimer / HitFlashDuration;
            hpFillImage.color = Color.Lerp(baseHpFillColor, Color.white, t * 0.55f);
        }

        if (trailHpRatio > currentHpRatio)
        {
            if (trailHoldTimer > 0f)
            {
                trailHoldTimer -= Time.unscaledDeltaTime;
            }
            else
            {
                trailHpRatio = Mathf.MoveTowards(trailHpRatio, currentHpRatio, TrailDrainSpeed * Time.unscaledDeltaTime);
                ApplyTrailAnchor(trailHpRatio);
            }
        }
    }

    private void StyleBar(
        Slider slider,
        float posX,
        float posY,
        float width,
        float height,
        Vector2 offsetMin,
        Vector2 offsetMax,
        bool createTrail)
    {
        if (slider == null) return;

        RectTransform sliderRt = slider.GetComponent<RectTransform>();
        if (sliderRt != null)
        {
            sliderRt.anchorMin = new Vector2(0f, 1f);
            sliderRt.anchorMax = new Vector2(0f, 1f);
            sliderRt.pivot = new Vector2(0f, 1f);
            sliderRt.anchoredPosition = new Vector2(posX, posY);
            sliderRt.sizeDelta = new Vector2(width, height);
            sliderRt.localScale = Vector3.one;
        }

        Transform bgTf = slider.transform.Find("Background");
        if (bgTf != null)
        {
            Image bgImg = bgTf.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = Color.white;
                bgImg.type = Image.Type.Sliced;
                bgImg.pixelsPerUnitMultiplier = 1f;
                bgImg.raycastTarget = false;
            }
        }

        Transform fillAreaTf = slider.transform.Find("Fill Area");
        if (fillAreaTf != null)
        {
            RectTransform faRt = fillAreaTf as RectTransform;
            if (faRt != null)
            {
                faRt.anchorMin = Vector2.zero;
                faRt.anchorMax = Vector2.one;
                faRt.offsetMin = offsetMin;
                faRt.offsetMax = offsetMax;
            }

            Transform fillTf = fillAreaTf.Find("Fill");
            if (fillTf != null)
            {
                RectTransform fillRt = fillTf as RectTransform;
                if (fillRt != null)
                {
                    fillRt.anchorMin = Vector2.zero;
                    fillRt.anchorMax = new Vector2(slider.value, 1f);
                    fillRt.offsetMin = Vector2.zero;
                    fillRt.offsetMax = Vector2.zero;
                }

                Image fillImg = fillTf.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.type = Image.Type.Sliced;
                    fillImg.pixelsPerUnitMultiplier = 1f;
                    fillImg.raycastTarget = false;

                    if (createTrail)
                    {
                        hpFillImage = fillImg;
                        baseHpFillColor = fillImg.color;
                        EnsureDamageTrail(fillAreaTf, fillTf, fillImg.sprite);
                    }
                }
            }
        }
    }

    private void EnsureDamageTrail(Transform fillAreaTf, Transform fillTf, Sprite fillSprite)
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

        hpTrailRect = trailTf as RectTransform;
        ApplyTrailAnchor(trailHpRatio);

        Image trailImg = trailTf.GetComponent<Image>();
        if (trailImg != null)
        {
            if (fillSprite != null)
            {
                trailImg.sprite = fillSprite;
            }
            trailImg.type = Image.Type.Sliced;
            trailImg.pixelsPerUnitMultiplier = 1f;
            trailImg.color = TrailColor;
            trailImg.raycastTarget = false;
        }
    }

    private void ApplyTrailAnchor(float ratio)
    {
        if (hpTrailRect == null) return;
        hpTrailRect.anchorMin = Vector2.zero;
        hpTrailRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        hpTrailRect.offsetMin = Vector2.zero;
        hpTrailRect.offsetMax = Vector2.zero;
    }
}
