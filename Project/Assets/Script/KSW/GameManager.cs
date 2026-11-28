using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public int sceneIndex;

    [Header("대화창")]
    public int id;
    public bool isNPC;

    [Header("사운드")]
    public float bgmVolume;
    public float sfxVolume;

    [Header("씬 전환 연출")]
    [SerializeField] private float fadeOutDuration = 0.24f;
    [SerializeField] private float fadeInDuration = 0.28f;
    [SerializeField] private Color transitionColor = new Color(0.04f, 0.03f, 0.05f, 1f);

    //대화 데이터 관리 인스펙터 연결 잊지 말것
    Dictionary<int, string[]> talkData;

    public bool isSetting;
    private bool isLoadingScene;
    public bool IsTransitioning => isLoadingScene;

    private Canvas transitionCanvas;
    private Image transitionCurtain;


    void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        talkData = new Dictionary<int, string[]>();
        GenerateTalkData();
    }

    void GenerateTalkData() // 대화 데이터 생성 함수
    {
        talkData.Add(1000, new string[] { "안녕!" });
    }

    public string GetTalk(int id, int talkIndex) // 대화 데이터 반환 함수
    {
        if(talkIndex == talkData[id].Length)
            return null;
        else
            return talkData[id][talkIndex];
    }
    
    public void LoadScene(string index)
    {
        if (isLoadingScene || string.IsNullOrEmpty(index))
            return;

        SaveManager.OnSceneTransitionRequested(index);
        StartCoroutine(LoadSceneAsyncRoutine(index));
    }

    public void LoadContinueScene(SaveData saveData)
    {
        if (isLoadingScene || saveData == null || string.IsNullOrEmpty(saveData.sceneName))
            return;

        StartCoroutine(LoadContinueSceneRoutine(saveData));
    }

    private IEnumerator LoadContinueSceneRoutine(SaveData saveData)
    {
        isLoadingScene = true;
        EnsureTransitionOverlay();

        Time.timeScale = 1f;
        isSetting = false;

        if (CameraSetting.instance != null)
        {
            CameraSetting.instance.colliders.Clear();
        }

        if (transitionCurtain != null)
        {
            transitionCurtain.raycastTarget = true;
        }

        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.High;

        yield return FadeCurtainRoutine(0f, 1f, fadeOutDuration);

        // Tutorial 씬에만 존재하는 DontDestroyOnLoad 세션 오브젝트(Player, Canvas, UIManager 등)를 먼저 부트스트랩
        if (Player.instance == null && saveData.sceneName != "Tutorial")
        {
            SaveManager.IsBootstrappingSession = true;
            AsyncOperation bootstrapOp = SceneManager.LoadSceneAsync("Tutorial");
            if (bootstrapOp != null)
            {
                while (!bootstrapOp.isDone)
                {
                    yield return null;
                }
            }
            yield return null;
            SaveManager.IsBootstrappingSession = false;
        }

        AsyncOperation targetOp = SceneManager.LoadSceneAsync(saveData.sceneName);
        if (targetOp != null)
        {
            targetOp.allowSceneActivation = false;
            while (targetOp.progress < 0.9f)
            {
                yield return null;
            }

            if (PoolingManager.HasInstance)
            {
                PoolingManager.Instance.ReturnAllActiveObjects();
            }

            targetOp.allowSceneActivation = true;
            while (!targetOp.isDone)
            {
                yield return null;
            }
        }

        float settleElapsed = 0f;
        while (ZoneManager.Instance != null && ZoneManager.Instance.IsInitializingFirstZone && settleElapsed < 5f)
        {
            settleElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return null;

        Application.backgroundLoadingPriority = previousPriority;

        if (transitionCurtain != null)
        {
            transitionCurtain.raycastTarget = false;
        }

        isLoadingScene = false;
        yield return FadeCurtainRoutine(1f, 0f, fadeInDuration);
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName)
    {
        isLoadingScene = true;
        EnsureTransitionOverlay();

        Time.timeScale = 1f;
        isSetting = false;

        if (CameraSetting.instance != null)
        {
            CameraSetting.instance.colliders.Clear();
        }

        if (transitionCurtain != null)
        {
            transitionCurtain.raycastTarget = true;
        }

        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.High;

        // 페이드 아웃과 동시에 다음 씬 백그라운드 비동기 로딩을 미리 시작해 전환 렉 최소화
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            op.allowSceneActivation = false;
        }

        yield return FadeCurtainRoutine(0f, 1f, fadeOutDuration);

        if (op != null)
        {
            while (op.progress < 0.9f)
            {
                yield return null;
            }

            if (PoolingManager.HasInstance)
            {
                PoolingManager.Instance.ReturnAllActiveObjects();
            }

            if (IsNonGameplayScene(sceneName))
            {
                CleanupGameplaySession();
            }

            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                yield return null;
            }
        }
        else if (IsNonGameplayScene(sceneName))
        {
            CleanupGameplaySession();
        }

        // 어두운 화면 뒤에서 ZoneManager의 첫 구역 로드 및 카메라 스냅이 끝날 때까지 짧게 대기
        float settleElapsed = 0f;
        while (ZoneManager.Instance != null && ZoneManager.Instance.IsInitializingFirstZone && settleElapsed < 5f)
        {
            settleElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 첫 프레임 렌더링 안정화 대기
        yield return null;

        Application.backgroundLoadingPriority = previousPriority;

        if (transitionCurtain != null)
        {
            transitionCurtain.raycastTarget = false;
        }

        isLoadingScene = false;
        yield return FadeCurtainRoutine(1f, 0f, fadeInDuration);
    }

    private void EnsureTransitionOverlay()
    {
        if (transitionCurtain != null) return;

        GameObject canvasObj = new GameObject("SceneTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        transitionCanvas = canvasObj.GetComponent<Canvas>();
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject curtainObj = new GameObject("FadeCurtain", typeof(RectTransform), typeof(Image));
        curtainObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rt = curtainObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        transitionCurtain = curtainObj.GetComponent<Image>();
        transitionCurtain.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, 0f);
        transitionCurtain.raycastTarget = false;
    }

    private IEnumerator FadeCurtainRoutine(float fromAlpha, float toAlpha, float duration)
    {
        if (transitionCurtain == null) yield break;

        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
        }

        if (duration <= 0.001f)
        {
            transitionCurtain.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, toAlpha);
            if (toAlpha <= 0f && transitionCanvas != null)
            {
                transitionCanvas.gameObject.SetActive(false);
            }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            float alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            transitionCurtain.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, alpha);
            yield return null;
        }

        transitionCurtain.color = new Color(transitionColor.r, transitionColor.g, transitionColor.b, toAlpha);
        if (toAlpha <= 0f && transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(false);
        }
    }

    public void PauseOnOff()
    {
        isSetting = !isSetting;
        if(!isSetting)
        {
            Time.timeScale = 1f;
            if (AudioManager.instance != null)
            {
                AudioManager.instance.ResumeSFX();
            }
        }
        else if(isSetting)
        {
            Time.timeScale = 0f;
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PauseSFX();
            }
        }
        if(Player.instance != null)
            Player.instance.CloseSetting();
    }

    public static bool IsNonGameplayScene(string sceneName)
    {
        return sceneName == "MainMenu" || sceneName == "GameClear";
    }

    /// <summary>
    /// 타이틀 또는 엔딩 씬으로 전환 시 인게임 DontDestroyOnLoad 오브젝트들을 정리합니다.
    /// </summary>
    public static void CleanupGameplaySession()
    {
        Time.timeScale = 1f;
        if (instance != null)
        {
            instance.isSetting = false;
        }

        if (Player.instance != null)
        {
            Player p = Player.instance;
            DeactivateAndDestroy(p.cursorObject);
            DeactivateAndDestroy(p.groundLine);
            DeactivateAndDestroy(p.attackCursor);
            DeactivateAndDestroy(p.cam);
            DeactivateAndDestroy(p.canvas);
            DeactivateAndDestroy(p.gameObject);
        }

        if (UI.Instance != null)
        {
            DeactivateAndDestroy(UI.Instance.gameObject);
        }

        if (CameraSetting.instance != null)
        {
            DeactivateAndDestroy(CameraSetting.instance.gameObject);
        }

        if (TutorialManager.Instance != null)
        {
            DeactivateAndDestroy(TutorialManager.Instance.gameObject);
        }

        if (ZoneManager.Instance != null)
        {
            ZoneManager.Instance.ClearSessionZones();
        }
    }

    private static void DeactivateAndDestroy(GameObject target)
    {
        if (target == null) return;
        target.SetActive(false);
        Destroy(target);
    }

}
