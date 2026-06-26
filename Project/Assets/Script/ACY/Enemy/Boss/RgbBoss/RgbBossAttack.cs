using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossAttackType
{
    Burst,
    FireGear,
    MushRoom,
    IceHammer,
    Hurricane,
    Bullet,
    Lightning
}

public class RgbBossAttack : MonoBehaviour, IHitReaction
{
    #region Inspector Fields
    [Header("회복량 설정")]
    [SerializeField] private int healAmount = 5;

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
    [SerializeField] private string redLightningPoolName = "RedLightning";
    [SerializeField] private string greenLightningPoolName = "GreenLightning";
    [SerializeField] private string blueLightningPoolName = "BlueLightning";
    [SerializeField] private int lightningCount = 3;
    [SerializeField] private float lightningInterval = 0.3f;
    [SerializeField] private float posRecordInterval = 0.5f;

    [Header("FirePoint")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform bulletFirePoint;

    [Header("얼음 망치 설정")]
    [SerializeField] private float hammerOffsetX = 0f;
    [SerializeField] private float hammerOffsetY = 0f;

    [Header("10%패턴 (블랙홀) 설정")]
    [SerializeField] private string blackHolePoolName = "BlackHole";
    [SerializeField] private Transform[] blackHoleSpawnPoints;
    [SerializeField] private float finalPhaseDuration = 10f;
    #endregion

    #region Private Fields
    private Vector3 lastPlayerPos;
    private Transform player;
    private EnemyStatus enemyStatus;
    private SpriteRenderer bossSpriteRenderer;
    private Animator animator;
    private RgbBossMove bossMove;
    private RgbColorCycle colorCycle;

    private BossAttackType lastAttackType = (BossAttackType)(-1);
    private bool isFinalPhaseActive = false;

    private bool isFinalPhaseTriggered = false;
    private bool isInvincible = false;
    private List<BlackHole> activeBlackHoles = new List<BlackHole>();

    private static readonly int ShootTrigger = Animator.StringToHash("Shoot");
    private static readonly int CastingTrigger = Animator.StringToHash("Casting");
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        bossSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        bossMove = GetComponent<RgbBossMove>();
        colorCycle = GetComponent<RgbColorCycle>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        StartCoroutine(RecordPlayerPosition());
        StartCoroutine(AttackRoutine());
    }

    private void Update()
    {
        // 체력이 10% 이하이고, 아직 발악 패턴에 진입하지 않았다면 실행
        if (!isFinalPhaseTriggered && enemyStatus.GetHPRatio() <= 0.1f)
        {
            isFinalPhaseTriggered = true;
            StartCoroutine(FinalPhaseRoutine());
        }
    }
    #endregion

