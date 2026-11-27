using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject playerObject;

    [Header("현재 씬의 맵 조각 데이터 리스트")]
    [Tooltip("인스펙터의 Element 번호가 곧 구역 ID가 됩니다. (0번칸 = 0번 구역)")]
    [SerializeField] private List<GameObject> zonePrefabs = new List<GameObject>();

    [SerializeField] private List<GameObject> onPlayerZone = new List<GameObject>();
    [SerializeField] private Dictionary<int, GameObject> activeZones = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> cachedZones = new Dictionary<int, GameObject>();
    private readonly HashSet<int> loadingZones = new HashSet<int>();
    private readonly HashSet<int> desiredZones = new HashSet<int>();
    private int currentZoneId = -1;
    private int sceneGeneration = 0;
    private bool isInitializingFirstZone = false;
    private bool hasSceneZoneDataThisLoad = false;
    public bool IsInitializingFirstZone => isInitializingFirstZone;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            hasSceneZoneDataThisLoad = true;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Instance.UpdateSceneData(this.zonePrefabs);
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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
        InitFirstZone();
    }

    public void UpdateSceneData(List<GameObject> newPrefabs)
    {
        this.zonePrefabs = new List<GameObject>(newPrefabs);
        hasSceneZoneDataThisLoad = true;
        Debug.Log($"[ZoneManager] 새로운 씬의 맵 데이터 {zonePrefabs.Count}개를 갱신했습니다.");
    }

    public void ClearSessionZones()
    {
        StopAllCoroutines();
        sceneGeneration++;
        hasSceneZoneDataThisLoad = false;

        if (zonePrefabs != null) zonePrefabs.Clear();
        onPlayerZone.Clear();
        activeZones.Clear();
        cachedZones.Clear();
        loadingZones.Clear();
        desiredZones.Clear();
        currentZoneId = -1;
        isInitializingFirstZone = false;
    }

    private void InitFirstZone()
    {
        if (!hasSceneZoneDataThisLoad)
        {
            ClearSessionZones();
            return;
        }

        hasSceneZoneDataThisLoad = false;
        StopAllCoroutines();
        sceneGeneration++;

        onPlayerZone.Clear();
        activeZones.Clear();
        cachedZones.Clear();
        loadingZones.Clear();
        desiredZones.Clear();
        currentZoneId = -1;
        isInitializingFirstZone = false;

        if (zonePrefabs == null || zonePrefabs.Count == 0) return;

        int firstZoneId = 0;
        desiredZones.Add(firstZoneId);
        isInitializingFirstZone = true;
        StartCoroutine(InitAndSpawnFirstZoneRoutine(firstZoneId, sceneGeneration));
    }

    private IEnumerator InitAndSpawnFirstZoneRoutine(int firstZoneId, int generation)
    {
        isInitializingFirstZone = true;

        if (playerObject == null)
        {
            playerObject = Player.instance != null ? Player.instance.gameObject : GameObject.FindWithTag("Player");
        }

        Rigidbody2D playerRb = null;
        bool prevSimulated = true;
        if (playerObject != null)
        {
            playerRb = playerObject.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                prevSimulated = playerRb.simulated;
                playerRb.linearVelocity = Vector2.zero;
                playerRb.simulated = false;
            }
        }

        yield return StartCoroutine(LoadZonePrefabAsync(firstZoneId, generation));

        if (generation != sceneGeneration)
        {
            isInitializingFirstZone = false;
            yield break;
        }

        if (activeZones.TryGetValue(firstZoneId, out GameObject startZoneObj) && startZoneObj != null)
        {
            Zone startZone = startZoneObj.GetComponent<Zone>();
            if (startZone != null && startZone.spawnPoint != null && playerObject != null)
            {
                yield return StartCoroutine(SafeTeleport(playerObject.transform, startZone.spawnPoint.position));

                if (generation != sceneGeneration)
                {
                    isInitializingFirstZone = false;
                    yield break;
                }

                if (playerRb != null)
                {
                    playerRb.simulated = prevSimulated;
                }

                OnPlayerEnterZone(startZone);
                isInitializingFirstZone = false;
                yield break;
            }
        }

        if (playerRb != null)
        {
            playerRb.simulated = prevSimulated;
        }

        isInitializingFirstZone = false;
    }

    private IEnumerator SafeTeleport(Transform target, Vector3 destination)
    {
        yield return new WaitForEndOfFrame();

        if (target == null)
            yield break;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Vector3 prevPosition = target.position;
        target.position = destination;

        if (CameraSetting.instance != null)
        {
            CameraSetting.instance.SnapToTarget(target, destination - prevPosition);
        }

        yield return null;
    }

    public void OnPlayerEnterZone(Zone enteredZone)
    {
        if (enteredZone == null || enteredZone.zoneId == -1) return;

        // 플레이어가 새로운 콜라이더 경계를 밟아 트리거가 인식되었을 때의 원시 로그를 출력합니다.
        Debug.Log($"[ZoneManager] 플레이어가 {enteredZone.zoneId}번 구역 트리거 콜라이더에 진입 시도 중입니다.");

        // 이미 활성화된 현재 구역과 중복되는 연산일 경우 리턴 처리되기 전에 확인 가능하도록 상단에 배치했습니다.
        if (currentZoneId == enteredZone.zoneId) return;

        currentZoneId = enteredZone.zoneId;

        // 필터 및 로드 처리가 정상적으로 승인되어 현재 구역 상태가 전환되었음을 알리는 확정 로그입니다.
        Debug.Log($"[ZoneManager] 현재 활성화된 구역이 {currentZoneId}번 구역으로 성공적으로 갱신되었습니다.");

        desiredZones.Clear();
        desiredZones.Add(currentZoneId);
        foreach (int neighborId in enteredZone.connectedZoneIds)
        {
            desiredZones.Add(neighborId);
        }

        List<int> zonesToDeactivate = new List<int>();
        foreach (int activeId in activeZones.Keys)
        {
            if (!desiredZones.Contains(activeId))
            {
                if (activeZones[activeId] != null)
                {
                    activeZones[activeId].SetActive(false);
                }
                zonesToDeactivate.Add(activeId);
            }
        }

        foreach (int removeId in zonesToDeactivate)
        {
            activeZones.Remove(removeId);
        }

        int gen = sceneGeneration;
        foreach (int keepId in desiredZones)
        {
            if (activeZones.ContainsKey(keepId))
                continue;

            if (cachedZones.TryGetValue(keepId, out GameObject cachedZone) && cachedZone != null)
            {
                cachedZone.SetActive(true);
                activeZones[keepId] = cachedZone;
            }
            else if (!loadingZones.Contains(keepId))
            {
                StartCoroutine(LoadZonePrefabAsync(keepId, gen));
            }
        }
    }

    private IEnumerator LoadZonePrefabAsync(int zoneId, int generation)
    {
        if (zonePrefabs == null || zoneId < 0 || zoneId >= zonePrefabs.Count)
            yield break;

        if (cachedZones.TryGetValue(zoneId, out GameObject existing) && existing != null)
        {
            if (desiredZones.Contains(zoneId))
            {
                existing.SetActive(true);
                activeZones[zoneId] = existing;
            }
            yield break;
        }

        GameObject zonePrefab = zonePrefabs[zoneId];
        if (zonePrefab == null)
            yield break;

        loadingZones.Add(zoneId);
        AsyncInstantiateOperation<GameObject> op = Object.InstantiateAsync(
            zonePrefab,
            zonePrefab.transform.position,
            zonePrefab.transform.rotation
        );

        while (!op.isDone)
        {
            yield return null;
        }

        loadingZones.Remove(zoneId);

        GameObject spawnedZone = (op.Result != null && op.Result.Length > 0) ? op.Result[0] : null;
        if (spawnedZone == null)
            yield break;

        // 비동기 로드 도중 씬이 전환되었다면 즉시 폐기
        if (generation != sceneGeneration)
        {
            Destroy(spawnedZone);
            yield break;
        }

        Zone zoneScript = spawnedZone.GetComponent<Zone>();
        if (zoneScript != null)
        {
            zoneScript.zoneId = zoneId;
            Debug.Log($"[ZoneManager] 하이어라키에 고유 ID {zoneId}번 구역 프리팹 인스턴스가 비동기 로드되었습니다.");
        }

        cachedZones[zoneId] = spawnedZone;

        if (desiredZones.Contains(zoneId))
        {
            spawnedZone.SetActive(true);
            activeZones[zoneId] = spawnedZone;
        }
        else
        {
            spawnedZone.SetActive(false);
        }
    }

    public void UpdateOnPlayerZone(GameObject playerZone, bool isEnter)
    {
        onPlayerZone.RemoveAll(z => z == null || !z.activeInHierarchy);

        if (isEnter)
        {
            if (playerZone != null && !onPlayerZone.Contains(playerZone))
                onPlayerZone.Add(playerZone);
        }
        else
        {
            onPlayerZone.Remove(playerZone);
        }

        if (onPlayerZone.Count > 0)
        {
            Zone targetZone = onPlayerZone.Last().GetComponent<Zone>();
            if (targetZone != null)
            {
                OnPlayerEnterZone(targetZone);
            }
        }
    }
}