using System.Collections;
using UnityEngine;

public class BlackHole : MonoBehaviour
{
    [Header("성장 설정")]
    [SerializeField] private float growSpeed = 1.5f; // 초당 커지는 속도
    [SerializeField] private float maxScale = 5f;    // 최대 크기

    [Header("흡입력 설정")]
    [SerializeField] private float basePullForce = 15f; // 기본 흡입력
    [SerializeField] private float pullRadius = 15f;    // 흡입 범위

    [Header("폭발 이펙트 설정")]
    [SerializeField] private string explosionPoolName = "Explosion";
    [SerializeField] private float explosionInterval = 0.2f;
    [SerializeField] private float explosionSpawnRadius = 1.5f;
    [SerializeField] private int baseExplosionCount = 1; // 스케일 1일 때 기본 폭발 개수

    private Transform player;
    private Rigidbody2D playerRb;
    private string poolKey;
    private Coroutine explosionRoutine;

    public void Initialize(string key, Transform targetPlayer)
    {
        poolKey = key;
        player = targetPlayer;

        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }

        // 풀링에서 꺼낼 때마다 크기를 1로 초기화
        transform.localScale = Vector3.one;
        explosionRoutine = StartCoroutine(SpawnExplosionsRoutine());
    }

    private void Update()
    {
        // 1. 점진적으로 크기 증가
        if (transform.localScale.x < maxScale)
        {
            float scaleIncrease = growSpeed * Time.deltaTime;
            transform.localScale += new Vector3(scaleIncrease, scaleIncrease, 0f);
        }
    }

    private void FixedUpdate()
    {
        // 2. 물리적 흡입 로직 (FixedUpdate 권장)
        PullPlayer();
    }

    private void PullPlayer()
    {
        if (player == null || playerRb == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // 플레이어가 범위 안에 있을 때만 당기기
        if (distance <= pullRadius)
        {
            // 현재 블랙홀의 크기(Scale x)에 비례하여 흡입력 증가
            float currentForce = basePullForce * transform.localScale.x;

            // 블랙홀을 향하는 방향 벡터
            Vector2 direction = (transform.position - player.position).normalized;

            // 플레이어에게 지속적인 힘 가하기
            playerRb.AddForce(direction * currentForce * Time.fixedDeltaTime, ForceMode2D.Impulse);
        }
    }

    private IEnumerator SpawnExplosionsRoutine()
    {
        while (true)
        {
            float currentScale = transform.localScale.x;

            // 1. 크기에 비례해서 생성 범위 증가
            float currentRadius = explosionSpawnRadius * currentScale;

            // 2. 크기에 비례해서 생성 개수 증가 (예: 스케일이 3배면 한 번에 3개 스폰)
            int spawnCount = Mathf.FloorToInt(baseExplosionCount * currentScale);
            if (spawnCount < 1) spawnCount = 1;

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 randomPos = (Vector2)transform.position + Random.insideUnitCircle * currentRadius;
                PoolingManager.Instance.Get(explosionPoolName, randomPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(explosionInterval);
        }
    }

    public void ReturnToPool()
    {
        if (explosionRoutine != null) StopCoroutine(explosionRoutine);
        PoolingManager.Instance.Return(poolKey, gameObject);
    }
}
