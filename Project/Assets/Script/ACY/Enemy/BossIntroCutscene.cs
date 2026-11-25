using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 보스 조우 시 대사 없이 진행되는 시네마틱 컷씬(레터박스 + 카메라 팬/줌 + 보스명 배너 + 스킵 버튼)을 제어합니다.
/// </summary>
public class BossIntroCutscene : MonoBehaviour
{
    private static BossIntroCutscene _instance;
    public static BossIntroCutscene Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("BossIntroCutscene");
                _instance = go.AddComponent<BossIntroCutscene>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("타이밍 설정")]
    [SerializeField] private float panToBossDuration = 0.6f;
    [SerializeField] private float holdOnBossDuration = 1.1f;
    [SerializeField] private float panToPlayerDuration = 0.6f;

    [Header("카메라 설정")]
    [SerializeField] private float bossZoomOrthoSize = 10.5f;
    [SerializeField] private float bossFocusYOffset = 0.8f;

    public static bool IsPlaying => _instance != null && _instance.isPlaying;

    private bool isPlaying = false;
    private bool skipRequested = false;

    // 런타임 생성 UI
    private Canvas overlayCanvas;
    private RectTransform topBar;
    private RectTransform bottomBar;
    private CanvasGroup bannerGroup;
    private RectTransform bannerRect;
    private Text bossTitleText;
    private Image topAccentLine;
    private Image bottomAccentLine;
    private GameObject skipButtonObj;
    private Button skipButton;
    private Text skipButtonText;

    private const float BarHeight = 130f;

    public static void Play(EnemyStatus boss, GameObject[] enemies)
    {
        Instance.StartCutscene(boss, enemies);
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isPlaying)
        {
            skipRequested = true;
        }
    }

    private void StartCutscene(EnemyStatus boss, GameObject[] enemies)
    {
        if (isPlaying)
            return;

        StartCoroutine(CutsceneRoutine(boss, enemies));
    }

    public void RequestSkip()
    {
        if (isPlaying)
        {
            skipRequested = true;
        }
    }

    private IEnumerator CutsceneRoutine(EnemyStatus boss, GameObject[] enemies)
    {
        isPlaying = true;
        skipRequested = false;

        EnsureUI();
        ApplyBossTheme(boss);

        // 1. 보스 AI 일시 정지 후 오브젝트 활성화 (Idle + BGM 전환만 먼저 실행)
        List<MonoBehaviour> pausedScripts = new List<MonoBehaviour>();
        PauseEnemyScripts(enemies, pausedScripts);

        if (boss != null)
        {
            boss.SuppressAutoBossBarOnStart = true;
        }

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null)
                {
                    enemies[i].SetActive(true);
                }
            }
        }

        // SetActive(true) 직후 재활성화된 스크립트나 이미 실행 중이던 코루틴/물리 속도도 차단
        PauseEnemyScripts(enemies, pausedScripts);
        FreezeEnemyBodies(enemies);
        PrepareBossVisualStateForCutscene(enemies, Player.instance != null ? Player.instance.transform : null);

        // Awake 이후 결정된 보스 이름/색상 다시 반영
        ApplyBossTheme(boss);

        // 2. 플레이어 입력 잠금 및 무적 보호
        Player player = Player.instance;
        PlayerInput playerInput = null;
        bool prevInputEnabled = true;

        if (player != null)
        {
            playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                prevInputEnabled = playerInput.enabled;
                playerInput.enabled = false;
            }

            player.inputVec = Vector2.zero;
            player.isCanMove = false;
            player.isDashReady = false;
            player.DashLine();

            if (player.cursor != null) player.cursor.isMove = false;
            if (player.groundCursor != null) player.groundCursor.isMove = false;
            if (player.anim != null) player.anim.SetBool("IsWalk", false);
            if (player.rigid != null) player.rigid.linearVelocityX = 0f;
            if (player.status != null) player.status.SetInvincible(true);
        }

        if (UI.Instance != null)
        {
            UI.Instance.ActivePal(false);
        }

        // 3. 시네머신 카메라 준비
        CinemachineCamera vcam = CameraSetting.instance != null
            ? CameraSetting.instance.GetComponent<CinemachineCamera>()
            : FindFirstObjectByType<CinemachineCamera>();

        CinemachinePositionComposer composer = vcam != null ? vcam.GetComponent<CinemachinePositionComposer>() : null;
        Transform prevTarget = vcam != null ? vcam.Target.TrackingTarget : null;
        float prevOrthoSize = vcam != null ? vcam.Lens.OrthographicSize : 15f;
        Vector3 prevDamping = composer != null ? composer.Damping : Vector3.one;

        GameObject camTargetObj = null;
        if (vcam != null)
        {
            camTargetObj = new GameObject("BossCutsceneCamTarget");
            Vector3 startCamPos = prevTarget != null
                ? prevTarget.position
                : (player != null ? player.transform.position : transform.position);
            camTargetObj.transform.position = startCamPos;

            vcam.Target.TrackingTarget = camTargetObj.transform;
            if (composer != null)
            {
                composer.Damping = Vector3.zero;
            }
        }

        // 4. UI 표시 초기화
        overlayCanvas.gameObject.SetActive(true);
        SetLetterboxProgress(0f);
        SetBannerProgress(0f);
        if (skipButtonObj != null) skipButtonObj.SetActive(true);

        Vector3 originPos = camTargetObj != null ? camTargetObj.transform.position : Vector3.zero;
        Vector3 bossFocusPos = boss != null
            ? boss.transform.position + new Vector3(0f, bossFocusYOffset, 0f)
            : originPos;
        float targetZoom = Mathf.Min(prevOrthoSize, bossZoomOrthoSize);

        bool useCustomTiming = boss != null && boss.OverrideCutsceneTiming;
        float panToBoss = Mathf.Max(0f, useCustomTiming ? boss.CutscenePanToBossDuration : panToBossDuration);
        float holdOnBoss = Mathf.Max(0f, useCustomTiming ? boss.CutsceneHoldOnBossDuration : holdOnBossDuration);
        float panToPlayer = Mathf.Max(0f, useCustomTiming ? boss.CutscenePanToPlayerDuration : panToPlayerDuration);

        // Phase 1: 보스로 카메라 팬 + 줌인 + 레터박스 인
        float elapsed = 0f;
        if (panToBoss > 0f)
        {
            while (elapsed < panToBoss && !skipRequested)
            {
                CheckKeyboardSkip(elapsed);
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / panToBoss);
                float ease = EaseInOutCubic(t);

                SetLetterboxProgress(EaseOutCubic(t));

                if (camTargetObj != null)
                {
                    camTargetObj.transform.position = Vector3.Lerp(originPos, bossFocusPos, ease);
                }

                if (vcam != null)
                {
                    var lens = vcam.Lens;
                    lens.OrthographicSize = Mathf.Lerp(prevOrthoSize, targetZoom, ease);
                    vcam.Lens = lens;
                }

                yield return null;
            }
        }
        else if (!skipRequested)
        {
            if (camTargetObj != null) camTargetObj.transform.position = bossFocusPos;
            if (vcam != null)
            {
                var lens = vcam.Lens;
                lens.OrthographicSize = targetZoom;
                vcam.Lens = lens;
            }
        }

        // Phase 2: 보스 포커스 + 임팩트 쉐이크 + 보스 타이틀 배너
        if (!skipRequested)
        {
            SetLetterboxProgress(1f);

            if (CameraShake.instance != null)
            {
                CameraShake.instance.Impulse();
            }

            elapsed = 0f;
            float holdZoomEnd = Mathf.Max(4f, targetZoom - 0.5f);
            if (holdOnBoss > 0f)
            {
                while (elapsed < holdOnBoss && !skipRequested)
                {
                    CheckKeyboardSkip(elapsed + panToBoss);
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / holdOnBoss);

                    if (boss != null && camTargetObj != null)
                    {
                        camTargetObj.transform.position = boss.transform.position + new Vector3(0f, bossFocusYOffset, 0f);
                    }

                    if (vcam != null)
                    {
                        var lens = vcam.Lens;
                        lens.OrthographicSize = Mathf.Lerp(targetZoom, holdZoomEnd, t);
                        vcam.Lens = lens;
                    }

                    // 배너 페이드 인 (0 ~ 0.22s) 및 유지
                    float bannerAlpha = Mathf.Clamp01(elapsed / 0.22f);
                    SetBannerProgress(EaseOutCubic(bannerAlpha));

                    yield return null;
                }
            }
        }

        // Phase 3: 배너 & 레터박스 아웃 + 플레이어로 복귀
        if (!skipRequested && panToPlayer > 0f)
        {
            Vector3 returnStartPos = camTargetObj != null ? camTargetObj.transform.position : bossFocusPos;
            float returnStartZoom = vcam != null ? vcam.Lens.OrthographicSize : targetZoom;

            elapsed = 0f;
            while (elapsed < panToPlayer && !skipRequested)
            {
                CheckKeyboardSkip(elapsed + panToBoss + holdOnBoss);
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / panToPlayer);
                float ease = EaseInOutCubic(t);

                SetLetterboxProgress(1f - EaseInOutCubic(t));
                SetBannerProgress(1f - Mathf.Clamp01(t * 2.5f));

                Vector3 playerPos = player != null ? player.transform.position : originPos;
                if (camTargetObj != null)
                {
                    camTargetObj.transform.position = Vector3.Lerp(returnStartPos, playerPos, ease);
                }

                if (vcam != null)
                {
                    var lens = vcam.Lens;
                    lens.OrthographicSize = Mathf.Lerp(returnStartZoom, prevOrthoSize, ease);
                    vcam.Lens = lens;
                }

                yield return null;
            }
        }

        // 5. 종료 및 스킵 공통 정리
        if (vcam != null)
        {
            vcam.Target.TrackingTarget = prevTarget != null ? prevTarget : (player != null ? player.transform : null);
            var lens = vcam.Lens;
            lens.OrthographicSize = prevOrthoSize;
            vcam.Lens = lens;
        }

        if (composer != null)
        {
            composer.Damping = prevDamping;
        }

        if (camTargetObj != null)
        {
            Destroy(camTargetObj);
        }

        if (overlayCanvas != null)
        {
            overlayCanvas.gameObject.SetActive(false);
        }

        // Start/OnEnable/Update가 IsPlaying == false를 보도록 먼저 플래그 해제
        isPlaying = false;
        skipRequested = false;

        // 보스 AI 재개 및 체력바 표시
        for (int i = 0; i < pausedScripts.Count; i++)
        {
            if (pausedScripts[i] != null)
            {
                pausedScripts[i].enabled = true;
            }
        }

        if (boss != null)
        {
            boss.SuppressAutoBossBarOnStart = false;
            if (UI.Instance != null)
            {
                UI.Instance.ShowBossHPBar(boss.BossName, boss.BossNameColor, boss.GetHPRatio());
            }
        }

        // 플레이어 조작 복구
        if (player != null)
        {
            if (playerInput != null)
            {
                playerInput.enabled = prevInputEnabled;
            }
            player.isCanMove = true;
            if (player.status != null)
            {
                player.status.SetInvincible(false);
            }
        }
    }

    private static bool ShouldKeepEnabledDuringCutscene(MonoBehaviour mb)
    {
        return mb is EnemyStatus
            || mb is BGMChanger
            || mb is BossDeathEvent
            || mb is AnimationEvent
            || mb is RgbBossAnimationEvents
            || mb is UnityEngine.EventSystems.UIBehaviour;
    }

    private static void PauseEnemyScripts(GameObject[] enemies, List<MonoBehaviour> pausedScripts)
    {
        if (enemies == null) return;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            MonoBehaviour[] scripts = enemy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < scripts.Length; j++)
            {
                MonoBehaviour mb = scripts[j];
                if (mb == null || !mb.enabled) continue;
                if (ShouldKeepEnabledDuringCutscene(mb)) continue;

                mb.StopAllCoroutines();
                mb.enabled = false;
                if (!pausedScripts.Contains(mb))
                {
                    pausedScripts.Add(mb);
                }
            }
        }
    }

    private static void FreezeEnemyBodies(GameObject[] enemies)
    {
        if (enemies == null) return;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            Rigidbody2D[] bodies = enemy.GetComponentsInChildren<Rigidbody2D>(true);
            for (int j = 0; j < bodies.Length; j++)
            {
                if (bodies[j] != null)
                {
                    bodies[j].linearVelocity = Vector2.zero;
                }
            }
        }
    }

    private static void PrepareBossVisualStateForCutscene(GameObject[] enemies, Transform playerTransform)
    {
        if (enemies == null) return;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            // 보스가 플레이어(왼쪽)를 바라보도록 방향 동기화
            GreenBossMove greenMove = enemy.GetComponent<GreenBossMove>();
            if (greenMove != null) greenMove.FacePlayer(playerTransform);

            RedBossMove redMove = enemy.GetComponent<RedBossMove>();
            if (redMove != null) redMove.FacePlayer(playerTransform);

            BlueBossFlip blueFlip = enemy.GetComponent<BlueBossFlip>();
            if (blueFlip != null) blueFlip.FacePlayer(playerTransform);

            RgbBossMove rgbMove = enemy.GetComponent<RgbBossMove>();
            if (rgbMove != null) rgbMove.FacePlayer(playerTransform);

            // 레드 보스 캐스팅 슬라이더 등 전투 UI 비표시
            RedBossAttack redAttack = enemy.GetComponent<RedBossAttack>();
            if (redAttack != null) redAttack.HideCastUI();

            // 이클립스(RGB Boss) 컷씬 중 보라색(마젠타) Idle 유지 + 컷씬 종료 후 변환 연출 예약
            RgbColorCycle rgbColorCycle = enemy.GetComponent<RgbColorCycle>();
            if (rgbColorCycle != null) rgbColorCycle.ShowCutsceneMagentaIdle();

            // 애니메이터에 기본 켜져 있는 트리거/공격 상태 정리
            Animator[] animators = enemy.GetComponentsInChildren<Animator>(true);
            for (int j = 0; j < animators.Length; j++)
            {
                Animator anim = animators[j];
                if (anim == null || anim.runtimeAnimatorController == null) continue;

                AnimatorControllerParameter[] parameters = anim.parameters;
                for (int p = 0; p < parameters.Length; p++)
                {
                    if (parameters[p].type == AnimatorControllerParameterType.Trigger)
                    {
                        anim.ResetTrigger(parameters[p].nameHash);
                    }
                }
            }
        }
    }

    private void CheckKeyboardSkip(float totalElapsed)
    {
        if (totalElapsed < 0.12f)
            return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            skipRequested = true;
        }
    }

    private void ApplyBossTheme(EnemyStatus boss)
    {
        if (bossTitleText == null) return;

        string title = boss != null && !string.IsNullOrEmpty(boss.BossName) ? boss.BossName : "BOSS";
        Color themeColor = boss != null && boss.BossNameColor != Color.clear ? boss.BossNameColor : Color.white;

        // 어두운 배너 위에서 시인성 확보
        Color brightTextColor = Color.Lerp(themeColor, Color.white, 0.25f);
        bossTitleText.text = title;
        bossTitleText.color = brightTextColor;

        if (topAccentLine != null) topAccentLine.color = themeColor;
        if (bottomAccentLine != null) bottomAccentLine.color = themeColor;
    }

    private void SetLetterboxProgress(float t)
    {
        float yOffset = Mathf.Lerp(BarHeight, 0f, t);
        if (topBar != null)
        {
            topBar.anchoredPosition = new Vector2(0f, yOffset);
        }
        if (bottomBar != null)
        {
            bottomBar.anchoredPosition = new Vector2(0f, -yOffset);
        }
    }

    private void SetBannerProgress(float t)
    {
        if (bannerGroup != null)
        {
            bannerGroup.alpha = t;
        }
        if (bannerRect != null)
        {
            float scaleX = Mathf.Lerp(0.86f, 1f, t);
            bannerRect.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    private void EnsureUI()
    {
        if (overlayCanvas != null)
            return;

        Font uiFont = UI.Instance != null ? UI.Instance.GetUIFont() : null;
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        GameObject canvasObj = new GameObject("CutsceneCanvas");
        canvasObj.transform.SetParent(transform, false);

        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 4000;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 상단 레터박스
        topBar = CreateRectImage("TopLetterbox", canvasObj.transform, Color.black, false);
        topBar.anchorMin = new Vector2(0f, 1f);
        topBar.anchorMax = new Vector2(1f, 1f);
        topBar.pivot = new Vector2(0.5f, 1f);
        topBar.sizeDelta = new Vector2(0f, BarHeight);

        // 하단 레터박스
        bottomBar = CreateRectImage("BottomLetterbox", canvasObj.transform, Color.black, false);
        bottomBar.anchorMin = new Vector2(0f, 0f);
        bottomBar.anchorMax = new Vector2(1f, 0f);
        bottomBar.pivot = new Vector2(0.5f, 0f);
        bottomBar.sizeDelta = new Vector2(0f, BarHeight);

        // 보스 이름 배너 (대사 없이 타이틀만)
        GameObject bannerObj = new GameObject("BossNameBanner");
        bannerObj.transform.SetParent(canvasObj.transform, false);
        bannerRect = bannerObj.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 0f);
        bannerRect.anchorMax = new Vector2(0.5f, 0f);
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.anchoredPosition = new Vector2(0f, 225f);
        bannerRect.sizeDelta = new Vector2(780f, 114f);

        Image bannerBg = bannerObj.AddComponent<Image>();
        bannerBg.color = new Color(0.05f, 0.05f, 0.07f, 0.78f);
        bannerBg.raycastTarget = false;

        bannerGroup = bannerObj.AddComponent<CanvasGroup>();
        bannerGroup.interactable = false;
        bannerGroup.blocksRaycasts = false;

        topAccentLine = CreateRectImage("TopAccent", bannerObj.transform, Color.white, false).GetComponent<Image>();
        RectTransform topLineRect = topAccentLine.rectTransform;
        topLineRect.anchorMin = new Vector2(0f, 1f);
        topLineRect.anchorMax = new Vector2(1f, 1f);
        topLineRect.pivot = new Vector2(0.5f, 1f);
        topLineRect.sizeDelta = new Vector2(0f, 4f);

        bottomAccentLine = CreateRectImage("BottomAccent", bannerObj.transform, Color.white, false).GetComponent<Image>();
        RectTransform bottomLineRect = bottomAccentLine.rectTransform;
        bottomLineRect.anchorMin = new Vector2(0f, 0f);
        bottomLineRect.anchorMax = new Vector2(1f, 0f);
        bottomLineRect.pivot = new Vector2(0.5f, 0f);
        bottomLineRect.sizeDelta = new Vector2(0f, 4f);

        GameObject titleObj = new GameObject("BossTitleText");
        titleObj.transform.SetParent(bannerObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;

        bossTitleText = titleObj.AddComponent<Text>();
        bossTitleText.font = uiFont;
        bossTitleText.fontSize = 62;
        bossTitleText.alignment = TextAnchor.MiddleCenter;
        bossTitleText.raycastTarget = false;

        Outline outline = titleObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        // SKIP 버튼 (하단 우측 레터박스 위)
        skipButtonObj = new GameObject("SkipButton");
        skipButtonObj.transform.SetParent(canvasObj.transform, false);
        RectTransform skipRect = skipButtonObj.AddComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0.5f);
        skipRect.anchoredPosition = new Vector2(-48f, BarHeight * 0.5f);
        skipRect.sizeDelta = new Vector2(210f, 54f);

        Image skipBg = skipButtonObj.AddComponent<Image>();
        skipBg.color = new Color(0.12f, 0.12f, 0.15f, 0.88f);

        Outline skipBorder = skipButtonObj.AddComponent<Outline>();
        skipBorder.effectColor = new Color(1f, 1f, 1f, 0.5f);
        skipBorder.effectDistance = new Vector2(2f, -2f);

        skipButton = skipButtonObj.AddComponent<Button>();
        ColorBlock colors = skipButton.colors;
        colors.normalColor = new Color(0.12f, 0.12f, 0.15f, 0.88f);
        colors.highlightedColor = new Color(0.28f, 0.28f, 0.34f, 0.96f);
        colors.pressedColor = new Color(0.42f, 0.42f, 0.5f, 1f);
        skipButton.colors = colors;
        skipButton.onClick.AddListener(RequestSkip);

        GameObject skipTextObj = new GameObject("Label");
        skipTextObj.transform.SetParent(skipButtonObj.transform, false);
        RectTransform skipTextRect = skipTextObj.AddComponent<RectTransform>();
        skipTextRect.anchorMin = Vector2.zero;
        skipTextRect.anchorMax = Vector2.one;
        skipTextRect.sizeDelta = Vector2.zero;

        skipButtonText = skipTextObj.AddComponent<Text>();
        skipButtonText.font = uiFont;
        skipButtonText.text = "SKIP >>";
        skipButtonText.fontSize = 30;
        skipButtonText.color = Color.white;
        skipButtonText.alignment = TextAnchor.MiddleCenter;
        skipButtonText.raycastTarget = false;

        canvasObj.SetActive(false);
    }

    private static RectTransform CreateRectImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return rt;
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
}
