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

        if (settingPanel != null && settingPanel.transform.parent != null)
        {
            Transform menuTf = settingPanel.transform.parent.Find("MenuPanel");
            if (menuTf != null)
            {
                StyleMainMenuButtons(menuTf);
            }
        }

        if (settingPanel != null) AttachButtonClickSounds(settingPanel.transform.root);
        else if (guidePanel != null) AttachButtonClickSounds(guidePanel.transform.root);
        else if (licensePanel != null) AttachButtonClickSounds(licensePanel.transform.root);
    }

    public static void AttachButtonClickSounds(Transform root)
    {
        if (root == null) return;
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            AttachButtonClickSound(buttons[i]);
        }
    }

    public static void AttachButtonClickSound(Button btn)
    {
        if (btn == null) return;
        if (btn.GetComponent<UIButtonSound>() == null)
        {
            btn.gameObject.AddComponent<UIButtonSound>();
        }
    }

    private static void StyleMainMenuButtons(Transform menuTf)
    {
        StyleMainMenuLabel(menuTf.Find("Start"), new Color(0.08f, 0.18f, 0.10f, 0.92f));
        StyleMainMenuLabel(menuTf.Find("Setting"), new Color(0.08f, 0.12f, 0.22f, 0.92f));
        StyleMainMenuLabel(menuTf.Find("Exit"), new Color(0.22f, 0.08f, 0.08f, 0.92f));
    }

    private static void StyleMainMenuLabel(Transform btnTf, Color outlineColor)
    {
        if (btnTf == null) return;
        Text txt = btnTf.GetComponentInChildren<Text>(true);
        if (txt == null) return;

        Outline outline = txt.GetComponent<Outline>();
        if (outline == null) outline = txt.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
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

        RectTransform rootRt = guidePanel.transform as RectTransform;
        if (rootRt != null)
        {
            rootRt.localScale = Vector3.one;
        }

        Image backdropImg = guidePanel.GetComponent<Image>();
        if (backdropImg != null)
        {
            backdropImg.sprite = null;
            backdropImg.type = Image.Type.Simple;
            backdropImg.color = new Color(0.06f, 0.05f, 0.05f, 0.72f);
        }

        Transform boardTf = guidePanel.transform.Find("GuideBoard");
        if (boardTf != null)
        {
            RectTransform boardRt = boardTf as RectTransform;
            if (boardRt != null)
            {
                boardRt.anchorMin = new Vector2(0.5f, 0.5f);
                boardRt.anchorMax = new Vector2(0.5f, 0.5f);
                boardRt.pivot = new Vector2(0.5f, 0.5f);
                boardRt.anchoredPosition = Vector2.zero;
                boardRt.sizeDelta = new Vector2(1180f, 760f);
                boardRt.localScale = Vector3.one;
            }
            Image boardImg = boardTf.GetComponent<Image>();
            if (boardImg != null)
            {
                boardImg.color = Color.white;
                boardImg.type = Image.Type.Sliced;
            }

            Transform stanceCardTf = boardTf.Find("StanceCard");
            Transform controlsCardTf = boardTf.Find("ControlsCard");
            if (stanceCardTf != null && controlsCardTf != null)
            {
                Image stanceCardImg = stanceCardTf.GetComponent<Image>();
                Image controlsCardImg = controlsCardTf.GetComponent<Image>();
                if (stanceCardImg != null && controlsCardImg != null)
                {
                    stanceCardImg.sprite = controlsCardImg.sprite;
                    stanceCardImg.type = controlsCardImg.type;
                    stanceCardImg.color = controlsCardImg.color;
                }

                Transform stanceHeaderTf = stanceCardTf.Find("StanceHeader");
                if (stanceHeaderTf != null)
                {
                    TMP_Text shTmp = stanceHeaderTf.GetComponent<TMP_Text>();
                    if (shTmp != null) shTmp.color = new Color(0.30f, 0.16f, 0.12f, 1f);
                }
            }
        }

        Transform titleTf = guidePanel.transform.Find("Title_Text");
        if (titleTf != null)
        {
            TMP_Text titleTmp = titleTf.GetComponent<TMP_Text>();
            if (titleTmp != null)
            {
                titleTmp.color = new Color(0.22f, 0.12f, 0.10f, 1f);
                titleTmp.fontSize = 40f;
                titleTmp.alignment = TextAlignmentOptions.Center;
            }
            RectTransform titleRt = titleTf as RectTransform;
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = new Vector2(0f, 316f);
                titleRt.sizeDelta = new Vector2(680f, 52f);
            }
        }

        Transform stanceTf = guidePanel.transform.Find("Stance");
        if (stanceTf != null)
        {
            Image stanceImg = stanceTf.GetComponent<Image>();
            if (stanceImg != null)
            {
                stanceImg.color = Color.white;
                stanceImg.preserveAspect = true;
            }
        }

        Transform controlsTf = guidePanel.transform.Find("Controls_Text");
        if (controlsTf != null)
        {
            TMP_Text ctrlTmp = controlsTf.GetComponent<TMP_Text>();
            if (ctrlTmp != null)
            {
                ctrlTmp.color = new Color(0.20f, 0.12f, 0.08f, 1f);
                ctrlTmp.fontSize = 23f;
                ctrlTmp.lineSpacing = 4f;
            }
        }

        StyleSkillLabel(guidePanel.transform.Find("R_Skill_Text"), new Color(0.64f, 0.11f, 0.14f, 1f), "Red Skill   :  Strong Attack");
        StyleSkillLabel(guidePanel.transform.Find("G_Skill_Text"), new Color(0.09f, 0.45f, 0.20f, 1f), "Green Skill :  Heal");
        StyleSkillLabel(guidePanel.transform.Find("B_Skill_Text"), new Color(0.11f, 0.27f, 0.66f, 1f), "Blue Skill  :  Stun (except Boss)");

        Transform closeTf = guidePanel.transform.Find("Close");
        if (closeTf != null)
        {
            StyleCenterButton(closeTf, "Close", new Vector2(0f, -314f), new Vector2(224f, 64f), pixelFont);
        }
    }

    private static void StyleSkillLabel(Transform tf, Color inkColor, string cleanText)
    {
        if (tf == null) return;
        TMP_Text tmp = tf.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = cleanText;
            tmp.color = inkColor;
            tmp.fontSize = 23f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    public static void StyleLicensePanel(GameObject licensePanel)
    {
        if (licensePanel == null) return;
        Font pixelFont = GetPixelFont(licensePanel.transform.root);

        Image backdropImg = licensePanel.GetComponent<Image>();
        if (backdropImg != null)
        {
            backdropImg.sprite = null;
            backdropImg.type = Image.Type.Simple;
            backdropImg.color = new Color(0.06f, 0.05f, 0.05f, 0.72f);
        }

        Transform scrollTf = licensePanel.transform.Find("Scroll View");
        if (scrollTf != null)
        {
            RectTransform scrollRt = scrollTf as RectTransform;
            if (scrollRt != null)
            {
                scrollRt.anchorMin = new Vector2(0.5f, 0.5f);
                scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
                scrollRt.pivot = new Vector2(0.5f, 0.5f);
                scrollRt.anchoredPosition = Vector2.zero;
                scrollRt.sizeDelta = new Vector2(920f, 780f);
                scrollRt.localScale = Vector3.one;
            }

            Image scrollBg = scrollTf.GetComponent<Image>();
            if (scrollBg != null)
            {
                scrollBg.color = Color.white;
                scrollBg.type = Image.Type.Sliced;
            }

            Text titleLabel = EnsureLabel(
                scrollTf,
                "HeaderTitle",
                "LICENSE",
                pixelFont,
                34,
                new Color(0.22f, 0.12f, 0.10f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 336f),
                new Vector2(400f, 48f),
                useOutline: false,
                outlineColor: Color.clear);
            if (titleLabel != null) titleLabel.alignment = TextAnchor.MiddleCenter;

            Transform ruleTf = scrollTf.Find("HeaderRule");
            GameObject ruleGo = ruleTf != null ? ruleTf.gameObject : new GameObject("HeaderRule", typeof(RectTransform), typeof(Image));
            if (ruleTf == null)
            {
                ruleGo.layer = scrollTf.gameObject.layer;
                ruleGo.transform.SetParent(scrollTf, false);
            }
            RectTransform ruleRt = ruleGo.GetComponent<RectTransform>();
            ruleRt.anchorMin = new Vector2(0.5f, 0.5f);
            ruleRt.anchorMax = new Vector2(0.5f, 0.5f);
            ruleRt.pivot = new Vector2(0.5f, 0.5f);
            ruleRt.anchoredPosition = new Vector2(0f, 298f);
            ruleRt.sizeDelta = new Vector2(790f, 3f);
            Image ruleImg = ruleGo.GetComponent<Image>();
            ruleImg.color = new Color(0.49f, 0.32f, 0.21f, 0.65f);
            ruleImg.raycastTarget = false;

            Transform viewportTf = scrollTf.Find("Viewport");
            if (viewportTf != null)
            {
                RectTransform vpRt = viewportTf as RectTransform;
                if (vpRt != null)
                {
                    vpRt.anchorMin = Vector2.zero;
                    vpRt.anchorMax = Vector2.one;
                    vpRt.offsetMin = new Vector2(58f, 122f);
                    vpRt.offsetMax = new Vector2(-82f, -110f);
                }

                Mask legacyMask = viewportTf.GetComponent<Mask>();
                if (legacyMask != null) Destroy(legacyMask);

                Image vpImg = viewportTf.GetComponent<Image>();
                if (vpImg != null) vpImg.enabled = false;

                if (viewportTf.GetComponent<RectMask2D>() == null)
                {
                    viewportTf.gameObject.AddComponent<RectMask2D>();
                }
            }

            Transform sbTf = scrollTf.Find("Scrollbar Vertical");
            if (sbTf != null)
            {
                RectTransform sbRt = sbTf as RectTransform;
                if (sbRt != null)
                {
                    sbRt.anchorMin = new Vector2(1f, 0f);
                    sbRt.anchorMax = new Vector2(1f, 1f);
                    sbRt.pivot = new Vector2(1f, 1f);
                    sbRt.anchoredPosition = new Vector2(-46f, -106f);
                    sbRt.sizeDelta = new Vector2(20f, -224f);
                }
            }

            LicenseScrollLayoutRefresher refresher = scrollTf.GetComponent<LicenseScrollLayoutRefresher>();
            if (refresher == null)
            {
                refresher = scrollTf.gameObject.AddComponent<LicenseScrollLayoutRefresher>();
            }
            refresher.RefreshNow(resetScrollTop: true);
        }

        Transform closeTf = licensePanel.transform.Find("Close");
        if (closeTf != null)
        {
            StyleCenterButton(closeTf, "Back", new Vector2(0f, -324f), new Vector2(224f, 64f), pixelFont);
        }
    }

    /// <summary>
    /// Computes exact wrapped TMP height once the Scroll View is active in hierarchy
    /// so text never overflows horizontally or vertically outside the parchment ledger.
    /// </summary>
    private sealed class LicenseScrollLayoutRefresher : MonoBehaviour
    {
        private bool _needsPostEnableRefresh;

        private void OnEnable()
        {
            _needsPostEnableRefresh = true;
            RefreshNow(resetScrollTop: true);
        }

        private void LateUpdate()
        {
            if (!_needsPostEnableRefresh) return;
            _needsPostEnableRefresh = false;
            RefreshNow(resetScrollTop: true);
        }

        public void RefreshNow(bool resetScrollTop)
        {
            ScrollRect sr = GetComponent<ScrollRect>();
            if (sr != null)
            {
                sr.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
            Transform viewportTf = transform.Find("Viewport");
            if (viewportTf == null) return;

            Transform contentTf = viewportTf.Find("Content");
            if (contentTf == null) return;

            VerticalLayoutGroup vlg = contentTf.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Destroy(vlg);

            ContentSizeFitter csf = contentTf.GetComponent<ContentSizeFitter>();
            if (csf != null) Destroy(csf);

            RectTransform vpRt = viewportTf as RectTransform;
            RectTransform contentRt = contentTf as RectTransform;
            TMP_Text licenseTmp = contentTf.GetComponentInChildren<TMP_Text>(true);
            if (vpRt == null || contentRt == null || licenseTmp == null) return;

            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(58f, 122f);
            vpRt.offsetMax = new Vector2(-82f, -110f);

            licenseTmp.color = new Color(0.20f, 0.12f, 0.08f, 1f);
            licenseTmp.fontSize = 22f;
            licenseTmp.lineSpacing = 4f;
            licenseTmp.alignment = TextAlignmentOptions.Top;
            licenseTmp.textWrappingMode = TextWrappingModes.Normal;
            licenseTmp.overflowMode = TextOverflowModes.Overflow;
            licenseTmp.margin = Vector4.zero;
            licenseTmp.maskable = true;

            if (gameObject.activeInHierarchy)
            {
                Canvas.ForceUpdateCanvases();
            }

            float vpWidth = vpRt.rect.width;
            if (vpWidth < 100f) vpWidth = 788f;
            float vpHeight = vpRt.rect.height;
            if (vpHeight < 100f) vpHeight = 556f;

            float horizontalPadding = 64f;
            float textWidth = Mathf.Max(200f, vpWidth - horizontalPadding);

            RectTransform textRt = licenseTmp.rectTransform;
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);

            licenseTmp.ForceMeshUpdate(true, true);
            Vector2 preferred = licenseTmp.GetPreferredValues(licenseTmp.text, textWidth, 0f);
            float textHeight = Mathf.Max(200f, preferred.y + 24f);

            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, Mathf.Max(vpHeight, textHeight + 52f));

            textRt.sizeDelta = new Vector2(-horizontalPadding, textHeight);
            textRt.anchoredPosition = new Vector2(0f, -20f);

            if (sr != null)
            {
                sr.scrollSensitivity = 28f;
                if (resetScrollTop)
                {
                    sr.verticalNormalizedPosition = 1f;
                }
            }
        }
    }

    private static void StyleCenterButton(Transform btnTf, string labelText, Vector2 anchoredPos, Vector2 size, Font pixelFont)
    {
        if (btnTf == null) return;

        RectTransform rt = btnTf as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
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
            txt.color = new Color(0.22f, 0.12f, 0.10f, 1f);
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = txt.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
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

public class UIButtonSound : MonoBehaviour
{
    private Button cachedButton;

    private void Awake()
    {
        cachedButton = GetComponent<Button>();
        if (cachedButton != null)
        {
            cachedButton.onClick.AddListener(PlayClickSound);
        }
    }

    private void OnDestroy()
    {
        if (cachedButton != null)
        {
            cachedButton.onClick.RemoveListener(PlayClickSound);
        }
    }

    private static void PlayClickSound()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayUIClick();
        }
    }
}
