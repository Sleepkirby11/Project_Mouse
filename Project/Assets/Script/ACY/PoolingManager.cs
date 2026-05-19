using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{
    public static PoolingManager Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string key;           // 풀 식별 키 ("RedBossArrow", "Arrow" 등)
        public GameObject prefab;    // 프리팹
        public int initialSize = 10; // 초기 생성 개수
    }

    [SerializeField] private List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDict;
    private Dictionary<string, GameObject> prefabDict;  // 동적 확장용

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
        InitializePools();
    }

    private void InitializePools()
    {
        poolDict = new Dictionary<string, Queue<GameObject>>();
        prefabDict = new Dictionary<string, GameObject>();

        foreach (var pool in pools)
        {
            CreatePool(pool.key, pool.prefab, pool.initialSize);
        }
    }

    private void CreatePool(string key, GameObject prefab, int size)
    {
        Queue<GameObject> queue = new Queue<GameObject>();
        prefabDict[key] = prefab;

        for (int i = 0; i < size; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            queue.Enqueue(obj);
        }

        poolDict[key] = queue;
    }

    // 꺼내기
    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (!poolDict.ContainsKey(key))
        {
            Debug.LogWarning($"[ObjectPool] '{key}' 풀이 없습니다.");
            return null;
        }

        Queue<GameObject> queue = poolDict[key];

        // 풀이 비어있으면 자동 확장
        if (queue.Count == 0)
        {
            Debug.Log($"[ObjectPool] '{key}' 풀 확장");
            GameObject newObj = Instantiate(prefabDict[key], transform);
            newObj.SetActive(false);
            queue.Enqueue(newObj);
        }

        GameObject obj = queue.Dequeue();
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        return obj;
    }

    // 반납
    public void Return(string key, GameObject obj)
    {
        if (!poolDict.ContainsKey(key))
        {
            Destroy(obj);
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        poolDict[key].Enqueue(obj);
    }
}