    #region IHitReaction Implementation
    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (isInvincible)
        {
            return true;
        }
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage) { }
    #endregion

    #region Attack Loop & Selection
    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(2f);

        while (true)
        {
            if (isFinalPhaseActive)
            {
                yield return null;
                continue;
            }

            int attackCount = Random.Range(1, 3);

            for (int i = 0; i < attackCount; i++)
            {
                if (isFinalPhaseActive)
                    break;

                BossAttackType chosenAttack = GetRandomAttack();
                yield return StartCoroutine(ExecuteAttack(chosenAttack));
            }

            if (!isFinalPhaseActive)
            {
                if (colorCycle != null)
                {
                    colorCycle.ChangeElementRandom();
                }
                yield return new WaitForSeconds(1.5f);
            }
        }
    }

    private BossAttackType GetRandomAttack()
    {
        List<BossAttackType> availableAttacks = new List<BossAttackType>();

        switch (enemyStatus.CurrentElement)
        {
            case EnemyStatus.EnemyElement.Red:
                availableAttacks.Add(BossAttackType.Burst);
                availableAttacks.Add(BossAttackType.FireGear);
                break;
            case EnemyStatus.EnemyElement.Green:
                availableAttacks.Add(BossAttackType.MushRoom);
                break;
            case EnemyStatus.EnemyElement.Blue:
                availableAttacks.Add(BossAttackType.IceHammer);
                break;
        }

        availableAttacks.Add(BossAttackType.Hurricane);
        availableAttacks.Add(BossAttackType.Bullet);
        availableAttacks.Add(BossAttackType.Lightning);

        if (availableAttacks.Count > 1)
        {
            availableAttacks.Remove(lastAttackType);
        }

        BossAttackType chosen = availableAttacks[Random.Range(0, availableAttacks.Count)];
        lastAttackType = chosen;
        return chosen;
    }

    private IEnumerator ExecuteAttack(BossAttackType attack)
    {
        switch (attack)
        {
            case BossAttackType.Burst:
                SpawnBurstPattern();
                yield return new WaitForSeconds(3f);
                break;
            case BossAttackType.FireGear:
                SpawnFireGears();
                yield return new WaitForSeconds(3f);
                break;
            case BossAttackType.MushRoom:
                SpawnPoisonMushrooms();
                yield return new WaitForSeconds(3f);
                break;
            case BossAttackType.IceHammer:
                SpawnIceHammer();
                yield return new WaitForSeconds(3f);
                break;
            case BossAttackType.Hurricane:
                SpawnHurricane();
                yield return new WaitForSeconds(3f);
                break;
            case BossAttackType.Bullet:
                ShootBullet();
                yield return new WaitForSeconds(2f);
                break;
            case BossAttackType.Lightning:
                if (animator != null)
                {
                    animator.SetTrigger(CastingTrigger);
                }
                float waitTime = (lightningCount * lightningInterval) + 1.5f;
                yield return new WaitForSeconds(waitTime);
                break;
        }
    }
    #endregion

    #region Red Pattern (Burst, FireGear)
    private void SpawnBurstPattern()
    {
        if (animator != null)
        {
            animator.SetTrigger(CastingTrigger);
        }
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

    private void SpawnFireGears()
    {
        if (player == null) return;

        Vector3 spawnPos = new Vector3(
            player.position.x,
            player.position.y + spawnOffsetY,
            0f);

        PoolingManager.Instance.Get("FireGear", spawnPos, Quaternion.identity);
    }
    #endregion

    #region Green Pattern (Mushroom)
    private void SpawnPoisonMushrooms()
    {
        if (animator != null)
        {
            animator.SetTrigger(CastingTrigger);
        }
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
    #endregion

    #region Blue Pattern (IceHammer)
    public void SpawnIceHammer()
    {
        if (animator != null)
        {
            animator.SetTrigger(CastingTrigger);
        }

        if (player == null)
            return;

        Vector3 rayStart = player.position + new Vector3(hammerOffsetX, 5f, 0f);

        RaycastHit2D hit = Physics2D.Raycast(
            rayStart,
            Vector2.down,
            15f,
            groundLayer);

        if (hit.collider == null)
        {
            Debug.LogWarning("망치 생성 실패 : Ground를 찾지 못함");
            return;
        }

        Vector3 spawnPos = (Vector3)hit.point + new Vector3(0f, hammerOffsetY, 0f);

        PoolingManager.Instance.Get(
            "IceHammer",
            spawnPos,
            Quaternion.identity);
    }
    #endregion

    #region Common Pattern (Hurricane, Bullet, Lightning)
    private void SpawnHurricane()
    {
        if (animator != null)
        {
            animator.SetTrigger(CastingTrigger);
        }
        Vector3 spawnDirection = bossMove.isFacingRight ? Vector3.right : Vector3.left;

        Vector3 spawnPos = transform.position +
                      (spawnDirection * spawnOffsetX) +
                      (Vector3.up * spawnOffsetY);

        GameObject hurricaneObj = PoolingManager.Instance.Get(
            "Hurricane",
            spawnPos,
            Quaternion.identity);

        if (hurricaneObj != null)
        {
            Hurricane hurricane = hurricaneObj.GetComponent<Hurricane>();
            hurricane.Initialize(enemyStatus.CurrentElement, spawnDirection);

            if (enemyStatus.CurrentElement == EnemyStatus.EnemyElement.Green)
                hurricane.onHitPlayer = () => enemyStatus.Heal(healAmount);
            else
                hurricane.onHitPlayer = null;
        }
    }

    public void ShootBullet()
    {
        if (animator == null)
            return;

        animator.SetTrigger(ShootTrigger);
    }

    public void SpawnBossBullet()
    {
        if (player == null || bulletFirePoint == null)
            return;

        string targetPoolName = enemyStatus.CurrentElement switch
        {
            EnemyStatus.EnemyElement.Red => redBulletPoolName,
            EnemyStatus.EnemyElement.Green => greenBulletPoolName,
            EnemyStatus.EnemyElement.Blue => blueBulletPoolName,
            _ => ""
        };

        if (string.IsNullOrEmpty(targetPoolName))
            return;

        GameObject bulletObj = PoolingManager.Instance.Get(
            targetPoolName,
            bulletFirePoint.position,
            Quaternion.identity);

        if (bulletObj != null)
        {
            RgbBullet bullet = bulletObj.GetComponent<RgbBullet>();

            if (bullet != null)
            {
                bullet.Initialize(player, targetPoolName);

                if (enemyStatus.CurrentElement == EnemyStatus.EnemyElement.Green)
                    bullet.onHitPlayer = () => enemyStatus.Heal(healAmount);
                else
                    bullet.onHitPlayer = null;
            }
        }
    }

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
            Vector2 dir = (lastPlayerPos - firePoint.position).normalized;

            GameObject obj = PoolingManager.Instance.Get(
                poolName,
                firePoint.position,
                Quaternion.identity);

            if (obj != null)
            {
                LightningBolt bolt = obj.GetComponent<LightningBolt>();
                if (bolt != null)
                {
                    bolt.Initialize(poolName, dir, enemyStatus.CurrentElement);

                    if (enemyStatus.CurrentElement == EnemyStatus.EnemyElement.Green)
                        bolt.onHitPlayer = () => enemyStatus.Heal(healAmount);
                    else
                        bolt.onHitPlayer = null;
                }
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
    #endregion

    #region Final Phase (10% HP Pattern)
    private IEnumerator FinalPhaseRoutine()
    {
        isFinalPhaseActive = true;
        isInvincible = true;

        if (colorCycle != null) colorCycle.EnterFinalPhase();
        if (animator != null) animator.SetTrigger(CastingTrigger);

        SpawnBlackHoles();

        yield return new WaitForSeconds(finalPhaseDuration);

        foreach (BlackHole bh in activeBlackHoles)
        {
            if (bh != null) bh.ReturnToPool();
        }
        activeBlackHoles.Clear();

        isInvincible = false;
        if (colorCycle != null)
        {
            colorCycle.ExitFinalPhase();
        }
        isFinalPhaseActive = false;
    }

    private void SpawnBlackHoles()
    {
        activeBlackHoles.Clear();

        if (player == null || blackHoleSpawnPoints == null) return;

        foreach (Transform spawnPoint in blackHoleSpawnPoints)
        {
            GameObject bhObj = PoolingManager.Instance.Get(
                blackHolePoolName,
                spawnPoint.position,
                Quaternion.identity
            );

            if (bhObj != null)
            {
                BlackHole blackHole = bhObj.GetComponent<BlackHole>();
                if (blackHole != null)
                {
                    blackHole.Initialize(blackHolePoolName, player);
                    activeBlackHoles.Add(blackHole);
                }
            }
        }

        Debug.Log($"발악 패턴 시작! {blackHoleSpawnPoints.Length}개의 블랙홀 생성됨.");
    }
    #endregion
}