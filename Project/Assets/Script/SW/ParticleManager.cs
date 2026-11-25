using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ParticleManager : MonoBehaviour
{
    #region Singleton

    private static ParticleManager _instance;
    public static ParticleManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("[ParticleManager] ParticleManager가 씬에 존재하지 않습니다.");
            }

            return _instance;
        }
    }

    #endregion

    #region Types

    [System.Serializable]
    public class ParticlePool
    {
        public string key;
        public GameObject prefab;
        public int initialSize = 10;
    }

    /// <summary>
    /// 원샷 파티클 재생이 끝나면 자동으로 ParticleManager 풀에 반납하는 내부 훅 컴포넌트
    /// </summary>
    private class PooledParticleHook : MonoBehaviour
    {
        private ParticleManager owner;
        private string poolKey;
        private ParticleSystem rootParticle;

        public void Bind(ParticleManager manager, string key)
        {
            owner = manager;
            poolKey = key;

            if (rootParticle == null)
            {
                rootParticle = GetComponent<ParticleSystem>();
            }

            if (rootParticle != null && !rootParticle.main.loop)
            {
                var main = rootParticle.main;
                main.stopAction = ParticleSystemStopAction.Callback;
            }
        }

        public void PlayFresh()
        {
            if (rootParticle == null)
            {
                rootParticle = GetComponent<ParticleSystem>();
            }

            if (rootParticle != null)
            {
                rootParticle.Clear(true);
                rootParticle.Play(true);
            }
        }

        private void OnParticleSystemStopped()
        {
            if (owner != null && !string.IsNullOrEmpty(poolKey))
            {
                owner.Return(poolKey, gameObject);
            }
        }
    }

    #endregion

    #region Settings & Variables

    [SerializeField] private List<ParticlePool> pools;
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

            // 다른 매니저(예: PoolingManager)의 자식으로 붙어있더라도 독립 루트로 분리
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_instance != this)
        {
            return;
        }

        // 씬 전환 시 재생 중이던 파티클을 모두 안전하게 회수
        ReturnAllActiveObjects();

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
                Debug.LogWarning("[ParticleManager] Null Pool 데이터가 존재합니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(pool.key))
            {
                Debug.LogWarning("[ParticleManager] Pool Key가 비어 있습니다.");
                continue;
            }

            if (pool.prefab == null)
            {
                Debug.LogWarning($"[ParticleManager] {pool.key}의 Prefab이 비어 있습니다.");
                continue;
            }

            RegisterPool(pool.key, pool.prefab);
        }
    }

    private void RegisterPool(string key, GameObject prefab)
    {
        if (poolDict.ContainsKey(key))
        {
            Debug.LogError($"[ParticleManager] Duplicate Pool Key : {key}");
            return;
        }

        prefabDict.Add(key, prefab);
        poolDict.Add(key, new Queue<GameObject>());
    }

    private void StartPrewarm()
    {
        if (_instance != this || pools == null || poolDict == null)
        {
            return;
        }

        if (prewarmCoroutine != null)
        {
            StopCoroutine(prewarmCoroutine);
        }

        prewarmCoroutine = StartCoroutine(PrewarmPoolsRoutine());
    }

    private IEnumerator PrewarmPoolsRoutine()
    {
        int createdThisFrame = 0;
        int batchLimit = Mathf.Max(1, prewarmPerFrame);

        foreach (var pool in pools)
        {
            if (pool == null || string.IsNullOrWhiteSpace(pool.key) || pool.prefab == null)
            {
                continue;
            }

            if (!poolDict.TryGetValue(pool.key, out Queue<GameObject> queue))
            {
                continue;
            }

            while (queue.Count < pool.initialSize)
            {
                GameObject obj = CreatePooledInstance(pool.key, pool.prefab);
                if (obj != null)
                {
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

    private GameObject CreatePooledInstance(string key, GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, transform);
        if (obj == null)
        {
            return null;
        }

        obj.SetActive(false);

        PooledParticleHook hook = obj.GetComponent<PooledParticleHook>();
        if (hook == null)
        {
            hook = obj.AddComponent<PooledParticleHook>();
        }
        hook.Bind(this, key);

        return obj;
    }

    #endregion

    #region Pool Get & Return Logic

    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (poolDict == null || !poolDict.ContainsKey(key))
        {
            Debug.LogError($"[ParticleManager] Pool key not found : {key}");
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
                Debug.LogError($"[ParticleManager] 원본 프리팹이 존재하지 않습니다. Key : {key}");
                return null;
            }

            obj = CreatePooledInstance(key, prefab);
            if (obj == null)
            {
                return null;
            }
        }

        pooledObjects.Remove(obj);

        // 씬 루트로 분리하지 않고 항상 ParticleManager 본인의 자식으로 유지
        if (obj.transform.parent != transform)
        {
            obj.transform.SetParent(transform, false);
        }

        // 월드 위치/회전 설정
        obj.transform.SetPositionAndRotation(position, rotation);

        // 오브젝트 활성화 및 파티클 재시작
        obj.SetActive(true);

        PooledParticleHook hook = obj.GetComponent<PooledParticleHook>();
        if (hook != null)
        {
            hook.Bind(this, key);
            hook.PlayFresh();
        }

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
            Debug.LogWarning($"[ParticleManager] Null 오브젝트를 반납하려고 했습니다. Key : {key}");
            return;
        }

        if (poolDict == null || !poolDict.ContainsKey(key))
        {
            Debug.LogError($"[ParticleManager] 존재하지 않는 Pool Key : {key}");
            Destroy(obj);
            return;
        }

        // 중복 반납 방지
        if (pooledObjects.Contains(obj))
        {
            return;
        }

        obj.SetActive(false);

        // ParticleManager 본인의 자식 개체로 정리
        obj.transform.SetParent(transform, false);

        poolDict[key].Enqueue(obj);
        pooledObjects.Add(obj);

        // 대여 상태 추적 해제
        if (activeObjects != null)
        {
            activeObjects.Remove(obj);
        }
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
        if (activeObjects == null)
        {
            return;
        }

        List<GameObject> nullKeys = null;
        foreach (var kvp in activeObjects)
        {
            if (kvp.Key == null)
            {
                if (nullKeys == null)
                {
                    nullKeys = new List<GameObject>();
                }
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
        if (activeObjects == null)
        {
            return;
        }

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
            if (obj != null && activeObjects.TryGetValue(obj, out string key))
            {
                Return(key, obj);
            }
        }
    }

    #endregion
}
