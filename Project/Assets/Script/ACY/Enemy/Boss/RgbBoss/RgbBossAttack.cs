using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
 * 보스는 현재 자신의 상태에 따라 외관이 변함
Red Blue Green None 총 4가지로 나뉘며
현재 상태에 따라 사용하는 공격 패턴이 달라짐
Red 전용 패턴 
1. 불 톱니바퀴 : 플레이어 밑에서 톱니가 나타나 위로 이동
히트 시 플레이어는 속박당하며 톱니바퀴에 끌려가며 지속대미지
2. 폭발 : 플레이어 주변 범위에 3개의 폭발을 일으킴

Blue 전용 패턴
1. 얼음망치 : 플레이어 왼쪽에 얼음 망치를 생성
얼음 망치가 땅에 닿을 시 바닥에 얼음 기둥 생성
만약 땅에 닿기 전에 "그린"으로 공격 시 망치는 사라짐

Green 전용 패턴
1. 독버섯 : 플레이어 주변 땅에 3개의 독버섯을 생성
독버섯은 일정 시간이 경과하면 터짐
터질 시 바닥에 독장판 생성
만약 터지기 전에 "레드"상태로 공격 시 버섯은 사라짐

공용 패턴 (이 패턴은 모든 상태에서 사용되며 상태에 따라 효과나 스프라이트가 바뀜)
1. 탄환 : 플레이어에게 일직선으로 이동하는 탄환 발사
탄환 이동 후 명중하지 않을 시 잠깐의 경직을 가진 후 플레이어에게 다시 이동함
총 5회에 걸쳐 이동하며 플레이어가 강점인 색으로 공격 시 탄환은 사라짐
색 별 효과: Green 체력 회복, Blue 스턴, Red 강한 대미지

2. 번개 : 보스는  플레이어 방향으로 번개를 쏨
번개는 플레이어의 이전 위치를 추적하여 2회 연속으로 공격함
색 별 효과: Green 회복, Blue 스턴, Red 강한 대미지

3. 소용돌이 : 보스 앞에 소용돌이를 소환하여 플레이어 쪽으로 발사
색 별 효과: Green 회복, Blue 위로 날려보냄, Red 강한 대미지

