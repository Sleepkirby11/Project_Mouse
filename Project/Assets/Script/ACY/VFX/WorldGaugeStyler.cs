using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime pixel-art styler for overhead world-space Slider gauges.
/// Applies crisp dark-ink frames, clean zero-padded fill rects, gloss highlights,
/// and anti-flip world scale correction without modifying settings UI.
/// </summary>
public static class WorldGaugeStyler
{
    private static Sprite s_pixelSprite;

    public static Sprite GetPixelSprite()
    {
        if (s_pixelSprite != null) return s_pixelSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, 255);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        s_pixelSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        s_pixelSprite.hideFlags = HideFlags.HideAndDontSave;
        return s_pixelSprite;
    }

    public static void StyleWorldGauge(
        Slider slider,
        string labelText,
        Color fillColor,
        Color accentBorderColor)
    {
        if (slider == null) return;

        Sprite pixel = GetPixelSprite();
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect != null && sliderRect.sizeDelta.y < 18f)
        {
            sliderRect.sizeDelta = new Vector2(sliderRect.sizeDelta.x, 18f);
        }

        Transform bgTransform = slider.transform.Find("Background");
        if (bgTransform != null)
        {
            RectTransform bgRect = bgTransform as RectTransform;
            if (bgRect != null)
            {
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
            }

            Image bgImg = bgTransform.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = pixel;
                bgImg.type = Image.Type.Simple;
                bgImg.color = new Color(0.06f, 0.06f, 0.09f, 0.94f);
                bgImg.raycastTarget = false;
            }

            Outline bgOutline = EnsureComponent<Outline>(bgTransform.gameObject);
            bgOutline.effectColor = new Color(0.02f, 0.02f, 0.04f, 1f);
            bgOutline.effectDistance = new Vector2(2f, -2f);
            bgOutline.useGraphicAlpha = false;

            EnsureTopBorderStrip(bgTransform, pixel, accentBorderColor, 1.5f);
        }

        // Fix leftover Handle padding on Fill Area & Fill so 0..1 spans 100% cleanly inside 3px border
        Transform fillArea = slider.transform.Find("Fill Area");
        if (fillArea != null)
        {
            RectTransform faRect = fillArea as RectTransform;
            if (faRect != null)
            {
                faRect.anchorMin = Vector2.zero;
                faRect.anchorMax = Vector2.one;
                faRect.offsetMin = new Vector2(3f, 3f);
                faRect.offsetMax = new Vector2(-3f, -3f);
            }
        }

        if (slider.fillRect != null)
        {
            RectTransform fillRect = slider.fillRect;
            fillRect.sizeDelta = Vector2.zero;

            Image fillImg = fillRect.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.sprite = pixel;
                fillImg.type = Image.Type.Simple;
                fillImg.color = fillColor;
                fillImg.raycastTarget = false;
            }

            EnsureTopBorderStrip(fillRect, pixel, new Color(1f, 1f, 1f, 0.30f), 2f);
        }

        Transform existingLabel = slider.transform.Find("GaugeLabel");
        if (string.IsNullOrEmpty(labelText))
        {
            if (existingLabel != null)
            {
                existingLabel.gameObject.SetActive(false);
            }
        }
        else
        {
            Text label = EnsureChildText(slider.transform, "GaugeLabel", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.text = labelText;
                label.fontSize = 13;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.LowerCenter;
                label.color = new Color(0.96f, 0.96f, 1f, 0.96f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;

                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0.5f, 1f);
                labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 3f);
                labelRect.sizeDelta = new Vector2(200f, 22f);

                Outline txtOutline = EnsureComponent<Outline>(label.gameObject);
                txtOutline.effectColor = new Color(0.03f, 0.03f, 0.05f, 1f);
                txtOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
    }

    public static void UpdateWorldGaugeColor(Slider slider, Color startColor, Color endColor)
    {
        if (slider == null || slider.fillRect == null) return;
        Image fillImg = slider.fillRect.GetComponent<Image>();
        if (fillImg == null) return;

        float t = slider.normalizedValue;
        fillImg.color = Color.Lerp(startColor, endColor, Mathf.Clamp01(t));
    }

    /// <summary>
    /// Keeps a world-space gauge oriented left-to-right even when its owner flips transform.localScale.x.
    /// </summary>
    public static void KeepUnflipped(Slider slider, Transform ownerTransform)
    {
        if (slider == null) return;

        Transform gaugeRoot = slider.transform.parent != null && slider.transform.parent != ownerTransform
            ? slider.transform.parent
            : slider.transform;

        Transform parentTf = gaugeRoot.parent;
        float parentSignX = (parentTf != null && parentTf.lossyScale.x < 0f) ? -1f : 1f;

        Vector3 scale = gaugeRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * parentSignX;
        gaugeRoot.localScale = scale;
    }

    private static void EnsureTopBorderStrip(Transform parent, Sprite pixel, Color color, float height)
    {
        Transform stripTf = parent.Find("TopHighlight");
        GameObject stripObj;
        if (stripTf == null)
        {
            stripObj = new GameObject("TopHighlight", typeof(RectTransform), typeof(Image));
            stripObj.transform.SetParent(parent, false);
        }
        else
        {
            stripObj = stripTf.gameObject;
        }

        RectTransform stripRect = stripObj.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.sizeDelta = new Vector2(0f, height);

        Image stripImg = stripObj.GetComponent<Image>();
        stripImg.sprite = pixel;
        stripImg.type = Image.Type.Simple;
        stripImg.color = color;
        stripImg.raycastTarget = false;
    }

    private static Text EnsureChildText(Transform parent, string childName, Font font)
    {
        Transform childTf = parent.Find(childName);
        GameObject childObj;
        if (childTf == null)
        {
            childObj = new GameObject(childName, typeof(RectTransform), typeof(Text));
            childObj.transform.SetParent(parent, false);
        }
        else
        {
            childObj = childTf.gameObject;
        }

        Text text = childObj.GetComponent<Text>();
        if (text != null && font != null)
        {
            text.font = font;
        }
        return text;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp == null)
        {
            comp = target.AddComponent<T>();
        }
        return comp;
    }
}
