using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{
    #region Singleton

    private static PoolingManager _instance;
    public static bool HasInstance => _instance != null;
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
    [SerializeField] private int prewarmPerFrame = 8;

    private Dictionary<string, Queue<GameObject>> poolDict;
    private Dictionary<string, GameObject> prefabDict;
    private Coroutine prewarmCoroutine;

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

    private void Start()
    {
        StartPrewarm();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (activeObjects != null)
        {
            activeObjects.Clear();
        }

        if (pooledObjects != null)
        {
            pooledObjects.RemoveWhere(obj => obj == null);
        }

        StartPrewarm();
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

            RegisterPool(pool.key, pool.prefab);
        }
    }

    private void RegisterPool(string key, GameObject prefab)
    {
        if (poolDict.ContainsKey(key))
        {
            Debug.LogError($"[PoolingManager] Duplicate Pool Key : {key}");
            return;
        }

        prefabDict.Add(key, prefab);
        poolDict.Add(key, new Queue<GameObject>());
    }

    private void StartPrewarm()
    {
        if (_instance != this || pools == null || poolDict == null)
            return;

        if (prewarmCoroutine != null)
        {
            StopCoroutine(prewarmCoroutine);
        }
        prewarmCoroutine = StartCoroutine(PrewarmPoolsRoutine());
    }

    private IEnumerator PrewarmPoolsRoutine()
    {
        // 씬 전환 및 첫 프레임 안정화 직후부터 프레임당 소량만 분산 생성해 씬 이동 렉 완화
        yield return null;
        while (GameManager.instance != null && GameManager.instance.IsTransitioning)
        {
            yield return null;
        }

        int createdThisFrame = 0;
        int batchLimit = Mathf.Min(Mathf.Max(1, prewarmPerFrame), 2);

        foreach (var pool in pools)
        {
            if (pool == null || string.IsNullOrWhiteSpace(pool.key) || pool.prefab == null)
                continue;

            if (!poolDict.TryGetValue(pool.key, out Queue<GameObject> queue))
                continue;

            while (queue.Count < pool.initialSize)
            {
                GameObject obj = Instantiate(pool.prefab, transform);
                if (obj != null)
                {
                    obj.SetActive(false);
                    queue.Enqueue(obj);
                    pooledObjects.Add(obj);
                }

                createdThisFrame++;
                if (createdThisFrame >= batchLimit)
                {
                    createdThisFrame = 0;
                    yield return null;
                }
            }
        }

        prewarmCoroutine = null;
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
        GameObject obj = null;

        // 파괴된 null 오브젝트가 큐에 섞여있더라도 건너뛰고 유효한 오브젝트 획득
        while (queue.Count > 0 && obj == null)
        {
            obj = queue.Dequeue();
        }

        // 자동 확장 (풀이 비어있을 때 새로 인스턴스화)
        if (obj == null)
        {
            if (!prefabDict.TryGetValue(key, out GameObject prefab) || prefab == null)
            {
                Debug.LogError($"[PoolingManager] 원본 프리팹이 존재하지 않습니다. Key : {key}");
                return null;
            }

            obj = Instantiate(prefab, transform);
            if (obj == null)
            {
                return null;
            }
            obj.SetActive(false);
        }

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
            return;
        }

        pooledObjects.Add(obj);
        poolDict[key].Enqueue(obj);

        // 대여 상태 추적 해제
        if (activeObjects != null)
        {
            activeObjects.Remove(obj);
        }

        // Pool 밑으로 자식화하여 씬 전환 파괴 방지 및 비활성화
        obj.transform.SetParent(transform, false);
        obj.SetActive(false);
    }

    public void ReturnAllActiveObjects()
    {
        if (activeObjects == null || activeObjects.Count == 0)
        {
            return;
        }

        CleanupNullActiveObjects();

        List<KeyValuePair<GameObject, string>> snapshot = new List<KeyValuePair<GameObject, string>>(activeObjects);
        foreach (var kvp in snapshot)
        {
            if (kvp.Key != null)
            {
                Return(kvp.Value, kvp.Key);
            }
        }

        activeObjects.Clear();
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