10% 패턴
상태가 None 으로 바뀌며 마젠타 색으로 변하게 됨
보스는 무적 상태가 되며
미리 지정해두었던 위치에 블랙홀이 생기고 플레이어를 빨아들임
블랙홀은 점점 크기가 커지며 흡입력도 강해짐
블랙홀 근처와 내부에는 폭발 파티클이 생기며 플레이어가 닿으면 대미지를 입음
일정 시간이 끝나면 패턴 종료 후 기존 rgb 사이클로 회귀
*/

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

    [Header("Hurricane 설정")]
    [SerializeField] private float spawnOffsetX = 1.5f;
    [SerializeField] private float hurricaneOffsetY = 0f;

    [Header("속성별 탄환 풀 키 설정")]
    [SerializeField] private string redBulletPoolKey = "RedBullet";
    [SerializeField] private string greenBulletPoolKey = "GreenBullet";
    [SerializeField] private string blueBulletPoolKey = "BlueBullet";

    [Header("Burst 패턴 설정")]
    [SerializeField] private string redBurstPoolKey = "Burst";
    [SerializeField] private float burstSpawnRadius = 3.5f;

    [Header("독버섯 패턴 설정 (Green)")]
    [SerializeField] private string mushroomPoolKey = "Mushroom";
    [SerializeField] private int mushroomSpawnCount = 3;
    [SerializeField] private float spawnRangeX = 5f;
    [SerializeField] private float mushroomOffsetY = 0f;
    [SerializeField] private LayerMask groundLayer;

    [Header("번개 패턴 설정")]
    [SerializeField] private string redLightningPoolKey = "RedLightning";
    [SerializeField] private string greenLightningPoolKey = "GreenLightning";
    [SerializeField] private string blueLightningPoolKey = "BlueLightning";
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
    [SerializeField] private string blackHolePoolKey = "BlackHole";
    [SerializeField] private Transform[] blackHoleSpawnPoints;
    [SerializeField] private float finalPhaseDuration = 10f;
    [SerializeField] private float finalPhaseRestDuration = 2f;

    [Header("VFX 풀 키 설정")]
    [SerializeField] private string hurricanePoolKey = "Hurricane";
    [SerializeField] private string fireGearPoolKey = "FireGear";
    [SerializeField] private string iceHammerPoolKey = "IceHammer";
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
    private static readonly int LightningTrigger = Animator.StringToHash("LightningAttack");
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
                    animator.SetTrigger(LightningTrigger);
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
        if (player == null) return;

        for (int i = 0; i < 3; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * burstSpawnRadius;
            Vector3 spawnPos = player.position + new Vector3(randomCircle.x, randomCircle.y, 0f);

            GameObject burstObj = PoolingManager.Instance.Get(
                redBurstPoolKey,
                spawnPos,
                Quaternion.identity);

            if (burstObj != null)
            {
                Burst burst = burstObj.GetComponent<Burst>();
                burst?.InitializeBurst(redBurstPoolKey);
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

        PoolingManager.Instance.Get(fireGearPoolKey, spawnPos, Quaternion.identity);
    }
    #endregion

    #region Green Pattern (Mushroom)
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
                PoolingManager.Instance.Get(mushroomPoolKey, spawnPos, Quaternion.identity);
                successfulSpawns++;
            }
        }
    }
    #endregion

    #region Blue Pattern (IceHammer)
    public void SpawnIceHammer()
    {
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
            return;
        }

        Vector3 spawnPos = (Vector3)hit.point + new Vector3(0f, hammerOffsetY, 0f);

        PoolingManager.Instance.Get(
            iceHammerPoolKey,
            spawnPos,
            Quaternion.identity);
    }
    #endregion

    #region Common Pattern (Hurricane, Bullet, Lightning)
    private void SpawnHurricane()
    {
        Vector3 spawnDirection = bossMove.isFacingRight ? Vector3.right : Vector3.left;

        Vector3 rayStart = transform.position + (spawnDirection * spawnOffsetX) + Vector3.up * 5f;
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 50f, groundLayer);

        Vector3 spawnPos;
        if (hit.collider != null)
        {
            spawnPos = (Vector3)hit.point + new Vector3(0f, hurricaneOffsetY, 0f);
        }
        else
        {
            spawnPos = transform.position +
                          (spawnDirection * spawnOffsetX) +
                          (Vector3.up * spawnOffsetY);
        }

        GameObject hurricaneObj = PoolingManager.Instance.Get(
            hurricanePoolKey,
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

        string targetPoolKey = enemyStatus.CurrentElement switch
        {
            EnemyStatus.EnemyElement.Red => redBulletPoolKey,
            EnemyStatus.EnemyElement.Green => greenBulletPoolKey,
            EnemyStatus.EnemyElement.Blue => blueBulletPoolKey,
            _ => ""
        };

        if (string.IsNullOrEmpty(targetPoolKey))
            return;

        GameObject bulletObj = PoolingManager.Instance.Get(
            targetPoolKey,
            bulletFirePoint.position,
            Quaternion.identity);

        if (bulletObj != null)
        {
            RgbBullet bullet = bulletObj.GetComponent<RgbBullet>();

            if (bullet != null)
            {
                bullet.Initialize(player, targetPoolKey);

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

    private string GetLightningPoolKey()
    {
        return enemyStatus.CurrentElement switch
        {
            EnemyStatus.EnemyElement.Red => redLightningPoolKey,
            EnemyStatus.EnemyElement.Green => greenLightningPoolKey,
            EnemyStatus.EnemyElement.Blue => blueLightningPoolKey,
            _ => redLightningPoolKey
        };
    }

    private IEnumerator SpawnLightningBurst()
    {
        if (firePoint == null) yield break;

        string poolKey = GetLightningPoolKey();

        for (int i = 0; i < lightningCount; i++)
        {
            Vector2 dir = (lastPlayerPos - firePoint.position).normalized;

            GameObject obj = PoolingManager.Instance.Get(
                poolKey,
                firePoint.position,
                Quaternion.identity);

            if (obj != null)
            {
                LightningBolt bolt = obj.GetComponent<LightningBolt>();
                if (bolt != null)
                {
                    bolt.Initialize(poolKey, dir, enemyStatus.CurrentElement);

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

        yield return new WaitForSeconds(finalPhaseRestDuration);

        isFinalPhaseActive = false;
    }

    private void SpawnBlackHoles()
    {
        activeBlackHoles.Clear();

        if (player == null || blackHoleSpawnPoints == null) return;

        foreach (Transform spawnPoint in blackHoleSpawnPoints)
        {
            GameObject bhObj = PoolingManager.Instance.Get(
                blackHolePoolKey,
                spawnPoint.position,
                Quaternion.identity
            );

            if (bhObj != null)
            {
                BlackHole blackHole = bhObj.GetComponent<BlackHole>();
                if (blackHole != null)
                {
                    blackHole.Initialize(blackHolePoolKey, player);
                    activeBlackHoles.Add(blackHole);
                }
            }
        }
    }
    #endregion
}