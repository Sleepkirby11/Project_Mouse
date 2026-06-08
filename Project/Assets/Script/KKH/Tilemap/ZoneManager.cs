using System.Collections.Generic;
using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject playerObject; 

    [Header("최초 시작 구역 프리팹")]
    [SerializeField] private GameObject startZonePrefab;

    private Dictionary<int, GameObject> activeZones = new Dictionary<int, GameObject>();
    private int currentZoneId = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (startZonePrefab != null)
        {
            Zone startZone = startZonePrefab.GetComponent<Zone>();
            if (startZone != null)
            {
                // 1. 첫 구역 생성
                GameObject zoneObj = Instantiate(startZonePrefab, startZonePrefab.transform.position, Quaternion.identity);
                Zone instantiatedZone = zoneObj.GetComponent<Zone>();
                activeZones.Add(instantiatedZone.zoneId, zoneObj);

                // 2. 플레이어를 첫 구역의 스폰 지점으로 순간이동
                if (playerObject != null && instantiatedZone.spawnPoint != null)
                {
                    playerObject.transform.position = instantiatedZone.spawnPoint.position;
                    Debug.Log($"[ZoneManager] 플레이어가 {instantiatedZone.zoneId}번 구역 스폰 지점으로 이동했습니다.");
                }
                else
                {
                    Debug.LogWarning("[ZoneManager] 플레이어 오브젝트 또는 구역의 Spawn Point가 지정되지 않았습니다.");
                }

                // 3. 첫 구역 주변 맵 로드
                OnPlayerEnterZone(instantiatedZone);
            }
        }
    }

    public void OnPlayerEnterZone(Zone enteredZone)
    {
        if (currentZoneId == enteredZone.zoneId) return;

        currentZoneId = enteredZone.zoneId;
        Debug.Log($"[ZoneManager] 플레이어가 {currentZoneId}번 구역에 진입했습니다.");

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

        if (zonesToRemove.Count > 0)
        {
            Resources.UnloadUnusedAssets();
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
        GameObject zonePrefab = Resources.Load<GameObject>($"Zones/Zone_{zoneId}");

        if (zonePrefab != null)
        {
            GameObject spawnedZone = Instantiate(zonePrefab, zonePrefab.transform.position, zonePrefab.transform.rotation);
            activeZones.Add(zoneId, spawnedZone);
        }
        else
        {
            Debug.LogWarning($"[ZoneManager] Resources/Zones/Zone_{zoneId} 프리팹을 찾을 수 없습니다.");
        }
    }
}