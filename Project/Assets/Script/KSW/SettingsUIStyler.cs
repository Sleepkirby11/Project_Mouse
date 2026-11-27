using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Styles the Settings Panel to match the game's hand-pixeled stone & parchment UI (Interface.png).
/// Keeps typography simple and natural (SETTINGS, BGM, SFX, Guide, License, Back/Close, Quit).
/// </summary>
public class SettingsUIStyler : MonoBehaviour
{
    private static Font s_cachedPixelFont;

    public static Font GetPixelFont(Transform searchRoot = null)
    {
        if (s_cachedPixelFont != null) return s_cachedPixelFont;

        if (UI.Instance != null)
        {
            Font uiFont = UI.Instance.GetUIFont();
            if (uiFont != null && uiFont.name.IndexOf("MaruMinya", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s_cachedPixelFont = uiFont;
                return s_cachedPixelFont;
            }
        }

        Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
        foreach (Font f in allFonts)
        {
            if (f != null && f.name.IndexOf("MaruMinya", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s_cachedPixelFont = f;
                return s_cachedPixelFont;
            }
        }

        if (searchRoot != null)
        {
            Text[] texts = searchRoot.GetComponentsInChildren<Text>(true);
            foreach (Text t in texts)
            {
                if (t != null && t.font != null)
                {
                    s_cachedPixelFont = t.font;
                    if (t.font.name.IndexOf("MaruMinya", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return s_cachedPixelFont;
                }
            }
        }

        if (s_cachedPixelFont == null)
        {
            s_cachedPixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return s_cachedPixelFont;
    }

    public static void EnsureStyled(GameObject settingPanel, GameObject guidePanel = null, GameObject licensePanel = null)
    {
        if (settingPanel != null)
        {
            SettingsUIStyler styler = settingPanel.GetComponent<SettingsUIStyler>();
            if (styler == null)
            {
                styler = settingPanel.AddComponent<SettingsUIStyler>();
            }
            styler.ApplyStyle();
        }

        if (guidePanel != null)
        {
            StyleGuidePanel(guidePanel);
        }

        if (licensePanel != null)
        {
            StyleLicensePanel(licensePanel);
        }
    }

    private void Awake()
    {
        ApplyStyle();
    }

    private void OnEnable()
    {
        ApplyStyle();
    }

    public void ApplyStyle()
    {
        Font pixelFont = GetPixelFont(transform.root);

        // 1. Soft dark dim backdrop
        Image backdropImg = GetComponent<Image>();
        if (backdropImg != null)
        {
            backdropImg.color = new Color(0.06f, 0.05f, 0.05f, 0.68f);
        }

        // 2. Main Modal Frame ("Scroll View" - 684 x 612 stone & parchment board)
        Transform frameTf = transform.Find("Scroll View");
        if (frameTf == null) return;

        RectTransform windowRect = frameTf as RectTransform;
        if (windowRect != null)
        {
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(684f, 612f);
            windowRect.localScale = Vector3.one;
        }

        ScrollRect sr = frameTf.GetComponent<ScrollRect>();
        if (sr != null)
        {
            sr.enabled = false;
        }

        Transform viewportTf = frameTf.Find("Viewport");
        if (viewportTf != null)
        {
            viewportTf.gameObject.SetActive(false);
        }

        Image frameImg = frameTf.GetComponent<Image>();
        if (frameImg != null)
        {
            frameImg.color = Color.white;
            frameImg.type = Image.Type.Simple;
        }

        // Clean up any old artificial labels if present
        string[] legacyLabels = { "HeaderSubtitle", "SectionAudioLabel", "SectionSystemLabel", "FooterEscHint" };
        foreach (string oldName in legacyLabels)
        {
            Transform oldTf = frameTf.Find(oldName);
            if (oldTf != null) Destroy(oldTf.gameObject);
        }

        // 3. Simple "SETTINGS" title in dark warm ink on the top of the parchment
        Text titleLabel = EnsureLabel(
            frameTf,
            "HeaderTitle",
            "SETTINGS",
            pixelFont,
            34,
            new Color(0.22f, 0.12f, 0.10f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 242f),
            new Vector2(360f, 48f),
            useOutline: false,
            outlineColor: Color.clear);
        if (titleLabel != null) titleLabel.alignment = TextAnchor.MiddleCenter;

        // 4. Audio Control Rows (BGM & SFX)
        StyleAudioRow(frameTf.Find("BGM"), isBgm: true, pixelFont);
        StyleAudioRow(frameTf.Find("SFX"), isBgm: false, pixelFont);

        // 5. Action Buttons Layout
        LayoutActionButtons(frameTf, pixelFont);
    }

    private void StyleAudioRow(Transform rowTf, bool isBgm, Font pixelFont)
    {
        if (rowTf == null) return;

        RectTransform rowRect = rowTf as RectTransform;
        if (rowRect != null)
        {
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, isBgm ? 128f : 22f);
            rowRect.sizeDelta = new Vector2(480f, 84f);
        }

        string iconName = isBgm ? "SoundUI_BGM" : "SoundUI_SFX";
        Transform iconTf = rowTf.Find(iconName);
        if (iconTf != null)
        {
            RectTransform iconRect = iconTf as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(-195f, 0f);
                iconRect.sizeDelta = new Vector2(76f, 76f);
            }

            Image iconImg = iconTf.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.color = Color.white;
            }
        }

        string sliderName = isBgm ? "MusicVolume" : "SFXVolume";
        Transform sliderTf = rowTf.Find(sliderName);
        if (sliderTf != null)
        {
            RectTransform sliderRect = sliderTf as RectTransform;
            if (sliderRect != null)
            {
                sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
                sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
                sliderRect.pivot = new Vector2(0.5f, 0.5f);
                sliderRect.anchoredPosition = new Vector2(52f, -18f);
                sliderRect.sizeDelta = new Vector2(370f, 28f);
            }

            Transform bgTf = sliderTf.Find("Background");
            if (bgTf != null)
            {
                Image bgImg = bgTf.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.color = Color.white;
                    bgImg.type = Image.Type.Sliced;
                }
            }

            Transform fillAreaTf = sliderTf.Find("Fill Area");
            if (fillAreaTf != null)
            {
                RectTransform faRect = fillAreaTf as RectTransform;
                if (faRect != null)
                {
                    faRect.anchorMin = Vector2.zero;
                    faRect.anchorMax = Vector2.one;
                    faRect.anchoredPosition = Vector2.zero;
                    faRect.sizeDelta = new Vector2(-12f, -12f);
                }

                Transform fillTf = fillAreaTf.Find("Fill");
                if (fillTf != null)
                {
                    RectTransform fillRect = fillTf as RectTransform;
                    if (fillRect != null)
                    {
                        fillRect.anchoredPosition = Vector2.zero;
                        fillRect.sizeDelta = Vector2.zero;
                    }

                    Image fillImg = fillTf.GetComponent<Image>();
                    if (fillImg != null)
                    {
                        fillImg.color = new Color(0.85f, 0.54f, 0.24f, 1f);
                        fillImg.type = Image.Type.Sliced;
                    }
                }
            }

            Transform handleAreaTf = sliderTf.Find("Handle Slide Area");
            if (handleAreaTf != null)
            {
                RectTransform haRect = handleAreaTf as RectTransform;
                if (haRect != null)
                {
                    haRect.anchorMin = Vector2.zero;
                    haRect.anchorMax = Vector2.one;
                    haRect.anchoredPosition = Vector2.zero;
                    haRect.sizeDelta = new Vector2(-20f, 0f);
                }

                Transform handleTf = handleAreaTf.Find("Handle");
                if (handleTf != null)
                {
                    RectTransform hRect = handleTf as RectTransform;
                    if (hRect != null)
                    {
                        // Handle has vertical stretch anchors (0..1), so y=8 adds 4px top and 4px bottom
                        hRect.sizeDelta = new Vector2(24f, 8f);
                    }

                    Image hImg = handleTf.GetComponent<Image>();
                    if (hImg != null)
                    {
                        hImg.color = Color.white;
                    }
                }
            }
        }

        // Clean, natural "BGM" or "SFX" label in dark ink well above the slider
        string titleStr = isBgm ? "BGM" : "SFX";
        Text titleLabel = EnsureLabel(
            rowTf,
            "ChannelTitle",
            titleStr,
            pixelFont,
            24,
            new Color(0.24f, 0.13f, 0.11f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-73f, 26f),
            new Vector2(120f, 28f),
            useOutline: false,
            outlineColor: Color.clear);
        if (titleLabel != null) titleLabel.alignment = TextAnchor.MiddleLeft;

        // Clean volume percentage in warm brown ink well above the slider right edge
        Text pctLabel = EnsureLabel(
            rowTf,
            "VolumeValueText",
            "100%",
            pixelFont,
            22,
            new Color(0.38f, 0.23f, 0.18f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(182f, 26f),
            new Vector2(110f, 28f),
            useOutline: false,
            outlineColor: Color.clear);
        if (pctLabel != null) pctLabel.alignment = TextAnchor.MiddleRight;

        SoundControl sc = rowTf.GetComponentInChildren<SoundControl>(true);
        if (sc != null)
        {
            sc.RefreshVisuals();
        }
    }

    private void LayoutActionButtons(Transform frameTf, Font pixelFont)
    {
        Transform guideBtn = frameTf.Find("Guide");
        Transform licenseBtn = frameTf.Find("License");
        Transform closeBtn = frameTf.Find("Close");
        if (closeBtn == null) closeBtn = frameTf.Find("Back");
        Transform quitBtn = frameTf.Find("Quit");

        bool hasGuide = guideBtn != null && guideBtn.gameObject.activeSelf;
        bool hasLicense = licenseBtn != null && licenseBtn.gameObject.activeSelf;
        bool hasClose = closeBtn != null && closeBtn.gameObject.activeSelf;
        bool hasQuit = quitBtn != null && quitBtn.gameObject.activeSelf;

        if (hasGuide && hasLicense && hasClose && hasQuit)
        {
            StyleActionButton(guideBtn, "Guide", new Vector2(-125f, 162f), new Vector2(224f, 64f), pixelFont, isDanger: false);
            StyleActionButton(licenseBtn, "License", new Vector2(125f, 162f), new Vector2(224f, 64f), pixelFont, isDanger: false);
            StyleActionButton(closeBtn, "Back", new Vector2(-125f, 78f), new Vector2(224f, 64f), pixelFont, isDanger: false);
            StyleActionButton(quitBtn, "Quit", new Vector2(125f, 78f), new Vector2(224f, 64f), pixelFont, isDanger: true);
        }
        else if (hasGuide && hasLicense && hasClose)
        {
            StyleActionButton(guideBtn, "Guide", new Vector2(-125f, 162f), new Vector2(224f, 64f), pixelFont, isDanger: false);
            StyleActionButton(licenseBtn, "License", new Vector2(125f, 162f), new Vector2(224f, 64f), pixelFont, isDanger: false);
            StyleActionButton(closeBtn, "Close", new Vector2(0f, 78f), new Vector2(474f, 64f), pixelFont, isDanger: false);
        }
        else
        {
            if (licenseBtn != null)
                StyleActionButton(licenseBtn, "License", new Vector2(-125f, 120f), new Vector2(224f, 64f), pixelFont, isDanger: false);
            if (closeBtn != null)
                StyleActionButton(closeBtn, "Close", new Vector2(125f, 120f), new Vector2(224f, 64f), pixelFont, isDanger: false);
        }
    }

    private static void StyleActionButton(Transform btnTf, string labelText, Vector2 anchoredPos, Vector2 size, Font pixelFont, bool isDanger)
    {
        if (btnTf == null) return;

        RectTransform rt = btnTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        Image btnImg = btnTf.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.color = Color.white;
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1f;
        }

        Text txt = btnTf.GetComponentInChildren<Text>(true);
        if (txt != null)
        {
            if (pixelFont != null) txt.font = pixelFont;
            txt.text = labelText;
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = isDanger
                ? new Color(0.98f, 0.92f, 0.88f, 1f)
                : new Color(0.22f, 0.12f, 0.10f, 1f);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = txt.GetComponent<Outline>();
            if (isDanger)
            {
                if (outline == null) outline = txt.gameObject.AddComponent<Outline>();
                outline.enabled = true;
                outline.effectColor = new Color(0.24f, 0.10f, 0.09f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else if (outline != null)
            {
                outline.enabled = false;
            }
        }
    }

    public static void StyleGuidePanel(GameObject guidePanel)
    {
        if (guidePanel == null) return;
        Font pixelFont = GetPixelFont(guidePanel.transform.root);

        Transform closeTf = guidePanel.transform.Find("Close");
        if (closeTf != null)
        {
            StyleActionButton(closeTf, "Close", new Vector2(0f, 48f), new Vector2(224f, 64f), pixelFont, isDanger: false);
        }
    }

    public static void StyleLicensePanel(GameObject licensePanel)
    {
        if (licensePanel == null) return;
        Font pixelFont = GetPixelFont(licensePanel.transform.root);

        Transform closeTf = licensePanel.transform.Find("Close");
        if (closeTf != null)
        {
            RectTransform rt = closeTf as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(800f, 950f);
                rt.sizeDelta = new Vector2(200f, 64f);
            }

            Image btnImg = closeTf.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = Color.white;
                btnImg.type = Image.Type.Sliced;
                btnImg.pixelsPerUnitMultiplier = 1f;
            }

            Text txt = closeTf.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                if (pixelFont != null) txt.font = pixelFont;
                txt.text = "Back";
                txt.fontSize = 26;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(0.22f, 0.12f, 0.10f, 1f);
            }
        }
    }

    private static Text EnsureLabel(
        Transform parent,
        string name,
        string content,
        Font font,
        int fontSize,
        Color color,
        Vector2 anchor,
        Vector2 anchoredPos,
        Vector2 sizeDelta,
        bool useOutline,
        Color outlineColor)
    {
        Transform child = parent.Find(name);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = child.gameObject;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Text txt = go.GetComponent<Text>();
        if (font != null) txt.font = font;
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;

        Outline outline = go.GetComponent<Outline>();
        if (useOutline)
        {
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }
        else if (outline != null)
        {
            outline.enabled = false;
        }

        return txt;
    }
}
