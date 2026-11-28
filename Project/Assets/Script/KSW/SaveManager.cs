using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SaveData
{
    public int version = 1;
    public string sceneName;
    public int zoneId;
    public float posX;
    public float posY;
    public float posZ;
    public bool facingRight = true;

    public int hp;
    public float ink;
    public float specialInk;
    public float currentCoolTime;
    public int currentStance;
    public int combo;

    public List<string> completedTutorialSteps = new List<string>();
    public List<ZoneSaveData> zones = new List<ZoneSaveData>();

    public bool isInBossFight;
    public int bossTriggerZoneId = -1;
    public string bossTriggerEntityId;
    public float bossRestartPosX;
    public float bossRestartPosY;
    public float bossRestartPosZ;
}

[Serializable]
public class ZoneSaveData
{
    public int zoneId;
    public bool bossDefeated;
    public List<EnemySaveEntry> enemies = new List<EnemySaveEntry>();
    public List<TriggerSaveEntry> triggers = new List<TriggerSaveEntry>();
    public List<WallSaveEntry> walls = new List<WallSaveEntry>();
    public List<PlateSaveEntry> plates = new List<PlateSaveEntry>();
    public List<LeverSaveEntry> levers = new List<LeverSaveEntry>();
    public List<DestroyedEntityEntry> destroyedEntities = new List<DestroyedEntityEntry>();
}

[Serializable]
public class EnemySaveEntry
{
    public string id;
    public int currentHP;
    public bool isDead;
    public bool isActive;
    public bool hasPosition;
    public float posX;
    public float posY;
    public float posZ;
}

[Serializable]
public class TriggerSaveEntry
{
    public string id;
    public bool activated;
}

[Serializable]
public class WallSaveEntry
{
    public string id;
    public bool isCollapsed;
    public bool isActive;
}

[Serializable]
public class PlateSaveEntry
{
    public string id;
    public bool isPressed;
}

[Serializable]
public class LeverSaveEntry
{
    public string id;
    public bool isActivated;
}

[Serializable]
public class DestroyedEntityEntry
{
    public string id;
    public bool isDestroyed;
}

public class SaveManager : MonoBehaviour
{
    private const string SaveFileName = "save_data.json";
    private const float AutoSaveInterval = 2.0f;

    private static SaveManager _instance;
    public static SaveManager Instance => EnsureInstance();

    public static bool IsBootstrappingSession { get; set; }
    public static bool IsRestoringContinue { get; set; }
    public static SaveData PendingContinueData { get; private set; }
    public static bool HasPendingContinue => PendingContinueData != null;

    private readonly Dictionary<int, ZoneSaveData> runtimeZones = new Dictionary<int, ZoneSaveData>();
    private bool bossFightActive;
    private int bossTriggerZoneId = -1;
    private string bossTriggerEntityId;
    private Vector3 bossRestartPos;

    private float autoSaveTimer;
    private bool isDirty;
    private bool hasSavedInitialForCurrentScene;

    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static SaveManager EnsureInstance()
    {
        if (!Application.isPlaying) return null;
        if (_instance != null) return _instance;

        _instance = FindFirstObjectByType<SaveManager>();
        if (_instance != null) return _instance;

        GameObject go = new GameObject("SaveManager");
        _instance = go.AddComponent<SaveManager>();
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
    }

    private void Update()
    {
        if (IsBootstrappingSession || IsRestoringContinue) return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || GameManager.IsNonGameplayScene(activeScene.name)) return;
        if (GameManager.instance != null && GameManager.instance.IsTransitioning) return;
        if (ZoneManager.Instance != null && ZoneManager.Instance.IsInitializingFirstZone) return;
        if (Player.instance == null || Player.instance.status == null || Player.instance.status.HP <= 0) return;

        if (!hasSavedInitialForCurrentScene)
        {
            hasSavedInitialForCurrentScene = true;
            SaveCurrentState();
            autoSaveTimer = 0f;
            isDirty = false;
            return;
        }

