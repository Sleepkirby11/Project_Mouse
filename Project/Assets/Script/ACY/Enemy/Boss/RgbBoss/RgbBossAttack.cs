using System.Collections;
using UnityEngine;

public class RgbBossAttack : MonoBehaviour
{
    [Header("FireGear Spawn")]
    [SerializeField] private float spawnOffsetY = -1f;
    [SerializeField] private float redAttackInterval = 3f;

    [Header("Hurricane 설정")]
    [SerializeField] private float spawnOffsetX = 1.5f;

    [Header("속성별 탄환 풀 이름 설정")]
    [SerializeField] private string redBulletPoolName = "RedBullet";
    [SerializeField] private string greenBulletPoolName = "GreenBullet";
    [SerializeField] private string blueBulletPoolName = "BlueBullet";

    [Header("Burst 패턴 설정")]
    [SerializeField] private string redBurstPoolName = "Burst";
    [SerializeField] private float burstSpawnRadius = 3.5f;

    [Header("독버섯 패턴 설정 (Green)")]
    [SerializeField] private string mushroomPoolName = "Mushroom";
    [SerializeField] private int mushroomSpawnCount = 3;
    [SerializeField] private float spawnRangeX = 5f;
    [SerializeField] private float mushroomOffsetY = 0f;
    [SerializeField] private LayerMask groundLayer;

    [Header("번개 패턴 설정")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private string redLightningPoolName = "RedLightning";
    [SerializeField] private string greenLightningPoolName = "GreenLightning";
    [SerializeField] private string blueLightningPoolName = "BlueLightning";
    [SerializeField] private int lightningCount = 3;
    [SerializeField] private float lightningInterval = 0.3f;
    [SerializeField] private float posRecordInterval = 0.5f; // 숫자가 클수록 피하기 쉬워짐

    private Vector3 lastPlayerPos;
    private Transform player;
    private EnemyStatus enemyStatus;
    private SpriteRenderer bossSpriteRenderer;
    private Animator animator;


    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        bossSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        StartCoroutine(RecordPlayerPosition());
        //StartCoroutine(AttackRoutine());
    }

