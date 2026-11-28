using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameStarting : MonoBehaviour
{
    public GameObject settingPanel;
    public GameObject licensePanel;
    public GameObject mainMenuPanel;
    public GameObject GuidePanel;

    private GameObject continueButtonObj;
    private GameObject newGameWarningPanel;
    private static Material cachedGrayscaleMat;

    void Awake()
    {
        if (GameManager.IsNonGameplayScene(gameObject.scene.name))
        {
            GameManager.CleanupGameplaySession();
        }

        if (gameObject.scene.name == "MainMenu")
        {
            SetupMainMenuContinueUI();
        }

        SettingsUIStyler.EnsureStyled(settingPanel, GuidePanel, licensePanel);

        if (gameObject.scene.name == "MainMenu")
        {
            RefreshMainMenuContinueState();
        }
    }

    private void Start()
    {
        if (gameObject.scene.name == "MainMenu")
        {
            RefreshMainMenuContinueState();
        }
    }

    private void OnEnable()
    {
        if (gameObject.scene.name == "MainMenu")
        {
            RefreshMainMenuContinueState();
        }
    }

    public void PlayGame()
    {
        if (gameObject.scene.name == "MainMenu" && SaveManager.HasSaveData())
        {
            OpenNewGameWarning();
            return;
        }

        SaveManager.ClearSaveData();
        StartNewTutorialGame();
    }

    public void ContinueGame()
    {
        if (!SaveManager.HasSaveData())
        {
            RefreshMainMenuContinueState();
            return;
        }

        HideAllMenuOverlays();
        SaveManager.ContinueFromSave();
    }

    public void ConfirmStartNewGame()
    {
        CloseNewGameWarning();
        SaveManager.ClearSaveData();
        StartNewTutorialGame();
    }

    public void CloseNewGameWarning()
    {
        if (newGameWarningPanel != null)
        {
            newGameWarningPanel.SetActive(false);
        }
        ClearEventSelection();
    }

    private void OpenNewGameWarning()
    {
        Transform canvasTf = GetMenuCanvasTransform();
        if (canvasTf != null && newGameWarningPanel == null)
        {
            EnsureNewGameWarningPanel(canvasTf);
        }

        if (newGameWarningPanel != null)
        {
            newGameWarningPanel.transform.SetAsLastSibling();
            newGameWarningPanel.SetActive(true);
        }
        else
        {
            SaveManager.ClearSaveData();
            StartNewTutorialGame();
        }
    }

    private void StartNewTutorialGame()
    {
        HideAllMenuOverlays();

        if (GameManager.instance != null)
        {
            GameManager.instance.LoadScene("Tutorial");
        }
        else
        {
            SceneManager.LoadSceneAsync("Tutorial");
        }
    }

    private void HideAllMenuOverlays()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
        if (licensePanel != null) licensePanel.SetActive(false);
        if (newGameWarningPanel != null) newGameWarningPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (GuidePanel != null) GuidePanel.SetActive(false);
    }

    // 종료 버튼 누를 때 호출
    public void QuitGame()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && !GameManager.IsNonGameplayScene(activeScene.name))
        {
            SaveManager.SaveCurrentState();
        }

        Debug.Log("게임 종료!");
        Application.Quit();
    }

    // 설정창 열기/닫기
    public void OpenSetting()
    {
        SettingsUIStyler.EnsureStyled(settingPanel, GuidePanel, licensePanel);
        if (settingPanel != null) settingPanel.SetActive(true);
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
    }

    public void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
        if (licensePanel != null) licensePanel.SetActive(false);
        if (GuidePanel != null) GuidePanel.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
    }

    void Update()
    {
        if (Player.instance != null) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (newGameWarningPanel != null && newGameWarningPanel.activeSelf)
            {
                CloseNewGameWarning();
            }
            else if (GuidePanel != null && GuidePanel.activeSelf)
            {
                CloseGuide();
            }
            else if (licensePanel != null && licensePanel.activeSelf)
            {
                CloseLicense();
            }
            else if (settingPanel != null && settingPanel.activeSelf)
            {
                CloseSetting();
            }
        }
    }

    // 라이선스창 열기/닫기
    public void OpenLicense()
    {
        SettingsUIStyler.StyleLicensePanel(licensePanel);
        if (licensePanel != null) licensePanel.SetActive(true);
    }

    public void CloseLicense()
    {
        if (licensePanel != null) licensePanel.SetActive(false);
    }

    public void OpenGuide()
    {
        SettingsUIStyler.StyleGuidePanel(GuidePanel);
        if (GuidePanel != null) GuidePanel.SetActive(true);
    }

    public void CloseGuide()
    {
        if (GuidePanel != null) GuidePanel.SetActive(false);
    }

    private Transform GetMenuCanvasTransform()
    {
        if (settingPanel != null && settingPanel.transform.parent != null)
        {
            return settingPanel.transform.parent;
        }
        Canvas canvas = FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private void SetupMainMenuContinueUI()
    {
        Transform canvasTf = GetMenuCanvasTransform();
        if (canvasTf == null) return;

        Transform menuTf = canvasTf.Find("MenuPanel");
        if (menuTf != null)
        {
            EnsureContinueButton(menuTf);
            RefreshMainMenuContinueState();
        }

        EnsureNewGameWarningPanel(canvasTf);
    }

    private void EnsureContinueButton(Transform menuTf)
    {
        Transform existing = menuTf.Find("Continue");
        if (existing != null)
        {
            continueButtonObj = existing.gameObject;
            Button existingBtn = existing.GetComponent<Button>();
            if (existingBtn != null)
            {
                if (existingBtn.onClick.GetPersistentEventCount() == 0)
                {
                    existingBtn.onClick.RemoveListener(ContinueGame);
                    existingBtn.onClick.AddListener(ContinueGame);
                }
                SettingsUIStyler.AttachButtonClickSound(existingBtn);
            }
            return;
        }

        Transform startTf = menuTf.Find("Start");
        if (startTf == null) return;

        GameObject btnObj = Instantiate(startTf.gameObject, menuTf, false);
        btnObj.name = "Continue";
        btnObj.transform.SetSiblingIndex(startTf.GetSiblingIndex() + 1);

        Transform oldTmp = btnObj.transform.Find("TMPLabel");
        if (oldTmp != null)
        {
            DestroyImmediate(oldTmp.gameObject);
        }

        Image startImg = startTf.GetComponent<Image>();
        Image img = btnObj.GetComponent<Image>();
        if (img != null && startImg != null)
        {
            img.sprite = startImg.sprite;
            img.type = startImg.type;
            img.pixelsPerUnitMultiplier = startImg.pixelsPerUnitMultiplier;
            img.color = Color.white;
        }

        Text legacyTxt = btnObj.GetComponentInChildren<Text>(true);
        if (legacyTxt != null)
        {
            legacyTxt.enabled = true;
            legacyTxt.text = "CONTINUE";
            legacyTxt.fontSize = 32;
        }

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
            cb.pressedColor = new Color(0.76f, 0.7f, 0.64f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(ContinueGame);
            SettingsUIStyler.AttachButtonClickSound(btn);
        }

        continueButtonObj = btnObj;
    }

    public void RefreshMainMenuContinueState()
    {
        Transform canvasTf = GetMenuCanvasTransform();
        Transform menuTf = canvasTf != null ? canvasTf.Find("MenuPanel") : null;
        if (menuTf == null) return;

        if (continueButtonObj == null)
        {
            Transform existing = menuTf.Find("Continue");
            if (existing != null) continueButtonObj = existing.gameObject;
        }

        Transform startTf = menuTf.Find("Start");
        Transform settingTf = menuTf.Find("Setting");
        Transform exitTf = menuTf.Find("Exit");

        SetButtonAnchoredPos(startTf, new Vector2(0f, 34f));
        SetButtonAnchoredPos(continueButtonObj != null ? continueButtonObj.transform : null, new Vector2(0f, -60f));
        SetButtonAnchoredPos(settingTf, new Vector2(0f, -154f));
        SetButtonAnchoredPos(exitTf, new Vector2(0f, -248f));

        bool hasSave = SaveManager.HasSaveData();

        if (continueButtonObj != null)
        {
            continueButtonObj.SetActive(true);

            Button continueBtn = continueButtonObj.GetComponent<Button>();
            if (continueBtn != null)
            {
                continueBtn.interactable = hasSave;

                ColorBlock cb = continueBtn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1f, 0.92f, 0.72f, 1f);
                cb.pressedColor = new Color(0.76f, 0.7f, 0.64f, 1f);
                cb.selectedColor = cb.highlightedColor;
                cb.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                continueBtn.colors = cb;
            }

            Image continueImg = continueButtonObj.GetComponent<Image>();
            Image startImg = startTf != null ? startTf.GetComponent<Image>() : null;
            if (continueImg != null)
            {
                if (startImg != null && startImg.sprite != null)
                {
                    continueImg.sprite = startImg.sprite;
                    continueImg.type = startImg.type;
                    continueImg.pixelsPerUnitMultiplier = startImg.pixelsPerUnitMultiplier;
                }

                if (hasSave)
                {
                    continueImg.material = null;
                    continueImg.color = Color.white;
                }
                else
                {
                    Material grayMat = GetGrayscaleButtonMaterial();
                    if (grayMat != null)
                    {
                        continueImg.material = grayMat;
                    }
                    continueImg.color = new Color(0.92f, 0.92f, 0.92f, 1f);
                }
            }

            ApplyContinueLabelVisual(continueButtonObj.transform, hasSave);
        }
    }

    private static Material GetGrayscaleButtonMaterial()
    {
        if (cachedGrayscaleMat != null) return cachedGrayscaleMat;

        Shader shader = Shader.Find("UI/Grayscale");
        if (shader == null)
        {
            shader = Resources.Load<Shader>("Shaders/UIGrayscale");
        }
        if (shader == null) return null;

        cachedGrayscaleMat = new Material(shader);
        cachedGrayscaleMat.name = "UIButtonGrayscale_Runtime";
        cachedGrayscaleMat.SetFloat("_GrayAmount", 1f);
        cachedGrayscaleMat.SetFloat("_Brightness", 0.74f);
        return cachedGrayscaleMat;
    }

    private static void ApplyContinueLabelVisual(Transform continueTf, bool hasSave)
    {
        if (continueTf == null) return;

        Color textColor = hasSave
            ? new Color(0.96f, 0.98f, 0.92f, 1f)
            : new Color(0.76f, 0.76f, 0.76f, 0.85f);
        Color outlineColor = hasSave
            ? new Color(0.08f, 0.18f, 0.10f, 0.92f)
            : new Color(0.14f, 0.14f, 0.14f, 0.88f);

        Transform tmpTf = continueTf.Find("TMPLabel");
        if (tmpTf != null)
        {
            TextMeshProUGUI tmp = tmpTf.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = textColor;
                tmp.outlineColor = outlineColor;
                tmp.outlineWidth = 0.18f;
            }
        }

        Text legacyTxt = continueTf.GetComponentInChildren<Text>(true);
        if (legacyTxt != null)
        {
            legacyTxt.color = textColor;
            Outline outline = legacyTxt.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = outlineColor;
            }
        }
    }

    private static void SetButtonAnchoredPos(Transform tf, Vector2 pos)
    {
        RectTransform rt = tf as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = pos;
        }
    }

    private void EnsureNewGameWarningPanel(Transform canvasTf)
    {
        if (newGameWarningPanel != null) return;

        Transform existing = canvasTf.Find("NewGameWarningPanel");
        if (existing != null)
        {
            newGameWarningPanel = existing.gameObject;
            return;
        }

        GameObject panelObj = new GameObject("NewGameWarningPanel", typeof(RectTransform), typeof(Image));
        panelObj.layer = canvasTf.gameObject.layer;
        panelObj.transform.SetParent(canvasTf, false);

        RectTransform panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        Image backdropImg = panelObj.GetComponent<Image>();
        backdropImg.color = new Color(0.06f, 0.05f, 0.05f, 0.74f);
        backdropImg.raycastTarget = true;

        GameObject boardObj = new GameObject("WarningBoard", typeof(RectTransform), typeof(Image));
        boardObj.layer = panelObj.layer;
        boardObj.transform.SetParent(panelObj.transform, false);

        RectTransform boardRt = boardObj.GetComponent<RectTransform>();
        boardRt.anchorMin = new Vector2(0.5f, 0.5f);
        boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = Vector2.zero;
        boardRt.sizeDelta = new Vector2(580f, 350f);

        Image boardImg = boardObj.GetComponent<Image>();
        Sprite parchment = SettingsUIStyler.GetParchmentFrameSprite();
        if (parchment != null)
        {
            boardImg.sprite = parchment;
            boardImg.type = Image.Type.Sliced;
            boardImg.pixelsPerUnitMultiplier = 1f;
            boardImg.color = Color.white;
        }
        else
        {
            boardImg.color = new Color(0.92f, 0.84f, 0.72f, 1f);
        }

        TMP_FontAsset tmpFont = SettingsUIStyler.GetPixelTmpFont(canvasTf);

        CreateWarningTmpText(
            boardObj.transform,
            "TitleText",
            "경고",
            tmpFont,
            34f,
            new Color(0.58f, 0.14f, 0.12f, 1f),
            new Vector2(0f, 118f),
            new Vector2(460f, 46f));

        GameObject ruleObj = new GameObject("HeaderRule", typeof(RectTransform), typeof(Image));
        ruleObj.layer = boardObj.layer;
        ruleObj.transform.SetParent(boardObj.transform, false);
        RectTransform ruleRt = ruleObj.GetComponent<RectTransform>();
        ruleRt.anchorMin = new Vector2(0.5f, 0.5f);
        ruleRt.anchorMax = new Vector2(0.5f, 0.5f);
        ruleRt.pivot = new Vector2(0.5f, 0.5f);
        ruleRt.anchoredPosition = new Vector2(0f, 82f);
        ruleRt.sizeDelta = new Vector2(460f, 3f);
        Image ruleImg = ruleObj.GetComponent<Image>();
        ruleImg.color = new Color(0.49f, 0.32f, 0.21f, 0.55f);
        ruleImg.raycastTarget = false;

        TMP_Text messageText = CreateWarningTmpText(
            boardObj.transform,
            "MessageText",
            "저장된 데이터가 사라집니다.\n새로 시작하시겠습니까?",
            tmpFont,
            24f,
            new Color(0.22f, 0.12f, 0.10f, 1f),
            new Vector2(0f, 6f),
            new Vector2(480f, 110f));
        messageText.lineSpacing = 8f;
        messageText.textWrappingMode = TextWrappingModes.Normal;

        Transform frameTf = settingPanel != null ? settingPanel.transform.Find("Scroll View") : null;
        Sprite normalBtnSprite = SettingsUIStyler.GetButtonSprite(frameTf);
        Sprite dangerBtnSprite = SettingsUIStyler.FindSpriteByName("Chroma_Button_Red")
            ?? SettingsUIStyler.FindSpriteByName("Chroma_Button_Danger")
            ?? normalBtnSprite;

        CreateWarningButton(
            boardObj.transform,
            "YesButton",
            "예",
            new Vector2(-120f, -104f),
            new Vector2(200f, 62f),
            normalBtnSprite,
            tmpFont,
            new Color(0.22f, 0.12f, 0.10f, 1f),
            useOutline: false,
            Color.clear,
            ConfirmStartNewGame);

        CreateWarningButton(
            boardObj.transform,
            "NoButton",
            "아니오",
            new Vector2(120f, -104f),
            new Vector2(200f, 62f),
            dangerBtnSprite,
            tmpFont,
            new Color(0.98f, 0.92f, 0.88f, 1f),
            useOutline: true,
            new Color(0.24f, 0.10f, 0.09f, 0.9f),
            CloseNewGameWarning);

        panelObj.SetActive(false);
        newGameWarningPanel = panelObj;
    }

    private static TMP_Text CreateWarningTmpText(
        Transform parent,
        string name,
        string text,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        Vector2 anchoredPos,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void CreateWarningButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        Sprite sprite,
        TMP_FontAsset font,
        Color textColor,
        bool useOutline,
        Color outlineColor,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.layer = parent.gameObject.layer;
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
        }
        img.color = Color.white;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(onClick);
        SettingsUIStyler.AttachButtonClickSound(btn);

        TMP_Text labelTmp = CreateWarningTmpText(
            btnObj.transform,
            "Label",
            label,
            font,
            26f,
            textColor,
            Vector2.zero,
            size);

        RectTransform labelRt = labelTmp.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        if (useOutline)
        {
            labelTmp.outlineColor = outlineColor;
            labelTmp.outlineWidth = 0.16f;
        }
    }

    private static void ClearEventSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}