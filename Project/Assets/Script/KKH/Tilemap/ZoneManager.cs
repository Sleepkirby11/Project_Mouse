using System.Collections.Generic;
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

    private Dictionary<int, GameObject> activeZones = new Dictionary<int, GameObject>();
    private int currentZoneId = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Instance.UpdateSceneData(this.zonePrefabs);
            Destroy(gameObject);
            return;
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
        Debug.Log($"[ZoneManager] 새로운 씬의 맵 데이터 {zonePrefabs.Count}개를 갱신했습니다.");
    }

    private void InitFirstZone()
    {
        if (zonePrefabs == null || zonePrefabs.Count == 0) return;

        activeZones.Clear();
        currentZoneId = -1;

        int firstZoneId = 0;
        LoadZonePrefab(firstZoneId);

        if (activeZones.TryGetValue(firstZoneId, out GameObject startZoneObj))
        {
            Zone startZone = startZoneObj.GetComponent<Zone>();

            if (startZone != null)
            {
                if (playerObject == null)
                {
                    playerObject = GameObject.FindWithTag("Player");
                }

                if (playerObject != null)
                {
                    StartCoroutine(SpawnProcessRoutine(startZone));
                }
            }
        }
    }

    private System.Collections.IEnumerator SpawnProcessRoutine(Zone startZone)
    {
        yield return null;

        if (startZone != null && startZone.spawnPoint != null && playerObject != null)
        {
            Vector3 spawnPosition = startZone.spawnPoint.position;

            yield return StartCoroutine(SafeTeleport(playerObject.transform, spawnPosition));

            OnPlayerEnterZone(startZone);
        }
    }

    private System.Collections.IEnumerator SafeTeleport(Transform target, Vector3 destination)
    {
        yield return new WaitForEndOfFrame();

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        target.position = destination;

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

        HashSet<int> zonesToKeep = new HashSet<int> { currentZoneId };
        foreach (int neighborId in enteredZone.connectedZoneIds)
        {
            zonesToKeep.Add(neighborId);
        }

        List<int> zonesToRemove = new List<int>();
        foreach (int activeId in activeZones.Keys)
        {
            if (!zonesToKeep.Contains(activeId))
            {
                Destroy(activeZones[activeId]);
                zonesToRemove.Add(activeId);
            }
        }

        foreach (int removeId in zonesToRemove)
        {
            activeZones.Remove(removeId);
        }

        foreach (int keepId in zonesToKeep)
        {
            if (!activeZones.ContainsKey(keepId))
            {
                LoadZonePrefab(keepId);
            }
        }
    }

    private void LoadZonePrefab(int zoneId)
    {
        if (zoneId < 0 || zoneId >= zonePrefabs.Count) return;

        GameObject zonePrefab = zonePrefabs[zoneId];

        if (zonePrefab != null)
        {
            GameObject spawnedZone = Instantiate(zonePrefab, zonePrefab.transform.position, zonePrefab.transform.rotation);

            Zone zoneScript = spawnedZone.GetComponent<Zone>();
            if (zoneScript != null)
            {
                zoneScript.zoneId = zoneId;
                Debug.Log($"[ZoneManager] 하이어라키에 고유 ID {zoneId}번 구역 프리팹 인스턴스가 로드되었습니다.");
            }

            activeZones.Add(zoneId, spawnedZone);
        }
    }
}