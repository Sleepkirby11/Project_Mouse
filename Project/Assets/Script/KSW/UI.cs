using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using Unity.VisualScripting;

public class UI : MonoBehaviour
{
    // 어디서나 UI에 접근할 수 있도록 싱글톤 인스턴스 생성
    public static UI Instance { get; private set; }

    //플레이어 status에 미리 접근
    PlayerStatus playerStatus;

    [Header("체력바 설정")]
    [SerializeField] private Slider hpSlider;

    [Header("잉크 게이지 설정")]
    [SerializeField] private Slider inkSlider;

    [Header("쿨타임 게이지 설정")]
    [SerializeField] private Slider coolTimeSlider;

    [Header("특수 잉크 게이지 설정")]
    [SerializeField] private Slider specialInkSlider;

    [Header("보스 체력바 설정")]
    [SerializeField] private Slider bossHpSlider;
    [SerializeField] private Text bossNameText;
    private BossUIStyler bossUIStyler;
    private PlayerHudStyler playerHudStyler;

    [Header("대화창")]
    public GameObject talkPanel;
    public Text talkText;
    public GameObject scanObject;
    public bool isAction;
    public int talkIndex;

    [Header("팔레트")]
    public GameObject pal;
    [SerializeField, Range(0.25f, 1f)] private float palWheelScale = 0.45f;
    private Image[] palSliceImages;
    private RectTransform[] palSliceRects;
    private Material[] palSliceMaterials;
    private float[] palSliceWeights;
    private int selectedPalIndex = -1;
    private const float PalSelectedScale = 1.12f;
    private const float PalUnselectedScale = 0.94f;
    private static readonly Color PalSelectedColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color PalUnselectedColor = new Color(0.62f, 0.62f, 0.62f, 0.32f);
    private static readonly int PropColorTop = Shader.PropertyToID("_ColorTop");
    private static readonly int PropColorBottom = Shader.PropertyToID("_ColorBottom");
    private static readonly int PropRimColor = Shader.PropertyToID("_RimColor");
    private static readonly int PropSelectWeight = Shader.PropertyToID("_SelectWeight");
    private static readonly int PropSliceBounds = Shader.PropertyToID("_SliceBounds");

    [Header("설정 패널")]
    public GameObject settingPanel;

