using System.Collections.Generic;
using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject playerObject; 
    [Tooltip("Optional: Player prefab to instantiate at runtime if no player exists in the scene.")]
    [SerializeField] private GameObject playerPrefab;

    [Header("���� ���� ���� ������")]
    [SerializeField] private GameObject startZonePrefab;

    private Dictionary<int, GameObject> activeZones = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> zonePrefabIndex = new Dictionary<int, GameObject>();
    private int currentZoneId = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Ensure there's a player instance (find in scene or instantiate prefab)
        EnsurePlayer();

        // Index all Zone prefabs under Resources/Tilemaps (includes subfolders)
        IndexZonePrefabs();

        if (startZonePrefab != null)
        {
            Zone startZone = startZonePrefab.GetComponent<Zone>();
            if (startZone != null)
            {
                GameObject zoneObj = Instantiate(startZonePrefab, startZonePrefab.transform.position, Quaternion.identity);
                Zone instantiatedZone = zoneObj.GetComponent<Zone>();
                activeZones.Add(instantiatedZone.zoneId, zoneObj);

                if (playerObject == null)
                {
                    Debug.LogWarning("[ZoneManager] playerObject가 할당되어 있지 않습니다. 스폰을 건너뜁니다.");
                }
                else
                {
                    Transform sp = instantiatedZone.spawnPoint;
                    if (sp == null)
                    {
                        sp = FindChildRecursive(instantiatedZone.transform, "SpawnPoint");
                        if (sp != null) instantiatedZone.spawnPoint = sp;
                    }

                    if (sp != null)
                    {
                        Debug.Log($"[ZoneManager] 이동 전 playerObject: name={playerObject.name}, activeInHierarchy={playerObject.activeInHierarchy}, sceneValid={playerObject.scene.IsValid()}");
                        playerObject.transform.position = sp.position;
                        Debug.Log($"[ZoneManager] 플레이어를 {instantiatedZone.zoneId}번 존의 SpawnPoint로 이동했습니다. 위치:{sp.position} | playerObject now at:{playerObject.transform.position}");
                    }
                    else
                    {
                        playerObject.transform.position = instantiatedZone.transform.position;
                        Debug.LogWarning($"[ZoneManager] SpawnPoint가 없습니다. 플레이어를 존 루트 위치로 이동: {instantiatedZone.zoneId}. playerObject: name={playerObject.name}, activeInHierarchy={playerObject.activeInHierarchy}");
                    }
                }

                // 3. ù ���� �ֺ� �� �ε�
                OnPlayerEnterZone(instantiatedZone);
            }
        }
    }

    public void OnPlayerEnterZone(Zone enteredZone)
    {
        if (currentZoneId == enteredZone.zoneId) return;

        currentZoneId = enteredZone.zoneId;
        Debug.Log($"[ZoneManager] �÷��̾ {currentZoneId}�� ������ �����߽��ϴ�.");

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
        // 먼저 인덱스에서 찾기
        if (zonePrefabIndex != null && zonePrefabIndex.TryGetValue(zoneId, out GameObject indexedPrefab))
        {
            GameObject spawnedZone = Instantiate(indexedPrefab, indexedPrefab.transform.position, indexedPrefab.transform.rotation);
            activeZones.Add(zoneId, spawnedZone);
            Debug.Log($"[ZoneManager] Indexed prefab으로 Zone_{zoneId} 로드 및 인스턴스화 완료: {indexedPrefab.name}");
            return;
        }

        // 인덱스에 없으면 기존 방식 시도
        GameObject zonePrefab = Resources.Load<GameObject>($"Tilemaps/Tilemaps_{zoneId}");
        Debug.Log($"[ZoneManager] Resources.Load로 Tilemaps_{zoneId}를 시도했습니다. 결과: {(zonePrefab != null ? "성공" : "실패")}");

        if (zonePrefab != null)
        {
            GameObject spawnedZone = Instantiate(zonePrefab, zonePrefab.transform.position, zonePrefab.transform.rotation);
            activeZones.Add(zoneId, spawnedZone);
        }
        else
        {
            Debug.LogWarning($"[ZoneManager] Resources/Tilemaps/Tilemaps_{zoneId}를 찾을 수 없습니다.");
        }
    }

    private void IndexZonePrefabs()
    {
        zonePrefabIndex.Clear();
        GameObject[] prefabs = Resources.LoadAll<GameObject>("Tilemaps");
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            var z = prefab.GetComponent<Zone>();
            if (z != null)
            {
                if (zonePrefabIndex.ContainsKey(z.zoneId))
                {
                    Debug.LogWarning($"[ZoneManager] 중복 zoneId {z.zoneId} 발견: 기존={zonePrefabIndex[z.zoneId].name} 새로찾음={prefab.name}");
                }
                else
                {
                    zonePrefabIndex.Add(z.zoneId, prefab);
                }
            }
        }
        Debug.Log($"[ZoneManager] IndexZonePrefabs 완료. found {zonePrefabIndex.Count} prefabs under Resources/Tilemaps (including subfolders).\n");
    }

    private void EnsurePlayer()
    {
        // // If inspector assigned something to playerObject, it may be a scene instance or a prefab asset.
        // if (playerObject != null)
        // {
        //     bool sceneValid = playerObject.scene.IsValid();
        //     Debug.Log($"[ZoneManager] playerObject 필드에 값이 있습니다. name={playerObject.name}, sceneValid={sceneValid}, activeInHierarchy={playerObject.activeInHierarchy}");
        //     if (!sceneValid)
        //     {
        //         // Likely a prefab asset assigned to the field — instantiate a runtime instance
        //         if (Application.isPlaying)
        //         {
        //             Debug.Log("[ZoneManager] playerObject가 프리팹 에셋으로 보입니다. 런타임 인스턴스화합니다.");
        //             GameObject inst = Instantiate(playerObject);
        //             inst.name = inst.name.Replace("(Clone)", ""); // keep readable
        //             playerObject = inst;
        //             Debug.Log($"[ZoneManager] playerPrefab 인스턴스화 완료: name={playerObject.name}, instanceID={playerObject.GetInstanceID()}, activeInHierarchy={playerObject.activeInHierarchy}");
        //         }
        //         else
        //         {
        //             Debug.LogWarning("[ZoneManager] playerObject가 에디터에서 프리팹 에셋으로 지정되어 있습니다. 플레이 모드에서 동작을 확인하세요.");
        //         }
        //     }
        //     else
        //     {
        //         // existing scene instance
        //         if (!playerObject.activeInHierarchy) playerObject.SetActive(true);
        //         Debug.Log($"[ZoneManager] 씬에 있는 playerObject를 사용합니다: name={playerObject.name}");
        //     }
        //     return;
        // }

        // 1) 씬에 Player 태그로 배치된 오브젝트 검색
        GameObject found = GameObject.FindWithTag("Player");
        if (found != null)
        {
            playerObject = found;
            Debug.Log($"[ZoneManager] 씬에서 태그로 플레이어를 찾음: name={playerObject.name}");
            return;
        }

        // 2) playerPrefab이 할당되어 있으면 인스턴스화
        // if (playerPrefab != null)
        // {
        //     Debug.Log("[ZoneManager] playerPrefab으로부터 플레이어를 인스턴스화합니다.");
        //     playerObject = Instantiate(playerPrefab);
        //     Debug.Log($"[ZoneManager] playerPrefab 인스턴스화 완료: name={playerObject.name}, instanceID={playerObject.GetInstanceID()}");
        //     return;
        // }

        Debug.LogWarning("[ZoneManager] playerObject와 playerPrefab이 모두 비어있습니다. 플레이어를 찾거나 prefab을 할당하세요.");
    }

    // 재귀적으로 자식에서 이름으로 Transform 찾기
    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}