    private void Update()
    {
            if (Input.GetKeyDown(KeyCode.L))
            {
                animator.SetTrigger("LightningAttack"); // 애니메이션 + Event로 번개 자동 발사
            }
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            switch (enemyStatus.CurrentElement)
            {
                case EnemyStatus.EnemyElement.Red:
                    yield return StartCoroutine(RedAttack());
                    break;
                case EnemyStatus.EnemyElement.Green:
                    yield return StartCoroutine(GreenAttack());
                    break;
                case EnemyStatus.EnemyElement.Blue:
                    yield return StartCoroutine(BlueAttack());
                    break;
                default:
                    yield return null;
                    break;
            }
        }
    }

    private IEnumerator RedAttack()
    {
        SpawnFireGears();
        yield return new WaitForSeconds(redAttackInterval);
    }

    private IEnumerator GreenAttack()
    {
        SpawnPoisonMushrooms();
        yield return new WaitForSeconds(3f);
    }

    private IEnumerator BlueAttack()
    {
        yield return new WaitForSeconds(5f);
    }

    // Animation Event에서 호출
    public void SpawnLightning()
    {
        StartCoroutine(SpawnLightningBurst());
    }

    private string GetLightningPoolName()
    {
        return enemyStatus.CurrentElement switch
        {
            EnemyStatus.EnemyElement.Red => redLightningPoolName,
            EnemyStatus.EnemyElement.Green => greenLightningPoolName,
            EnemyStatus.EnemyElement.Blue => blueLightningPoolName,
            _ => redLightningPoolName
        };
    }

    private IEnumerator SpawnLightningBurst()
    {
        if (firePoint == null) yield break;

        string poolName = GetLightningPoolName();

        for (int i = 0; i < lightningCount; i++)
        {
            Vector2 dir = (lastPlayerPos - firePoint.position).normalized; // 이전 위치로 발사

            GameObject obj = PoolingManager.Instance.Get(
                poolName,
                firePoint.position,
                Quaternion.identity);

            if (obj != null)
            {
                LightningBolt bolt = obj.GetComponent<LightningBolt>();
                bolt?.Initialize(poolName, dir);
            }

            yield return new WaitForSeconds(lightningInterval);
        }
    }
    private IEnumerator RecordPlayerPosition()
    {
        while (true)
        {
            lastPlayerPos = player.position;
            yield return new WaitForSeconds(posRecordInterval);
        }
    }

    // ─── 독버섯 ─────────────────────────────────────────

    private void SpawnPoisonMushrooms()
    {
        if (player == null) return;

        int successfulSpawns = 0;
        int attempts = 0;
        int maxAttempts = mushroomSpawnCount * 3;

        while (successfulSpawns < mushroomSpawnCount && attempts < maxAttempts)
        {
            attempts++;

            float randomX = Random.Range(-spawnRangeX, spawnRangeX);
            Vector3 rayStartPos = player.position + new Vector3(randomX, 5f, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayStartPos, Vector2.down, 15f, groundLayer);

            if (hit.collider != null)
            {
                Vector3 spawnPos = (Vector3)hit.point + new Vector3(0f, mushroomOffsetY, 0f);
                PoolingManager.Instance.Get(mushroomPoolName, spawnPos, Quaternion.identity);
                successfulSpawns++;
            }
        }

        Debug.Log($"독버섯 생성 패턴 실행: 플레이어 주변 바닥에 {successfulSpawns}개 스폰됨.");
    }

    // ─── Hurricane ───────────────────────────────────────

    private void SpawnHurricane()
    {
        Vector3 spawnDirection = bossSpriteRenderer.flipX ? Vector3.left : Vector3.right;

        // spawnOffsetX 실제로 적용
        Vector3 spawnPos = transform.position +
                      (spawnDirection * spawnOffsetX) +
                      (Vector3.up * spawnOffsetY);

        GameObject hurricaneObj = PoolingManager.Instance.Get(
            "Hurricane",
            spawnPos,          // transform.position 대신 spawnPos 사용
            Quaternion.identity);

        if (hurricaneObj != null)
        {
            Hurricane hurricane = hurricaneObj.GetComponent<Hurricane>();
            hurricane?.Initialize(enemyStatus.CurrentElement, spawnDirection);
        }
    }

    // ─── FireGear ────────────────────────────────────────

    private void SpawnFireGears()
    {
        if (player == null) return;

        Vector3 spawnPos = new Vector3(
            player.position.x,
            player.position.y + spawnOffsetY,
            0f);

        PoolingManager.Instance.Get("FireGear", spawnPos, Quaternion.identity);
    }

    // ─── 속성 탄환 ───────────────────────────────────────

    private void SpawnBossBullet()
    {
        if (player == null) return;

        string targetPoolName = enemyStatus.CurrentElement switch
        {
            EnemyStatus.EnemyElement.Red => redBulletPoolName,
            EnemyStatus.EnemyElement.Green => greenBulletPoolName,
            EnemyStatus.EnemyElement.Blue => blueBulletPoolName,
            _ => ""
        };

        if (string.IsNullOrEmpty(targetPoolName)) return;

        GameObject bulletObj = PoolingManager.Instance.Get(
            targetPoolName,
            transform.position,
            Quaternion.identity);

        if (bulletObj != null)
        {
            RgbBullet bullet = bulletObj.GetComponent<RgbBullet>();
            bullet?.Initialize(player, targetPoolName);
        }
    }

    // ─── Burst ───────────────────────────────────────────

    private void SpawnBurstPattern()
    {
        if (player == null) return;

        for (int i = 0; i < 3; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * burstSpawnRadius;
            Vector3 spawnPos = player.position + new Vector3(randomCircle.x, randomCircle.y, 0f);

            GameObject burstObj = PoolingManager.Instance.Get(
                redBurstPoolName,
                spawnPos,
                Quaternion.identity);

            if (burstObj != null)
            {
                Burst burst = burstObj.GetComponent<Burst>();
                burst?.InitializeBurst(redBurstPoolName);
            }
        }
    }
}