using System.Collections;
using UnityEngine;

public class BlackHole : MonoBehaviour
{
    #region Inspector Fields
    [Header("스펙 설정")]
    [SerializeField] private float growSpeed = 1.5f; // 초당 커지는 크기 속도
    [SerializeField] private float maxScale = 5f;    // 최대 확장 스케일

    [Header("인력 설정")]
    [SerializeField] private float basePullForce = 15f; // 기본 인력 (당기는 힘)
    [SerializeField] private float pullRadius = 15f;    // 플레이어를 끌어당기기 시작할 반경

    [Header("폭발 이펙트 설정")]
    [SerializeField] private string explosionPoolKey = "Explosion";
    [SerializeField] private float explosionInterval = 0.2f;
    [SerializeField] private float explosionSpawnRadius = 1.5f;
    [SerializeField] private int baseExplosionCount = 1; // 1회당 생성되는 기본 폭발물 개수
    #endregion

    #region Private Fields
    private Transform player;
    private Rigidbody2D playerRb;
    private string poolKey;
    private Coroutine explosionRoutine;
    #endregion

    #region Initialization
    public void Initialize(string key, Transform targetPlayer)
    {
        poolKey = key;
        player = targetPlayer;

        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }

        // 오브젝트 풀에서 꺼낼 때 크기를 1로 초기화
        transform.localScale = Vector3.one;
        explosionRoutine = StartCoroutine(SpawnExplosionsRoutine());
    }
    #endregion

    #region Main Physics & Update Loop
    private void Update()
    {
        // 1. 블랙홀 크기 확장
        if (transform.localScale.x < maxScale)
        {
            float scaleIncrease = growSpeed * Time.deltaTime;
            transform.localScale += new Vector3(scaleIncrease, scaleIncrease, 0f);
        }
    }

    private void FixedUpdate()
    {
        // 2. 플레이어 끌어당기기 (FixedUpdate 기반 물리 연산)
        PullPlayer();
    }

    private void PullPlayer()
    {
        if (player == null || playerRb == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // 플레이어가 블랙홀 인력 반경 안에 들어왔을 때만 힘 작용
        if (distance <= pullRadius)
        {
            // 블랙홀 스케일(Scale)에 비례하여 끌어당기는 힘 강화
            float currentForce = basePullForce * transform.localScale.x;

            // 플레이어에서 블랙홀 방향으로의 벡터 계산
            Vector2 direction = (transform.position - player.position).normalized;

            // 플레이어 리지드바디에 끌어당기는 물리 힘 가하기
            playerRb.AddForce(direction * currentForce * Time.fixedDeltaTime, ForceMode2D.Impulse);
        }
    }
    #endregion

    #region Explosion & Disposing Routines
    private IEnumerator SpawnExplosionsRoutine()
    {
        while (true)
        {
            float currentScale = transform.localScale.x;

            // 스케일에 비례하여 실제 폭발물 스폰 반경 결정
            float currentRadius = explosionSpawnRadius * currentScale;

            // 스케일에 비례하여 소환 개수 결정 (크기가 3배면 기본의 3배만큼 소환)
            int spawnCount = Mathf.FloorToInt(baseExplosionCount * currentScale);
            if (spawnCount < 1) spawnCount = 1;

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 randomPos = (Vector2)transform.position + Random.insideUnitCircle * currentRadius;
                PoolingManager.Instance.Get(explosionPoolKey, randomPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(explosionInterval);
        }
    }

    public void ReturnToPool()
    {
        if (explosionRoutine != null) StopCoroutine(explosionRoutine);
        PoolingManager.Instance.Return(poolKey, gameObject);
    }
    #endregion
}
