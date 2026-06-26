using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class RgbBossAttack : MonoBehaviour
{
    [Header("FireGear Spawn")]
    [SerializeField] private float spawnOffsetY = -1f;
    [SerializeField] private float redAttackInterval = 3f;

    [Header("Hurricane 설정")]
    [SerializeField] private float spawnOffsetX = 1.5f; // 보스 앞에 생성되는 거리

    [Header("속성별 탄환 풀 이름 설정")]
    [SerializeField] private string redBulletPoolName = "RedBullet";
    [SerializeField] private string greenBulletPoolName = "GreenBullet";
    [SerializeField] private string blueBulletPoolName = "BlueBullet";

    [Header("Burst 패턴 설정")]
    [SerializeField] private string redBurstPoolName = "Burst";
    [SerializeField] private float burstSpawnRadius = 3.5f;

    private Transform player;
    private EnemyStatus enemyStatus;
    private SpriteRenderer bossSpriteRenderer;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        bossSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");

        if (playerObj != null)
            player = playerObj.transform;

        //StartCoroutine(AttackRoutine());
    }
    private void Update() // 테스트용 
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            SpawnBurstPattern();
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
        // TODO : Green 전용 패턴

        yield return new WaitForSeconds(3f);
    }

    private IEnumerator BlueAttack()
    {
        Vector3 spawnPos =
            player.position + Vector3.up * 5f;

        PoolingManager.Instance.Get(
            "IceHammer",
            spawnPos,
            Quaternion.identity);

        yield return new WaitForSeconds(5f);
    }
    private void SpawnHurricane()
    {
        // flipX가 true이면 왼쪽, false이면 오른쪽
        Vector3 spawnDirection = bossSpriteRenderer.flipX ? Vector3.left : Vector3.right;

        Vector3 spawnPos = transform.position +
                      (spawnDirection * spawnOffsetX) +
                      (Vector3.up * spawnOffsetY);

        GameObject hurricaneObj = PoolingManager.Instance.Get(
            "Hurricane",
            transform.position,
            Quaternion.identity);

        if (hurricaneObj != null)
        {
            Hurricane hurricane = hurricaneObj.GetComponent<Hurricane>();
            if (hurricane != null)
            {
                hurricane.Initialize(enemyStatus.CurrentElement, spawnDirection);
            }
        }
    }
    private void SpawnFireGears()
    {
        if (player == null)
            return;

        Vector3 spawnPos = new Vector3(
            player.position.x,
            player.position.y + spawnOffsetY,
            0f);

        PoolingManager.Instance.Get(
            "FireGear",
            spawnPos,
            Quaternion.identity);
    }
    private void SpawnBossBullet()
    {
        if (player == null) return;

        // 1. 보스 속성에 맞는 풀 이름 선택
        string targetPoolName = "";
        switch (enemyStatus.CurrentElement)
        {
            case EnemyStatus.EnemyElement.Red:
                targetPoolName = redBulletPoolName;
                break;
            case EnemyStatus.EnemyElement.Green:
                targetPoolName = greenBulletPoolName;
                break;
            case EnemyStatus.EnemyElement.Blue:
                targetPoolName = blueBulletPoolName;
                break;
        }
        if (string.IsNullOrEmpty(targetPoolName)) return;

        // 2. 해당 속성 탄환 생성
        GameObject bulletObj = PoolingManager.Instance.Get(
            targetPoolName,
            transform.position,
            Quaternion.identity
        );

        if (bulletObj != null)
        {
            RgbBullet bullet = bulletObj.GetComponent<RgbBullet>();
            if (bullet != null)
            {
                // ★ 이제 색상 지정을 보스가 해줄 필요 없이, 플레이어 트랜스폼과 풀 이름만 넘겨서 발사시킵니다.
                bullet.Initialize(player, targetPoolName);
            }
        }
    }
    private void SpawnBurstPattern()
    {
        if (player == null) return;

        // 플레이어 주변에 무작위로 3개 배치
        for (int i = 0; i < 3; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * burstSpawnRadius;
            Vector3 spawnPos = player.position + new Vector3(randomCircle.x, randomCircle.y, 0f);

            GameObject burstObj = PoolingManager.Instance.Get(redBurstPoolName, spawnPos, Quaternion.identity);

            if (burstObj != null)
            {
                Burst burst = burstObj.GetComponent<Burst>();
                if (burst != null)
                {
                    // 버스트 초기화 수행 (풀 이름 전달)
                    burst.InitializeBurst(redBurstPoolName);
                }
            }
        }
    }
}