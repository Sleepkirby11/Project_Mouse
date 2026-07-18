using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{
    #region Singleton

    private static PoolingManager _instance;
    public static PoolingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("[PoolingManager] PoolingManager가 씬에 존재하지 않습니다.");
            }

            return _instance;
        }
    }

    #endregion

    #region Types

    [System.Serializable]
    public class Pool
    {
        public string key;
        public GameObject prefab;
        public int initialSize = 10;
    }

    #endregion

    #region Settings & Variables

    [SerializeField] private List<Pool> pools;

    private Dictionary<string, Queue<GameObject>> poolDict;
    private Dictionary<string, GameObject> prefabDict;

    // 중복 Return 방지용
    private HashSet<GameObject> pooledObjects;

    // 대여된(활성화된) 오브젝트 추적용 (GameObject, pool key)
    private Dictionary<GameObject, string> activeObjects;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePools();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (activeObjects != null)
        {
            activeObjects.Clear();
        }
    }

    #endregion

    #region Initialization & Pool Creation

    private void InitializePools()
    {
        poolDict = new Dictionary<string, Queue<GameObject>>();
        prefabDict = new Dictionary<string, GameObject>();
        pooledObjects = new HashSet<GameObject>();
        activeObjects = new Dictionary<GameObject, string>();

        if (pools == null) 
        {
            return;
        }

        foreach (var pool in pools)
        {
            if (pool == null)
            {
                Debug.LogWarning("[PoolingManager] Null Pool 데이터가 존재합니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(pool.key))
            {
                Debug.LogWarning("[PoolingManager] Pool Key가 비어 있습니다.");
                continue;
            }

            if (pool.prefab == null)
            {
                Debug.LogWarning($"[PoolingManager] {pool.key}의 Prefab이 비어 있습니다.");
                continue;
            }

            CreatePool(pool.key, pool.prefab, pool.initialSize);
        }
    }

    private void CreatePool(string key, GameObject prefab, int size)
    {
        if (poolDict.ContainsKey(key))
        {
            Debug.LogError($"[PoolingManager] Duplicate Pool Key : {key}");
            return;
        }

        Queue<GameObject> queue = new Queue<GameObject>();

        prefabDict.Add(key, prefab);

        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            if (obj != null)
            {
                obj.SetActive(false);
                queue.Enqueue(obj);
                pooledObjects.Add(obj);
            }
        }

        poolDict.Add(key, queue);
    }

    #endregion

    #region Pool Get & Return Logic

    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (poolDict == null || !poolDict.ContainsKey(key))
        {
            Debug.LogError($"[PoolingManager] Pool key not found : {key}");
            return null;
        }

        Queue<GameObject> queue = poolDict[key];

        // 자동 확장 (풀이 비어있을 때 새로 인스턴스화)
        if (queue.Count == 0)
        {
            if (!prefabDict.TryGetValue(key, out GameObject prefab) || prefab == null)
            {
                Debug.LogError($"[PoolingManager] 원본 프리팹이 존재하지 않습니다. Key : {key}");
                return null;
            }

            GameObject newObj = Instantiate(prefab, transform);
            if (newObj != null)
            {
                newObj.SetActive(false);
                queue.Enqueue(newObj);
                pooledObjects.Add(newObj);
            }
        }

        GameObject obj = queue.Dequeue();
        if (obj != null)
        {
            pooledObjects.Remove(obj);

            // Pool 부모에서 분리
            obj.transform.SetParent(null, false);

            // 위치 설정
            obj.transform.SetPositionAndRotation(position, rotation);

            // 오브젝트 활성화
            obj.SetActive(true);

            // 대여 상태 추적 추가
            if (activeObjects != null)
            {
                activeObjects[obj] = key;
            }
        }

        return obj;
    }

    public void Return(string key, GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[PoolingManager] Null 오브젝트를 반납하려고 했습니다. Key : {key}");
            return;
        }

        if (poolDict == null || !poolDict.ContainsKey(key))
        {
            Debug.LogError($"[PoolingManager] 존재하지 않는 Pool Key : {key}");
            Destroy(obj);
            return;
        }

        // 중복 반납 방지
        if (pooledObjects.Contains(obj))
        {
            Debug.LogWarning($"[PoolingManager] 이미 반납된 오브젝트입니다. ({obj.name})");
            return;
        }

        obj.SetActive(false);

        // Pool 밑으로 자식화하여 정리
        obj.transform.SetParent(transform, false);

        poolDict[key].Enqueue(obj);
        pooledObjects.Add(obj);

        // 대여 상태 추적 해제
        if (activeObjects != null)
        {
            activeObjects.Remove(obj);
        }
    }

    private void CleanupNullActiveObjects()
    {
        if (activeObjects == null) return;

        List<GameObject> nullKeys = null;
        foreach (var kvp in activeObjects)
        {
            if (kvp.Key == null)
            {
                if (nullKeys == null) nullKeys = new List<GameObject>();
                nullKeys.Add(kvp.Key);
            }
        }

        if (nullKeys != null)
        {
            foreach (var key in nullKeys)
            {
                activeObjects.Remove(key);
            }
        }
    }

    public void ReturnActiveObjectsOfType<T>() where T : Component
    {
        if (activeObjects == null) return;

        // 먼저 파괴된 오브젝트 정리
        CleanupNullActiveObjects();

        List<GameObject> toReturn = new List<GameObject>();
        foreach (var kvp in activeObjects)
        {
            GameObject obj = kvp.Key;
            if (obj != null && obj.activeSelf && obj.GetComponent<T>() != null)
            {
                toReturn.Add(obj);
            }
        }

        foreach (var obj in toReturn)
        {
            if (obj != null)
            {
                if (activeObjects.TryGetValue(obj, out string key))
                {
                    Return(key, obj);
                }
            }
        }
    }

    #endregion
}