        autoSaveTimer += Time.unscaledDeltaTime;
        if (isDirty && autoSaveTimer >= 0.35f)
        {
            SaveCurrentState();
            autoSaveTimer = 0f;
            isDirty = false;
        }
        else if (autoSaveTimer >= AutoSaveInterval)
        {
            SaveCurrentState();
            autoSaveTimer = 0f;
            isDirty = false;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveCurrentState();
        }
    }

    private void OnApplicationQuit()
    {
        SaveCurrentState();
    }

    public static bool HasSaveData()
    {
        return TryLoadSaveData(out _);
    }

    public static bool TryLoadSaveData(out SaveData data)
    {
        data = null;
        try
        {
            string path = SaveFilePath;
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return false;

            data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || string.IsNullOrEmpty(data.sceneName)) return false;
            if (GameManager.IsNonGameplayScene(data.sceneName)) return false;
            if (!Application.CanStreamedLevelBeLoaded(data.sceneName)) return false;

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] 세이브 데이터 로드 실패: {ex.Message}");
            data = null;
            return false;
        }
    }

    public static void ClearSaveData()
    {
        if (Application.isPlaying)
        {
            EnsureInstance();
            if (_instance != null)
            {
                _instance.runtimeZones.Clear();
                _instance.bossFightActive = false;
                _instance.bossTriggerZoneId = -1;
                _instance.bossTriggerEntityId = null;
                _instance.bossRestartPos = Vector3.zero;
                _instance.isDirty = false;
                _instance.hasSavedInitialForCurrentScene = false;
            }
        }

        PendingContinueData = null;
        IsRestoringContinue = false;
        IsBootstrappingSession = false;

        try
        {
            string path = SaveFilePath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] 세이브 파일 삭제 실패: {ex.Message}");
        }

        if (Application.isPlaying && SceneManager.GetActiveScene().name == "MainMenu")
        {
            GameStarting menu = FindFirstObjectByType<GameStarting>();
            if (menu != null)
            {
                menu.RefreshMainMenuContinueState();
            }
        }
    }

    public static void ContinueFromSave()
    {
        EnsureInstance();
        if (!TryLoadSaveData(out SaveData data))
        {
            Debug.LogWarning("[SaveManager] 이어하기 가능한 세이브 데이터가 없습니다.");
            return;
        }

        _instance.LoadIntoRuntimeState(data);
        PendingContinueData = data;
        IsRestoringContinue = true;

        if (GameManager.instance != null)
        {
            GameManager.instance.LoadContinueScene(data);
        }
        else
        {
            SceneManager.LoadSceneAsync(data.sceneName);
        }
    }

    public static void ClearPendingContinue()
    {
        PendingContinueData = null;
        IsRestoringContinue = false;
        IsBootstrappingSession = false;
        if (_instance != null)
        {
            _instance.hasSavedInitialForCurrentScene = true;
            _instance.autoSaveTimer = 0f;
            _instance.isDirty = false;
        }
    }

    public static void OnSceneTransitionRequested(string targetSceneName)
    {
        if (IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        if (targetSceneName == "GameClear")
        {
            ClearSaveData();
            return;
        }

        if (GameManager.IsNonGameplayScene(targetSceneName))
        {
            return;
        }

        // 새로운 씬 이동 또는 사망 재시작 시 현재 씬의 존/보스 진행 상태 초기화
        _instance.runtimeZones.Clear();
        _instance.bossFightActive = false;
        _instance.bossTriggerZoneId = -1;
        _instance.bossTriggerEntityId = null;
        _instance.bossRestartPos = Vector3.zero;
        _instance.hasSavedInitialForCurrentScene = false;
        _instance.isDirty = false;
    }

    public static void MarkDirty()
    {
        if (_instance != null && !IsBootstrappingSession && !IsRestoringContinue)
        {
            _instance.isDirty = true;
        }
    }

    public static void SaveCurrentState()
    {
        if (_instance == null) return;
        if (IsBootstrappingSession || IsRestoringContinue) return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || GameManager.IsNonGameplayScene(activeScene.name)) return;
        if (GameManager.instance != null && GameManager.instance.IsTransitioning) return;
        if (ZoneManager.Instance != null && ZoneManager.Instance.IsInitializingFirstZone) return;
        if (Player.instance == null || Player.instance.status == null) return;

        PlayerStatus status = Player.instance.status;
        if (status.HP <= 0) return;

        _instance.CaptureInstantiatedZones();

        SaveData data = new SaveData
        {
            version = 1,
            sceneName = activeScene.name,
            zoneId = _instance.bossFightActive && _instance.bossTriggerZoneId >= 0
                ? _instance.bossTriggerZoneId
                : (ZoneManager.Instance != null && ZoneManager.Instance.CurrentZoneId >= 0
                    ? ZoneManager.Instance.CurrentZoneId
                    : 0),
            facingRight = Player.instance.IsFacingRight,
            hp = Mathf.Clamp(Mathf.RoundToInt(status.HP), 1, Mathf.Max(1, Mathf.RoundToInt(status.MaxHP))),
            ink = status.ink,
            specialInk = status.specialInk,
            currentCoolTime = status.currentCoolTime,
            currentStance = (int)status.currentStance,
            combo = status.combo,
            isInBossFight = _instance.bossFightActive,
            bossTriggerZoneId = _instance.bossTriggerZoneId,
            bossTriggerEntityId = _instance.bossTriggerEntityId,
            bossRestartPosX = _instance.bossRestartPos.x,
            bossRestartPosY = _instance.bossRestartPos.y,
            bossRestartPosZ = _instance.bossRestartPos.z
        };

        if (_instance.bossFightActive)
        {
            data.posX = _instance.bossRestartPos.x;
            data.posY = _instance.bossRestartPos.y;
            data.posZ = _instance.bossRestartPos.z;
        }
        else
        {
            Vector3 playerPos = Player.instance.transform.position;
            data.posX = playerPos.x;
            data.posY = playerPos.y;
            data.posZ = playerPos.z;
        }

        if (TutorialManager.Instance != null)
        {
            data.completedTutorialSteps = TutorialManager.Instance.GetCompletedStepNames();
        }

        foreach (var kvp in _instance.runtimeZones)
        {
            data.zones.Add(kvp.Value);
        }

        WriteSaveFile(data);
    }

    private static void WriteSaveFile(SaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            string path = SaveFilePath;
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tempPath, path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] 세이브 파일 저장 실패: {ex.Message}");
        }
    }

    private void LoadIntoRuntimeState(SaveData data)
    {
        runtimeZones.Clear();
        if (data.zones != null)
        {
            for (int i = 0; i < data.zones.Count; i++)
            {
                ZoneSaveData z = data.zones[i];
                if (z != null)
                {
                    runtimeZones[z.zoneId] = z;
                }
            }
        }

        bossFightActive = data.isInBossFight;
        bossTriggerZoneId = data.bossTriggerZoneId;
        bossTriggerEntityId = data.bossTriggerEntityId;
        bossRestartPos = new Vector3(data.bossRestartPosX, data.bossRestartPosY, data.bossRestartPosZ);
    }

    public static void RegisterAndApplyZoneState(int zoneId, GameObject zoneObj)
    {
        if (zoneObj == null) return;
        EnsureInstance();

        AssignPersistentEntities(zoneId, zoneObj);

        if (!_instance.runtimeZones.TryGetValue(zoneId, out ZoneSaveData zoneData) || zoneData == null)
        {
            return;
        }

        bool resetBossInThisZone = _instance.bossFightActive
            && zoneId == _instance.bossTriggerZoneId
            && !zoneData.bossDefeated;

        // 1. 적 상태 복원 (보스전 초기화 대상일 경우 보스는 초기 상태 유지)
        EnemyStatus[] enemies = zoneObj.GetComponentsInChildren<EnemyStatus>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyStatus enemy = enemies[i];
            if (enemy == null) continue;

            if (enemy.IsBoss)
            {
                if (zoneData.bossDefeated)
                {
                    enemy.gameObject.SetActive(false);
                    Destroy(enemy.gameObject);
                    continue;
                }

                if (resetBossInThisZone)
                {
                    continue;
                }
            }

            PersistentEntity pe = enemy.GetComponent<PersistentEntity>();
            if (pe == null) continue;

            EnemySaveEntry entry = FindEnemyEntry(zoneData, pe.EntityId);
            if (entry == null) continue;

            if (entry.isDead)
            {
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            else
            {
                Vector3 savedPos = new Vector3(entry.posX, entry.posY, entry.posZ);
                int hpToRestore = entry.currentHP > 0 ? entry.currentHP : enemy.MaxHP;
                enemy.RestoreFromSave(hpToRestore, entry.isActive, entry.hasPosition, savedPos);
            }
        }

        // 2. 벽 상태 복원 (보스전 초기화 시 보스방 문은 비활성 유지)
        Wall[] walls = zoneObj.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null) continue;

            if (resetBossInThisZone)
            {
                wall.gameObject.SetActive(false);
                continue;
            }

            PersistentEntity pe = wall.GetComponent<PersistentEntity>();
            if (pe == null) continue;

            WallSaveEntry entry = FindWallEntry(zoneData, pe.EntityId);
            if (entry == null) continue;

            if (entry.isCollapsed)
            {
                wall.gameObject.SetActive(false);
                Destroy(wall.gameObject);
            }
            else
            {
                wall.gameObject.SetActive(entry.isActive);
            }
        }

        // 3. 발판 상태 복원
        PressurePlate[] plates = zoneObj.GetComponentsInChildren<PressurePlate>(true);
        for (int i = 0; i < plates.Length; i++)
        {
            PressurePlate plate = plates[i];
            if (plate == null) continue;

            PersistentEntity pe = plate.GetComponent<PersistentEntity>();
            if (pe == null) continue;

            PlateSaveEntry entry = FindPlateEntry(zoneData, pe.EntityId);
            if (entry != null)
            {
                plate.RestorePressedState(entry.isPressed);
            }
        }

        // 4. 레버 상태 복원
        Lever[] levers = zoneObj.GetComponentsInChildren<Lever>(true);
        for (int i = 0; i < levers.Length; i++)
        {
            Lever lever = levers[i];
            if (lever == null) continue;

            PersistentEntity pe = lever.GetComponent<PersistentEntity>();
            if (pe == null) continue;

            LeverSaveEntry entry = FindLeverEntry(zoneData, pe.EntityId);
            if (entry != null)
            {
                lever.RestoreActivated(entry.isActivated);
            }
        }

        // 5. 얼음 발판 / 폭탄 등 파괴형 기믹 복원
        IcePlatform[] icePlatforms = zoneObj.GetComponentsInChildren<IcePlatform>(true);
        for (int i = 0; i < icePlatforms.Length; i++)
        {
            IcePlatform ice = icePlatforms[i];
            if (ice == null) continue;
            PersistentEntity pe = ice.GetComponent<PersistentEntity>();
            if (pe == null) continue;

            if (IsEntityDestroyed(zoneData, pe.EntityId))
            {
                ice.gameObject.SetActive(false);
                Destroy(ice.gameObject);
            }
        }

        Bomb[] bombs = zoneObj.GetComponentsInChildren<Bomb>(true);
        for (int i = 0; i < bombs.Length; i++)
        {
            Bomb bomb = bombs[i];
            if (bomb == null) continue;
            PersistentEntity pe = bomb.GetComponent<PersistentEntity>();
            if (pe == null) continue;

            if (IsEntityDestroyed(zoneData, pe.EntityId))
            {
                bomb.gameObject.SetActive(false);
                Destroy(bomb.gameObject);
            }
        }

        // 6. 적 트리거 상태 복원
        EnemyTrigger[] triggers = zoneObj.GetComponentsInChildren<EnemyTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            EnemyTrigger trigger = triggers[i];
            if (trigger == null) continue;

            bool isBossTrigger = trigger.HasBoss();
            if (isBossTrigger)
            {
                if (zoneData.bossDefeated)
                {
                    trigger.SetActivatedFromSave(true);
                }
                else if (resetBossInThisZone)
                {
                    trigger.SetActivatedFromSave(false);
                }
                else
                {
                    PersistentEntity pe = trigger.GetComponent<PersistentEntity>();
                    TriggerSaveEntry entry = pe != null ? FindTriggerEntry(zoneData, pe.EntityId) : null;
                    if (entry != null)
                    {
                        trigger.SetActivatedFromSave(entry.activated);
                    }
                }
                continue;
            }

            PersistentEntity entity = trigger.GetComponent<PersistentEntity>();
            if (entity == null) continue;

            TriggerSaveEntry triggerEntry = FindTriggerEntry(zoneData, entity.EntityId);
            if (triggerEntry != null)
            {
                trigger.SetActivatedFromSave(triggerEntry.activated);
                if (triggerEntry.activated)
                {
                    trigger.EnsureAliveEnemiesActive();
                }
            }
        }

        // 7. 보스 처치 후 종료했다면 포탈 활성화 유지
        if (zoneData.bossDefeated)
        {
            ActivatePortalInZone(zoneObj);
        }
    }

    private static void ActivatePortalInZone(GameObject zoneObj)
    {
        Transform[] allTransforms = zoneObj.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t != null && t.CompareTag("Portal") && t.childCount > 0)
            {
                t.GetChild(0).gameObject.SetActive(true);
            }
        }
    }

    private static void AssignPersistentEntities(int zoneId, GameObject zoneObj)
    {
        Transform root = zoneObj.transform;
        AssignForType<EnemyStatus>(zoneId, root);
        AssignForType<EnemyTrigger>(zoneId, root);
        AssignForType<Wall>(zoneId, root);
        AssignForType<PressurePlate>(zoneId, root);
        AssignForType<Lever>(zoneId, root);
        AssignForType<IcePlatform>(zoneId, root);
        AssignForType<Bomb>(zoneId, root);
    }

    private static void AssignForType<T>(int zoneId, Transform zoneRoot) where T : Component
    {
        T[] components = zoneRoot.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T comp = components[i];
            if (comp == null) continue;

            PersistentEntity pe = comp.GetComponent<PersistentEntity>();
            if (pe == null)
            {
                pe = comp.gameObject.AddComponent<PersistentEntity>();
            }
            if (!pe.IsInitialized)
            {
                string path = GetRelativeSiblingPath(zoneRoot, comp.transform);
                pe.Initialize(zoneId, path);
            }
        }
    }

    private static string GetRelativeSiblingPath(Transform root, Transform target)
    {
        if (target == root) return "root";

        List<int> indices = new List<int>(8);
        Transform current = target;
        while (current != null && current != root)
        {
            indices.Add(current.GetSiblingIndex());
            current = current.parent;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = indices.Count - 1; i >= 0; i--)
        {
            if (sb.Length > 0) sb.Append('/');
            sb.Append(indices[i]);
        }
        return sb.ToString();
    }

    public static void NotifyBossFightStarted(EnemyTrigger trigger, Vector3 restartPos)
    {
        if (trigger == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = trigger.GetComponent<PersistentEntity>();
        int zId = pe != null ? pe.ZoneId : (ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZoneId : 0);
        string eId = pe != null ? pe.EntityId : string.Empty;

        _instance.bossFightActive = true;
        _instance.bossTriggerZoneId = zId;
        _instance.bossTriggerEntityId = eId;
        _instance.bossRestartPos = restartPos;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(zId);
        zoneData.bossDefeated = false;

        SaveCurrentState();
    }

    public static void NotifyBossDefeated(EnemyStatus boss)
    {
        if (boss == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = boss.GetComponent<PersistentEntity>();
        int zId = pe != null ? pe.ZoneId : _instance.bossTriggerZoneId;

        _instance.bossFightActive = false;
        if (zId >= 0)
        {
            ZoneSaveData zoneData = _instance.GetOrCreateZoneData(zId);
            zoneData.bossDefeated = true;
            if (pe != null)
            {
                EnemySaveEntry entry = GetOrCreateEnemyEntry(zoneData, pe.EntityId);
                entry.isDead = true;
                entry.currentHP = 0;
                entry.isActive = false;
            }
        }

        SaveCurrentState();
    }

    public static void NotifyEnemyDied(EnemyStatus enemy)
    {
        if (enemy == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = enemy.GetComponent<PersistentEntity>();
        if (pe == null || !pe.IsInitialized) return;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(pe.ZoneId);
        EnemySaveEntry entry = GetOrCreateEnemyEntry(zoneData, pe.EntityId);
        entry.isDead = true;
        entry.currentHP = 0;
        entry.isActive = false;
        _instance.isDirty = true;
    }

    public static void NotifyWallCollapsed(Wall wall)
    {
        if (wall == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = wall.GetComponent<PersistentEntity>();
        if (pe == null || !pe.IsInitialized) return;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(pe.ZoneId);
        WallSaveEntry entry = GetOrCreateWallEntry(zoneData, pe.EntityId);
        entry.isCollapsed = true;
        entry.isActive = false;
        _instance.isDirty = true;
    }

    public static void NotifyWallRebuilt(Wall wall)
    {
        if (wall == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = wall.GetComponent<PersistentEntity>();
        if (pe == null || !pe.IsInitialized) return;

        // 보스전 진행 중 보스방 문이 닫히는 것은 보스전 초기화 시 다시 열려야 하므로 저장하지 않음
        if (_instance.bossFightActive && pe.ZoneId == _instance.bossTriggerZoneId) return;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(pe.ZoneId);
        WallSaveEntry entry = GetOrCreateWallEntry(zoneData, pe.EntityId);
        entry.isCollapsed = false;
        entry.isActive = true;
        _instance.isDirty = true;
    }

    public static void NotifyPressurePlateChanged(PressurePlate plate, bool isPressed)
    {
        if (plate == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = plate.GetComponent<PersistentEntity>();
        if (pe == null || !pe.IsInitialized) return;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(pe.ZoneId);
        PlateSaveEntry entry = GetOrCreatePlateEntry(zoneData, pe.EntityId);
        entry.isPressed = isPressed;
        _instance.isDirty = true;
    }

    public static void NotifyLeverActivated(Lever lever)
    {
        if (lever == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = lever.GetComponent<PersistentEntity>();
        if (pe == null || !pe.IsInitialized) return;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(pe.ZoneId);
        LeverSaveEntry entry = GetOrCreateLeverEntry(zoneData, pe.EntityId);
        entry.isActivated = true;
        _instance.isDirty = true;
    }

    public static void NotifyObjectDestroyed(Component comp)
    {
        if (comp == null || IsRestoringContinue || IsBootstrappingSession) return;
        EnsureInstance();

        PersistentEntity pe = comp.GetComponent<PersistentEntity>();
        if (pe == null || !pe.IsInitialized) return;

        ZoneSaveData zoneData = _instance.GetOrCreateZoneData(pe.ZoneId);
        MarkEntityDestroyed(zoneData, pe.EntityId);
        _instance.isDirty = true;
    }

    private void CaptureInstantiatedZones()
    {
        if (ZoneManager.Instance == null) return;

        IReadOnlyDictionary<int, GameObject> cachedZones = ZoneManager.Instance.CachedZones;
        if (cachedZones == null) return;

        foreach (var kvp in cachedZones)
        {
            int zoneId = kvp.Key;
            GameObject zoneObj = kvp.Value;
            if (zoneObj == null) continue;

            ZoneSaveData zoneData = GetOrCreateZoneData(zoneId);
            bool skipBossRoomEntities = bossFightActive && zoneId == bossTriggerZoneId && !zoneData.bossDefeated;

            // 적 상태 캡처 (이미 죽어서 파괴된 적은 NotifyEnemyDied 기록 유지)
            EnemyStatus[] enemies = zoneObj.GetComponentsInChildren<EnemyStatus>(true);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyStatus enemy = enemies[i];
                if (enemy == null) continue;
                if (enemy.IsBoss && skipBossRoomEntities) continue;

                PersistentEntity pe = enemy.GetComponent<PersistentEntity>();
                if (pe == null || !pe.IsInitialized) continue;

                EnemySaveEntry entry = GetOrCreateEnemyEntry(zoneData, pe.EntityId);
                if (enemy.IsDead)
                {
                    entry.isDead = true;
                    entry.currentHP = 0;
                    entry.isActive = false;
                }
                else
                {
                    entry.isDead = false;
                    entry.currentHP = enemy.HasAwoken ? enemy.CurrentHP : enemy.MaxHP;
                    entry.isActive = enemy.gameObject.activeSelf;
                    Vector3 pos = enemy.transform.position;
                    entry.hasPosition = true;
                    entry.posX = pos.x;
                    entry.posY = pos.y;
                    entry.posZ = pos.z;
                }
            }

            // 트리거 상태 캡처
            EnemyTrigger[] triggers = zoneObj.GetComponentsInChildren<EnemyTrigger>(true);
            for (int i = 0; i < triggers.Length; i++)
            {
                EnemyTrigger trigger = triggers[i];
                if (trigger == null) continue;
                if (trigger.HasBoss() && skipBossRoomEntities) continue;

                PersistentEntity pe = trigger.GetComponent<PersistentEntity>();
                if (pe == null || !pe.IsInitialized) continue;

                TriggerSaveEntry entry = GetOrCreateTriggerEntry(zoneData, pe.EntityId);
                entry.activated = trigger.IsActivated;
            }

            // 벽 상태 캡처 (파괴된 벽은 NotifyWallCollapsed 기록 유지)
            Wall[] walls = zoneObj.GetComponentsInChildren<Wall>(true);
            for (int i = 0; i < walls.Length; i++)
            {
                Wall wall = walls[i];
                if (wall == null) continue;
                if (skipBossRoomEntities) continue;

                PersistentEntity pe = wall.GetComponent<PersistentEntity>();
                if (pe == null || !pe.IsInitialized) continue;

                WallSaveEntry entry = GetOrCreateWallEntry(zoneData, pe.EntityId);
                if (!entry.isCollapsed)
                {
                    entry.isActive = wall.gameObject.activeSelf;
                }
            }

            // 발판 상태 캡처
            PressurePlate[] plates = zoneObj.GetComponentsInChildren<PressurePlate>(true);
            for (int i = 0; i < plates.Length; i++)
            {
                PressurePlate plate = plates[i];
                if (plate == null) continue;

                PersistentEntity pe = plate.GetComponent<PersistentEntity>();
                if (pe == null || !pe.IsInitialized) continue;

                PlateSaveEntry entry = GetOrCreatePlateEntry(zoneData, pe.EntityId);
                entry.isPressed = plate.IsPressed;
            }

            // 레버 상태 캡처
            Lever[] levers = zoneObj.GetComponentsInChildren<Lever>(true);
            for (int i = 0; i < levers.Length; i++)
            {
                Lever lever = levers[i];
                if (lever == null) continue;

                PersistentEntity pe = lever.GetComponent<PersistentEntity>();
                if (pe == null || !pe.IsInitialized) continue;

                LeverSaveEntry entry = GetOrCreateLeverEntry(zoneData, pe.EntityId);
                entry.isActivated = lever.IsActivated;
            }
        }
    }

    private ZoneSaveData GetOrCreateZoneData(int zoneId)
    {
        if (!runtimeZones.TryGetValue(zoneId, out ZoneSaveData zoneData) || zoneData == null)
        {
            zoneData = new ZoneSaveData { zoneId = zoneId };
            runtimeZones[zoneId] = zoneData;
        }
        return zoneData;
    }

    private static EnemySaveEntry FindEnemyEntry(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.enemies.Count; i++)
        {
            if (zone.enemies[i].id == id) return zone.enemies[i];
        }
        return null;
    }

    private static EnemySaveEntry GetOrCreateEnemyEntry(ZoneSaveData zone, string id)
    {
        EnemySaveEntry entry = FindEnemyEntry(zone, id);
        if (entry == null)
        {
            entry = new EnemySaveEntry { id = id };
            zone.enemies.Add(entry);
        }
        return entry;
    }

    private static TriggerSaveEntry FindTriggerEntry(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.triggers.Count; i++)
        {
            if (zone.triggers[i].id == id) return zone.triggers[i];
        }
        return null;
    }

    private static TriggerSaveEntry GetOrCreateTriggerEntry(ZoneSaveData zone, string id)
    {
        TriggerSaveEntry entry = FindTriggerEntry(zone, id);
        if (entry == null)
        {
            entry = new TriggerSaveEntry { id = id };
            zone.triggers.Add(entry);
        }
        return entry;
    }

    private static WallSaveEntry FindWallEntry(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.walls.Count; i++)
        {
            if (zone.walls[i].id == id) return zone.walls[i];
        }
        return null;
    }

    private static WallSaveEntry GetOrCreateWallEntry(ZoneSaveData zone, string id)
    {
        WallSaveEntry entry = FindWallEntry(zone, id);
        if (entry == null)
        {
            entry = new WallSaveEntry { id = id, isActive = true };
            zone.walls.Add(entry);
        }
        return entry;
    }

    private static PlateSaveEntry FindPlateEntry(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.plates.Count; i++)
        {
            if (zone.plates[i].id == id) return zone.plates[i];
        }
        return null;
    }

    private static PlateSaveEntry GetOrCreatePlateEntry(ZoneSaveData zone, string id)
    {
        PlateSaveEntry entry = FindPlateEntry(zone, id);
        if (entry == null)
        {
            entry = new PlateSaveEntry { id = id };
            zone.plates.Add(entry);
        }
        return entry;
    }

    private static LeverSaveEntry FindLeverEntry(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.levers.Count; i++)
        {
            if (zone.levers[i].id == id) return zone.levers[i];
        }
        return null;
    }

    private static LeverSaveEntry GetOrCreateLeverEntry(ZoneSaveData zone, string id)
    {
        LeverSaveEntry entry = FindLeverEntry(zone, id);
        if (entry == null)
        {
            entry = new LeverSaveEntry { id = id };
            zone.levers.Add(entry);
        }
        return entry;
    }

    private static bool IsEntityDestroyed(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.destroyedEntities.Count; i++)
        {
            if (zone.destroyedEntities[i].id == id && zone.destroyedEntities[i].isDestroyed)
            {
                return true;
            }
        }
        return false;
    }

    private static void MarkEntityDestroyed(ZoneSaveData zone, string id)
    {
        for (int i = 0; i < zone.destroyedEntities.Count; i++)
        {
            if (zone.destroyedEntities[i].id == id)
            {
                zone.destroyedEntities[i].isDestroyed = true;
                return;
            }
        }
        zone.destroyedEntities.Add(new DestroyedEntityEntry { id = id, isDestroyed = true });
    }
}
