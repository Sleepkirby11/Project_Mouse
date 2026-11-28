using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages display settings (fullscreen state, preset resolutions, Alt+Enter synchronization,
/// and 16:9 aspect ratio synchronization across cameras, canvases, and full-screen backgrounds).
/// Persists across scenes and syncs both Main Menu and In-Game settings panels.
/// </summary>
public class DisplaySettingsManager : MonoBehaviour
{
    public readonly struct ResolutionOption
    {
        public readonly int Width;
        public readonly int Height;
        public readonly string Label;

        public ResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
            Label = $"{width} x {height}";
        }
    }

    private const float TargetAspect = 16f / 9f;
    private const string PrefFullscreenKey = "Display_Fullscreen";
    private const string PrefWidthKey = "Display_ResWidth";
    private const string PrefHeightKey = "Display_ResHeight";

    private static readonly ResolutionOption[] AllPresets =
    {
        new ResolutionOption(1280, 720),
        new ResolutionOption(1366, 768),
        new ResolutionOption(1600, 900),
        new ResolutionOption(1920, 1080),
        new ResolutionOption(2560, 1440),
        new ResolutionOption(3840, 2160)
    };

    private static DisplaySettingsManager _instance;
    public static DisplaySettingsManager Instance => EnsureInstance();

    public event Action OnDisplaySettingsChanged;

    private readonly List<ResolutionOption> _supportedPresets = new List<ResolutionOption>();
    private int _selectedResolutionIndex = 0;
    private bool _isFullscreen = true;
    private bool _lastObservedScreenFullscreen;
    private bool _pendingGraceSync;
    private float _ignoreExternalScreenChangeUntil;

    private int _lastSyncedWidth = -1;
    private int _lastSyncedHeight = -1;
    private Camera _lastSyncedMainCamera;
    private Camera _letterboxClearCamera;

    private static Sprite _cachedCheckmarkSprite;
    private static Sprite _cachedInsetBoxSprite;

    public int SelectedResolutionIndex => _selectedResolutionIndex;
    public int ResolutionCount => _supportedPresets.Count;
    public ResolutionOption SelectedResolution
    {
        get
        {
            if (_supportedPresets.Count == 0) BuildSupportedPresets();
            return _supportedPresets[Mathf.Clamp(_selectedResolutionIndex, 0, _supportedPresets.Count - 1)];
        }
    }
    public bool IsFullscreen => _isFullscreen;

    public ResolutionOption GetResolutionOption(int index)
    {
        if (_supportedPresets.Count == 0) BuildSupportedPresets();
        return _supportedPresets[Mathf.Clamp(index, 0, _supportedPresets.Count - 1)];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DisplaySettingsManager EnsureInstance()
    {
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<DisplaySettingsManager>();
        if (_instance != null) return _instance;

        GameObject go = new GameObject("DisplaySettingsManager");
        _instance = go.AddComponent<DisplaySettingsManager>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAndApplyInitialSettings();
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
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Start()
    {
        SyncAspectRatio(force: true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncAspectRatio(force: true);
        StartCoroutine(SyncAspectNextFrame());
    }

    private IEnumerator SyncAspectNextFrame()
    {
        yield return null;
        SyncAspectRatio(force: true);
    }

    private void Update()
    {
        // Prevent Alt+Enter from accidentally triggering a currently selected UI Button's Submit event.
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            // In Unity Editor, Screen.fullScreen cannot change via native Alt+Enter,
            // so handle Alt+Enter here to allow testing checkmark synchronization in Editor.
            if (Application.isEditor &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                ToggleFullscreen();
            }
        }

        if (!Application.isEditor)
        {
            MonitorExternalFullscreenChanges();
        }
    }

    private void LateUpdate()
    {
        if (Screen.width != _lastSyncedWidth ||
            Screen.height != _lastSyncedHeight ||
            Camera.main != _lastSyncedMainCamera)
        {
            SyncAspectRatio(force: false);
        }
    }

    private void MonitorExternalFullscreenChanges()
    {
        if (Time.unscaledTime < _ignoreExternalScreenChangeUntil)
        {
            return;
        }

        if (_pendingGraceSync)
        {
            _pendingGraceSync = false;
            _lastObservedScreenFullscreen = Screen.fullScreen;
            if (Screen.fullScreen != _isFullscreen)
            {
                _isFullscreen = Screen.fullScreen;
                SavePrefs();
                SyncAspectRatio(force: true);
                OnDisplaySettingsChanged?.Invoke();
            }
            return;
        }

        if (Screen.fullScreen != _lastObservedScreenFullscreen)
        {
            _lastObservedScreenFullscreen = Screen.fullScreen;
            _isFullscreen = Screen.fullScreen;
            SavePrefs();

            // When Alt+Enter switches from Fullscreen to Windowed, ensure the window uses the selected resolution.
            if (!_isFullscreen)
            {
                ResolutionOption opt = SelectedResolution;
                if (Screen.width != opt.Width || Screen.height != opt.Height)
                {
                    Screen.SetResolution(opt.Width, opt.Height, FullScreenMode.Windowed);
                    _ignoreExternalScreenChangeUntil = Time.unscaledTime + 0.35f;
                    _pendingGraceSync = true;
                }
            }

            SyncAspectRatio(force: true);
            OnDisplaySettingsChanged?.Invoke();
        }
    }

    private void LoadAndApplyInitialSettings()
    {
        BuildSupportedPresets();

        if (PlayerPrefs.HasKey(PrefWidthKey) && PlayerPrefs.HasKey(PrefHeightKey))
        {
            int savedW = PlayerPrefs.GetInt(PrefWidthKey, 1920);
            int savedH = PlayerPrefs.GetInt(PrefHeightKey, 1080);
            _selectedResolutionIndex = FindClosestPresetIndex(savedW, savedH);
        }
        else
        {
            _selectedResolutionIndex = DetermineDefaultPresetIndex();
        }

        if (PlayerPrefs.HasKey(PrefFullscreenKey))
        {
            _isFullscreen = PlayerPrefs.GetInt(PrefFullscreenKey, 1) != 0;
        }
        else
        {
            _isFullscreen = Application.isEditor ? true : Screen.fullScreen;
        }

        _lastObservedScreenFullscreen = Screen.fullScreen;
        ApplyDisplaySettings(savePrefs: true);
    }

    private void BuildSupportedPresets()
    {
        _supportedPresets.Clear();

        GetMaxMonitorResolution(out int maxW, out int maxH);
        bool include1366 = maxW >= 1366 && maxW < 1600 && maxH >= 768;

        for (int i = 0; i < AllPresets.Length; i++)
        {
            ResolutionOption opt = AllPresets[i];
            if (opt.Width == 1366 && opt.Height == 768 && !include1366)
            {
                continue;
            }
            if (opt.Width <= maxW && opt.Height <= maxH)
            {
                _supportedPresets.Add(opt);
            }
        }

        if (_supportedPresets.Count == 0)
        {
            _supportedPresets.Add(new ResolutionOption(Mathf.Max(640, maxW), Mathf.Max(360, maxH)));
        }
    }

    private static void GetMaxMonitorResolution(out int maxW, out int maxH)
    {
        maxW = Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width;
        maxH = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;

        Resolution[] hwResolutions = Screen.resolutions;
        if (hwResolutions != null)
        {
            for (int i = 0; i < hwResolutions.Length; i++)
            {
                if (hwResolutions[i].width > maxW) maxW = hwResolutions[i].width;
                if (hwResolutions[i].height > maxH) maxH = hwResolutions[i].height;
            }
        }

        if (maxW <= 0 || maxH <= 0)
        {
            maxW = 1920;
            maxH = 1080;
        }
    }

    private int DetermineDefaultPresetIndex()
    {
        for (int i = 0; i < _supportedPresets.Count; i++)
        {
            if (_supportedPresets[i].Width == 1920 && _supportedPresets[i].Height == 1080)
            {
                return i;
            }
        }
        return Mathf.Max(0, _supportedPresets.Count - 1);
    }

    private int FindClosestPresetIndex(int width, int height)
    {
        for (int i = 0; i < _supportedPresets.Count; i++)
        {
            if (_supportedPresets[i].Width == width && _supportedPresets[i].Height == height)
            {
                return i;
            }
        }

        int bestIdx = Mathf.Max(0, _supportedPresets.Count - 1);
        long bestDiff = long.MaxValue;
        for (int i = 0; i < _supportedPresets.Count; i++)
        {
            long dx = _supportedPresets[i].Width - width;
            long dy = _supportedPresets[i].Height - height;
            long diff = dx * dx + dy * dy;
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (_isFullscreen == fullscreen) return;
        _isFullscreen = fullscreen;
        ApplyDisplaySettings(savePrefs: true);
    }

    public void ToggleFullscreen()
    {
        SetFullscreen(!_isFullscreen);
    }

    public void StepResolution(int delta)
    {
        if (_supportedPresets.Count == 0) BuildSupportedPresets();
        int count = _supportedPresets.Count;
        int next = (_selectedResolutionIndex + delta) % count;
        if (next < 0) next += count;
        SetResolutionIndex(next);
    }

    public void SetResolutionIndex(int index)
    {
        if (_supportedPresets.Count == 0) BuildSupportedPresets();
        int clamped = Mathf.Clamp(index, 0, _supportedPresets.Count - 1);
        if (_selectedResolutionIndex == clamped) return;
        _selectedResolutionIndex = clamped;
        ApplyDisplaySettings(savePrefs: true);
    }

    public void ApplyDisplaySettings(bool savePrefs = true)
    {
        ResolutionOption opt = SelectedResolution;
        FullScreenMode mode = _isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        Screen.SetResolution(opt.Width, opt.Height, mode);

        _ignoreExternalScreenChangeUntil = Time.unscaledTime + 0.35f;
        _pendingGraceSync = true;

        if (savePrefs)
        {
            SavePrefs();
        }

        SyncAspectRatio(force: true);
        OnDisplaySettingsChanged?.Invoke();
    }

    private void SavePrefs()
    {
        ResolutionOption opt = SelectedResolution;
        PlayerPrefs.SetInt(PrefFullscreenKey, _isFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(PrefWidthKey, opt.Width);
        PlayerPrefs.SetInt(PrefHeightKey, opt.Height);
        PlayerPrefs.Save();
    }

    public void SyncAspectRatio(bool force)
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        float windowAspect = (float)width / height;

        _lastSyncedWidth = width;
        _lastSyncedHeight = height;
        _lastSyncedMainCamera = Camera.main;

        UpdateCameraViewports(windowAspect);
        UpdateCanvasScalers(windowAspect);
        UpdateFullScreenArtBackgrounds();

        if (force && CameraSetting.instance != null)
        {
            CameraSetting.instance.InvalidateConfinerCache();
        }
    }

    private void UpdateCameraViewports(float windowAspect)
    {
        bool needsBars = Mathf.Abs(windowAspect - TargetAspect) >= 0.01f;
        Rect targetRect;

        if (!needsBars)
        {
            targetRect = new Rect(0f, 0f, 1f, 1f);
        }
        else if (windowAspect > TargetAspect)
        {
            float scaleWidth = TargetAspect / windowAspect;
            targetRect = new Rect((1f - scaleWidth) * 0.5f, 0f, scaleWidth, 1f);
        }
        else
        {
            float scaleHeight = windowAspect / TargetAspect;
            targetRect = new Rect(0f, (1f - scaleHeight) * 0.5f, 1f, scaleHeight);
        }

        if (needsBars)
        {
            EnsureLetterboxClearCamera();
            if (_letterboxClearCamera != null)
            {
                _letterboxClearCamera.enabled = true;
            }
        }
        else if (_letterboxClearCamera != null)
        {
            _letterboxClearCamera.enabled = false;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null || cam == _letterboxClearCamera || cam.targetTexture != null)
            {
                continue;
            }

            if (cam.rect != targetRect)
            {
                cam.rect = targetRect;
            }
        }
    }

    private void EnsureLetterboxClearCamera()
    {
        if (_letterboxClearCamera != null) return;

        GameObject camObj = new GameObject("LetterboxClearCamera");
        camObj.transform.SetParent(transform, false);
        _letterboxClearCamera = camObj.AddComponent<Camera>();
        _letterboxClearCamera.depth = -100f;
        _letterboxClearCamera.clearFlags = CameraClearFlags.SolidColor;
        _letterboxClearCamera.backgroundColor = Color.black;
        _letterboxClearCamera.cullingMask = 0;
        _letterboxClearCamera.orthographic = true;
        _letterboxClearCamera.useOcclusionCulling = false;
        _letterboxClearCamera.allowHDR = false;
        _letterboxClearCamera.allowMSAA = false;
    }

    private static void UpdateCanvasScalers(float windowAspect)
    {
        float targetMatch = (windowAspect >= TargetAspect) ? 1f : 0f;
        CanvasScaler[] scalers = Resources.FindObjectsOfTypeAll<CanvasScaler>();
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null || !scaler.gameObject.scene.IsValid()) continue;
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (scaler.referenceResolution.x < 1900f || scaler.referenceResolution.y < 1000f) continue;

            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
            {
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            }
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, targetMatch))
            {
                scaler.matchWidthOrHeight = targetMatch;
            }
        }
    }

    private static void UpdateFullScreenArtBackgrounds()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName != "MainMenu" && sceneName != "GameClear") return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.isRootCanvas) continue;

            Transform bgTf = canvas.transform.Find("Background");
            if (bgTf == null) continue;

            Image bgImg = bgTf.GetComponent<Image>();
            if (bgImg == null || bgImg.sprite == null) continue;

            EnsureBlackBacking(canvas.transform, bgTf);

            AspectRatioFitter fitter = bgTf.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = bgTf.gameObject.AddComponent<AspectRatioFitter>();
            }
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = TargetAspect;
            bgImg.preserveAspect = true;
        }
    }

    private static void EnsureBlackBacking(Transform canvasTf, Transform bgTf)
    {
        Transform existing = canvasTf.Find("AspectLetterboxBacking");
        GameObject backingGo;
        if (existing == null)
        {
            backingGo = new GameObject("AspectLetterboxBacking", typeof(RectTransform), typeof(Image));
            backingGo.layer = canvasTf.gameObject.layer;
            backingGo.transform.SetParent(canvasTf, false);
        }
        else
        {
            backingGo = existing.gameObject;
        }

        backingGo.transform.SetSiblingIndex(Mathf.Max(0, bgTf.GetSiblingIndex()));

        RectTransform rt = backingGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = backingGo.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    /// <summary>
    /// Creates or returns a crisp outlined pixel-art checkmark sprite matching Interface.png.
    /// </summary>
    public static Sprite GetCheckmarkSprite()
    {
        if (_cachedCheckmarkSprite != null) return _cachedCheckmarkSprite;

        const int width = 25;
        const int height = 22;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "PixelCheckmarkTex",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color outline = new Color32(49, 25, 23, 255);
        Color fill = new Color32(94, 196, 84, 255);
        Color shade = new Color32(58, 142, 54, 255);

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // Pattern matching Interface.png checkmark (top-to-bottom)
        string[] rows =
        {
            ".........................",
            "...................#.....",
            "..................#+#....",
            ".................#+++#...",
            "................#+++++#..",
            "...............#+++++o#..",
            "......#.......#+++++o#...",
            ".....#+#.....#+++++o#....",
            "....#+++#...#+++++o#.....",
            "...#+++++#.#+++++o#......",
            "..##o+++++#+++++o#.......",
            "..###o+++++++++o#........",
            "...###o+++++++o#.........",
            "....###o+++++o#..........",
            ".....###o+++o#...........",
            "......###o+o#............",
            ".......###o#.............",
            "........###..............",
            ".........#...............",
            ".........................",
            ".........................",
            "........................."
        };

        for (int r = 0; r < height; r++)
        {
            int y = height - 1 - r;
            string row = rows[r];
            for (int x = 0; x < width; x++)
            {
                char ch = row[x];
                if (ch == '#') pixels[y * width + x] = outline;
                else if (ch == '+') pixels[y * width + x] = fill;
                else if (ch == 'o') pixels[y * width + x] = shade;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _cachedCheckmarkSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f);
        _cachedCheckmarkSprite.name = "PixelCheckmarkSprite";
        return _cachedCheckmarkSprite;
    }

    /// <summary>
    /// Creates or returns a 9-sliced pixel-art inset box sprite with a solid 3px dark border
    /// so borders remain crisp and never clip or drop on any side at non-native resolutions (e.g. 1600x900).
    /// </summary>
    public static Sprite GetInsetBoxSprite()
    {
        if (_cachedInsetBoxSprite != null) return _cachedInsetBoxSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "PixelInsetBoxTex",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color border = new Color32(49, 25, 23, 255);
        Color shadow = new Color32(180, 134, 88, 255);
        Color highlight = new Color32(250, 230, 196, 255);
        Color fill = new Color32(244, 222, 188, 255);

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isOuterCorner =
                    (x == 0 || x == size - 1) &&
                    (y == 0 || y == size - 1);

                if (isOuterCorner)
                {
                    pixels[y * size + x] = clear;
                    continue;
                }

                bool isBorder = x < 3 || x >= size - 3 || y < 3 || y >= size - 3;
                if (isBorder)
                {
                    pixels[y * size + x] = border;
                    continue;
                }

                // Subtle 1px inner bevel (top/left recessed shadow, bottom/right highlight)
                if (y == size - 4 || x == 3)
                {
                    pixels[y * size + x] = shadow;
                }
                else if (y == 3 || x == size - 4)
                {
                    pixels[y * size + x] = highlight;
                }
                else
                {
                    pixels[y * size + x] = fill;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _cachedInsetBoxSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(8f, 8f, 8f, 8f));
        _cachedInsetBoxSprite.name = "PixelInsetBoxSprite";
        return _cachedInsetBoxSprite;
    }
}