    [Header("라이센스 패널")]
    public GameObject licensePanel; 

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (palSliceMaterials != null)
        {
            for (int i = 0; i < palSliceMaterials.Length; i++)
            {
                if (palSliceMaterials[i] != null)
                {
                    Destroy(palSliceMaterials[i]);
                    palSliceMaterials[i] = null;
                }
            }
        }
    }

    void Start()
    {
        EnsureBossUIStyler();
        EnsurePlayerHudStyler();
        RefreshPlayerReference();
    }

    void Update()
    {
        UpdatePalVisuals();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 전환되거나 플레이어 사망 후 재시작될 때 보스 HP 바 숨김
        HideBossHPBar();
        HideDialogue(); // 씬 전환 시 대화창 자동 닫기
        RefreshPlayerReference();
    }

    private void RefreshPlayerReference()
    {
        PlayerStatus targetStatus = null;
        if (Player.instance != null)
        {
            targetStatus = Player.instance.GetComponent<PlayerStatus>();
        }
        else
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                targetStatus = playerObj.GetComponent<PlayerStatus>();
            }
        }

        if (targetStatus != null)
        {
            playerStatus = targetStatus;
            EnsurePlayerHudStyler();
            if (playerHudStyler != null && playerStatus.MaxHP > 0)
            {
                playerHudStyler.SetHPImmediate(playerStatus.HP / playerStatus.MaxHP);
            }
            UpdateHPBar();
            UpdateCoolTimeBar();
            UpdateInkBar();
        }
    }

    // 게임 시작 버튼 누를 때 호출
    public void StartGame()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadScene("Tutorial");
        }
        else
        {
            SceneManager.LoadSceneAsync("Tutorial");
        }
    }

    //설정 메뉴 추가 예정

    // 종료 버튼 누를 때 호출
    public void ExitGame()
    {
        Debug.Log("게임 종료!"); // 에디터에서는 안 꺼지므로 로그로 확인
        Application.Quit();     // 실제 빌드된 게임에서는 프로그램 종료
    }

    //플레이어 체력 상태 업데이트
    public void UpdateHPBar()
    {
        if (hpSlider != null && playerStatus != null)
        {
            float ratio = playerStatus.MaxHP > 0 ? playerStatus.HP / playerStatus.MaxHP : 0f;
            hpSlider.value = ratio;
            EnsurePlayerHudStyler()?.NotifyHPChanged(ratio);
        }
    }

    public void SnapHPBarImmediate()
    {
        if (playerStatus == null) RefreshPlayerReference();
        if (hpSlider != null && playerStatus != null)
        {
            float ratio = playerStatus.MaxHP > 0 ? playerStatus.HP / playerStatus.MaxHP : 0f;
            hpSlider.value = ratio;
            EnsurePlayerHudStyler()?.SetHPImmediate(ratio);
        }
    }

    private PlayerHudStyler EnsurePlayerHudStyler()
    {
        if (playerHudStyler == null && hpSlider != null)
        {
            playerHudStyler = PlayerHudStyler.AttachAndStyle(hpSlider, inkSlider, specialInkSlider, coolTimeSlider);
        }
        return playerHudStyler;
    }

    public Font GetUIFont()
    {
        if (bossNameText != null && bossNameText.font != null)
            return bossNameText.font;
        if (talkText != null && talkText.font != null)
            return talkText.font;
        return null;
    }

    private BossUIStyler EnsureBossUIStyler()
    {
        if (bossUIStyler == null && bossHpSlider != null)
        {
            bossUIStyler = BossUIStyler.AttachOrGet(bossHpSlider, bossNameText);
        }
        return bossUIStyler;
    }

    // 보스 체력바 활성화 및 초기 체력 설정
    public void ShowBossHPBar(float initialRatio)
    {
        ShowBossHPBar("Boss", Color.white, initialRatio);
    }

    public void ShowBossHPBar(string bossName, float initialRatio)
    {
        ShowBossHPBar(bossName, Color.white, initialRatio);
    }

    public void ShowBossHPBar(string bossName, Color textColor, float initialRatio)
    {
        Debug.Log($"[UI] ShowBossHPBar 호출됨. 이름: {bossName}, 색상: {textColor}, 초기 비율: {initialRatio}");
        BossUIStyler styler = EnsureBossUIStyler();
        if (styler != null)
        {
            styler.Show(bossName, textColor, initialRatio);
            Debug.Log($"[UI] bossHpSlider가 이제 활성화되었습니다. 값: {bossHpSlider.value}");
            return;
        }

        if (bossHpSlider != null)
        {
            bossHpSlider.gameObject.SetActive(true);
            bossHpSlider.value = initialRatio;
            Debug.Log($"[UI] bossHpSlider가 이제 활성화되었습니다. 값: {bossHpSlider.value}");
        }
        else
        {
            Debug.LogError("[UI] bossHpSlider가 null입니다! 인스펙터에서 UIManager (UI 스크립트)의 Boss HP Slider 필드에 EnemyHP 슬라이더를 할당해 주세요.");
        }

        if (bossNameText != null)
        {
            bossNameText.gameObject.SetActive(true);
            bossNameText.text = bossName;
            bossNameText.color = textColor;
        }
    }

    // 보스 체력바 상태 업데이트
    public void UpdateBossHPBar(float currentHP, float maxHP)
    {
        Debug.Log($"[UI] UpdateBossHPBar 호출됨: {currentHP}/{maxHP}");
        if (bossHpSlider != null && maxHP > 0)
        {
            BossUIStyler styler = EnsureBossUIStyler();
            if (styler != null)
            {
                styler.UpdateHP(currentHP, maxHP);
            }
            else
            {
                bossHpSlider.value = currentHP / maxHP;
            }
        }
        else if (bossHpSlider == null)
        {
            Debug.LogWarning("[UI] UpdateBossHPBar: bossHpSlider가 null입니다.");
        }
    }

    public void UpdateCoolTimeBar()
    {
        if (coolTimeSlider != null && playerStatus != null)
        {
            coolTimeSlider.value = playerStatus.currentCoolTime / playerStatus.coolTime;
        }
    }

    // 보스 체력바 비활성화
    public void HideBossHPBar()
    {
        Debug.Log("[UI] HideBossHPBar 호출됨.");
        BossUIStyler styler = EnsureBossUIStyler();
        if (styler != null)
        {
            styler.Hide();
            Debug.Log("[UI] bossHpSlider가 비활성화되었습니다.");
            return;
        }

        if (bossHpSlider != null)
        {
            bossHpSlider.gameObject.SetActive(false);
            Debug.Log("[UI] bossHpSlider가 비활성화되었습니다.");
        }
        else
        {
            Debug.LogWarning("[UI] HideBossHPBar: bossHpSlider가 null입니다.");
        }

        if (bossNameText != null)
        {
            bossNameText.gameObject.SetActive(false);
        }
    }

    public void ActivePal(bool isActive)
    {
        if (pal == null) return;

        if (isActive)
        {
            PositionPalAtMouse();
            pal.SetActive(true);
            if (playerStatus == null) RefreshPlayerReference();
            if (playerStatus != null)
            {
                playerStatus.currentStance = PlayerStatus.Stance.White;
            }
            SelectPalVisual(0, immediate: true);
        }
        else
        {
            pal.SetActive(false);
        }
    }

    private void PositionPalAtMouse()
    {
        RectTransform palRect = pal.GetComponent<RectTransform>();
        if (palRect == null) return;

        palRect.localScale = Vector3.one * palWheelScale;

        RectTransform parentRect = palRect.parent as RectTransform;
        if (parentRect == null) return;

        Canvas rootCanvas = pal.GetComponentInParent<Canvas>();
        Camera uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, Input.mousePosition, uiCamera, out Vector2 localPoint))
        {
            return;
        }

        // 휠이 화면 밖으로 잘리지 않도록 부모 영역 안에 클램프
        float margin = 16f;
        float halfW = (palRect.rect.width * palWheelScale * 0.5f) + margin;
        float halfH = (palRect.rect.height * palWheelScale * 0.5f) + margin;
        Rect bounds = parentRect.rect;

        if (bounds.width > halfW * 2f)
        {
            localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin + halfW, bounds.xMax - halfW);
        }
        if (bounds.height > halfH * 2f)
        {
            localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + halfH, bounds.yMax - halfH);
        }

        Vector3 localPos = palRect.localPosition;
        palRect.localPosition = new Vector3(localPoint.x, localPoint.y, localPos.z);
    }

    public void ChangeStance(int amount)
    {
        if (playerStatus == null) RefreshPlayerReference();
        if (playerStatus == null) return;

        Debug.Log("체인지");
        switch (amount)
        {
            case 0:
                playerStatus.currentStance = PlayerStatus.Stance.White;
                break;
            case 1:
                playerStatus.currentStance = PlayerStatus.Stance.Red;
                break;
            case 2:
                playerStatus.currentStance = PlayerStatus.Stance.Blue;
                break;
            case 3:
                playerStatus.currentStance = PlayerStatus.Stance.Green;
                break;
        }

        SelectPalVisual(amount, immediate: false);
    }

    private void EnsurePalSliceCache()
    {
        if (pal == null || palSliceImages != null) return;

        Image frameImg = pal.GetComponent<Image>();
        if (frameImg != null)
        {
            frameImg.color = new Color(0.24f, 0.25f, 0.30f, 0.78f);
        }

        string[] sliceNames = { "White", "Red", "Blue", "Green" };
        Color[] topColors =
        {
            new Color(0.97f, 0.98f, 1f, 1f),
            new Color(1f, 0.38f, 0.44f, 1f),
            new Color(0.27f, 0.78f, 1f, 1f),
            new Color(0.32f, 0.92f, 0.62f, 1f)
        };
        Color[] bottomColors =
        {
            new Color(0.75f, 0.80f, 0.88f, 1f),
            new Color(0.66f, 0.10f, 0.20f, 1f),
            new Color(0.11f, 0.31f, 0.78f, 1f),
            new Color(0.08f, 0.52f, 0.35f, 1f)
        };
        Color[] rimColors =
        {
            Color.white,
            new Color(1f, 0.72f, 0.76f, 1f),
            new Color(0.74f, 0.94f, 1f, 1f),
            new Color(0.74f, 1f, 0.88f, 1f)
        };

        Shader sliceShader = Resources.Load<Shader>("Shaders/PaletteSlice");
        if (sliceShader == null)
        {
            sliceShader = Shader.Find("UI/PaletteSlice");
        }

        palSliceImages = new Image[sliceNames.Length];
        palSliceRects = new RectTransform[sliceNames.Length];
        palSliceMaterials = new Material[sliceNames.Length];
        palSliceWeights = new float[sliceNames.Length];

        for (int i = 0; i < sliceNames.Length; i++)
        {
            Transform child = pal.transform.Find(sliceNames[i]);
            if (child == null) continue;

            Image img = child.GetComponent<Image>();
            RectTransform rt = child as RectTransform;
            palSliceImages[i] = img;
            palSliceRects[i] = rt;

            if (img != null && sliceShader != null)
            {
                Material mat = new Material(sliceShader)
                {
                    name = $"PaletteSliceMat_{sliceNames[i]}",
                    hideFlags = HideFlags.HideAndDontSave
                };
                mat.SetColor(PropColorTop, topColors[i]);
                mat.SetColor(PropColorBottom, bottomColors[i]);
                mat.SetColor(PropRimColor, rimColors[i]);
                mat.SetFloat(PropSelectWeight, 0f);
                if (rt != null)
                {
                    Rect r = rt.rect;
                    mat.SetVector(PropSliceBounds, new Vector4(
                        r.xMin,
                        r.yMin,
                        r.width > 0f ? 1f / r.width : 1f,
                        r.height > 0f ? 1f / r.height : 1f));
                }

                img.material = mat;
                palSliceMaterials[i] = mat;
            }
        }
    }

    private void SelectPalVisual(int index, bool immediate)
    {
        EnsurePalSliceCache();
        selectedPalIndex = index;

        if (palSliceRects == null) return;

        // 선택된 바깥쪽 색상이 다른 바깥쪽 조각 위로 올라오게 하되, 중앙 White 원은 항상 최상위 유지
        if (index > 0 && index < palSliceRects.Length && palSliceRects[index] != null)
        {
            palSliceRects[index].SetAsLastSibling();
        }
        if (palSliceRects[0] != null)
        {
            palSliceRects[0].SetAsLastSibling();
        }

        if (immediate)
        {
            ApplyPalVisualStep(1f);
        }
    }

    private void UpdatePalVisuals()
    {
        if (pal == null || !pal.activeInHierarchy || selectedPalIndex < 0) return;
        EnsurePalSliceCache();
        float lerpT = Mathf.Clamp01(Time.unscaledDeltaTime * 18f);
        ApplyPalVisualStep(lerpT);
    }

    private void ApplyPalVisualStep(float lerpT)
    {
        if (palSliceImages == null || palSliceRects == null) return;

        for (int i = 0; i < palSliceImages.Length; i++)
        {
            bool isSelected = i == selectedPalIndex;
            float targetWeight = isSelected ? 1f : 0f;
            Vector3 targetScale = Vector3.one * (isSelected ? PalSelectedScale : PalUnselectedScale);

            if (palSliceWeights != null)
            {
                palSliceWeights[i] = lerpT >= 1f
                    ? targetWeight
                    : Mathf.Lerp(palSliceWeights[i], targetWeight, lerpT);
            }

            bool hasShaderMat = palSliceMaterials != null && palSliceMaterials[i] != null;
            if (hasShaderMat)
            {
                palSliceMaterials[i].SetFloat(PropSelectWeight, palSliceWeights[i]);
                if (palSliceRects[i] != null)
                {
                    Rect r = palSliceRects[i].rect;
                    palSliceMaterials[i].SetVector(PropSliceBounds, new Vector4(
                        r.xMin,
                        r.yMin,
                        r.width > 0f ? 1f / r.width : 1f,
                        r.height > 0f ? 1f / r.height : 1f));
                }
            }

            if (palSliceImages[i] != null)
            {
                Color targetColor = hasShaderMat
                    ? new Color(1f, 1f, 1f, isSelected ? 1f : 0.44f)
                    : (isSelected ? PalSelectedColor : PalUnselectedColor);

                palSliceImages[i].color = lerpT >= 1f
                    ? targetColor
                    : Color.Lerp(palSliceImages[i].color, targetColor, lerpT);
            }

            if (palSliceRects[i] != null)
            {
                palSliceRects[i].localScale = lerpT >= 1f
                    ? targetScale
                    : Vector3.Lerp(palSliceRects[i].localScale, targetScale, lerpT);
            }
        }
    }

    //플레이어 ink 상태 업데이트
    public void UpdateInkBar()
    {
        if (inkSlider != null && playerStatus != null) inkSlider.value = playerStatus.ink / playerStatus.maxInk;
    }

    //플레이어 specialInk 상태 업데이트
    public void UpdateSpedialInkBar()
    {
        if (specialInkSlider != null && playerStatus != null) specialInkSlider.value = playerStatus.specialInk / playerStatus.maxSpecialInk;
    }

    //대화에 필요한 Action
    public void Action(GameObject scanObj)
    {
        isAction = true;
        scanObject = scanObj;
        GameManager objData = scanObject.GetComponent<GameManager>();
        Talk(objData.id, objData.isNPC);
        
        talkPanel.SetActive(isAction);
    }

    void Talk(int id, bool isNPC)
    {
        string talkData = GameManager.instance.GetTalk(id, talkIndex);

        if(talkData == null)
        {
            isAction = false;
            return;
        }

        if (isNPC)
        {
            talkText.text = talkData;
        }
        else
        {
            talkText.text = talkData;
        }

        isAction = true;
        talkIndex++;
    }

    // Tutorial Manager 에서 대화창을 제어하기 위한 함수들
    public void ShowDialogue(string text)
    {
        isAction = true;
        if (talkPanel != null) talkPanel.SetActive(true);
        if (talkText != null) talkText.text = text;
    }

    public void HideDialogue()
    {
        isAction = false;
        if (talkPanel != null) talkPanel.SetActive(false);
    }
    
    
    // 설정창 열기/닫기
    public void OpenSetting()
    {
        SettingsUIStyler.EnsureStyled(settingPanel, null, licensePanel);
        if (settingPanel != null) settingPanel.SetActive(true);
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
    }

    public void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
        if (licensePanel != null) licensePanel.SetActive(false);
        if (settingPanel != null && settingPanel.transform.parent != null)
        {
            Transform guideTransform = settingPanel.transform.parent.Find("GuidePanel");
            if (guideTransform != null) guideTransform.gameObject.SetActive(false);
        }
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
    }

    public void ToggleSetting()
    {
        if (settingPanel == null) return;
        SettingsUIStyler.EnsureStyled(settingPanel, null, licensePanel);
        bool nextState = !settingPanel.activeSelf;
        settingPanel.SetActive(nextState);
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
    }

    // [추가] 라이선스창 열기
    public void OpenLicense()
    {
        SettingsUIStyler.StyleLicensePanel(licensePanel);
        if (licensePanel != null) licensePanel.SetActive(true);
    }

    // [추가] 라이선스창 닫기
    public void CloseLicense()
    {
        if (licensePanel != null) licensePanel.SetActive(false);
    }
}