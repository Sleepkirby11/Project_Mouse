using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 공격패턴1 : 경고선 후 새를 세마리 소환하여 직선으로 돌진 
 * 공격패턴2 : 개구리를 플레이어 위에 소환하여 공격
 * 공격패턴3 : 바닥에 플레이어를 속박하는 꽃 함정을 생성
 * 방어패턴1 : 플레이어가 자신에게 근접할 시 바람을 일으켜 플레이어를 강하게 밀어냄
 * 방어패턴2 : 체력이 50% 미만이 되면 정령을 3체 소환(나비) 후 배리어 생성
 * 배리어가 있는 동안 보스는 체력 회복 + 무적
 * 정령을 모두 삭제해야 배리어가 사라짐
 * 공격패턴3 : 체력이 10% 미만이 되면 소환수(KillerPlant)를 소환하며 자신은 투명화상태 
 */

public class GreenBossAttack : MonoBehaviour, IHitReaction
{
    #region Settings & Variables

    [Header("새 소환 설정")]
    [SerializeField] private float warningTime = 1.0f;    // 경고선이 깜빡이는 시간
    [SerializeField] private float spawnInterval = 3.0f;  // 새 소환 주기
    [SerializeField] private float birdGap = 1.5f;        // 새 간격
    [SerializeField] private float spawnX = 15f;          // 화면 오른쪽 끝 (새가 생성될 X 좌표)
    [SerializeField] private float minY = -4f;            // 새가 생성될 최소 Y 좌표
    [SerializeField] private float maxY = 4f;            // 새가 생성될 최대 Y 좌표

    [Header("정령 소환 보호막 설정")]
    [SerializeField] private GameObject shieldObject;     // 보스 자식 배리어 오브젝트
    [SerializeField] private int healPerReturnedSpirit = 20; // 귀환 정령 1마리당 회복량
    [SerializeField] private float castingDuration = 1f;     // Cast 애니메이션 길이
    private Animator bossAnim;

    [SerializeField] private Transform[] spiritSpawnPoints;

    [Header("정령 key")]
    [SerializeField] private string[] spiritPoolKeys = new string[3] { "GreenSpirit", "MintSpirit", "YellowGreenSpirit" };

    [Header("바람 설정")]
    [SerializeField] private float detectRange = 3.5f;       // 플레이어 접근 감지 거리
    [SerializeField] private float pushCooldown = 6.0f;      // 패턴 재사용 대기시간
    [SerializeField] private float pushForce = 25f;          // 밀어내는 힘
    [SerializeField] private float lockDuration = 0.6f;      // 스턴 시간
    [SerializeField] private string windPoolKey = "BossWindBlast"; // 연출용 바람 이펙트 키

    [Header("개구리 소환 설정")]
    [SerializeField] private string frogPoolKey = "GreenBossFrog";
    [SerializeField] private Vector2 frogSpawnOffset = new Vector2(0f, 4f); // 플레이어 위 오프셋
    [SerializeField] private float frogPatternInterval = 10f;

    [Header("꽃 함정 설정")]
    [SerializeField] private string flowerTrapPoolKey = "FlowerTrap"; // PoolingManager 키
    [SerializeField] private int flowerSpawnCount = 3;                // 한 번에 소환할 꽃 개수
    [SerializeField] private float flowerSpawnInterval = 0.4f;        // 꽃 사이 소환 간격(초)
    [SerializeField] private float flowerPatternInterval = 12f;       // 패턴 반복 주기(초)
    [SerializeField] private float flowerMinX = -8f;   // 소환 범위 최솟값
    [SerializeField] private float flowerMaxX = 8f;    // 소환 범위 최댓값
    [SerializeField] private float flowerMinGap = 2f;  // 꽃 사이 최소 간격
    [SerializeField] private float flowerSpawnOffsetY = 0.6f;         // 꽃 오프셋
    [SerializeField] private LayerMask groundLayer;

    [Header("10% 페이즈")]
    [SerializeField] private GameObject finalEffectPrefab;
    [SerializeField] private GameObject finalMonsterPrefab;
    [SerializeField] private float effectOffset = 2f;
    [SerializeField] private float fadeOutDuration = 2f;

    private float currentPushCooldown = 0f;
    private bool isPushing = false; // 현재 밀어내는 패턴이 진행 중인지 체크

    // 코루틴 관리 변수
    private Coroutine birdRoutine;
    private Coroutine frogRoutine;
    private Coroutine flowerRoutine;
    private Coroutine pushRoutine;
    private Coroutine spiritRoutine;
    private Coroutine finalPhaseRoutine;
    private Coroutine fadeRoutine;

    private int activeSpiritsCount = 0;       // 현재 살아있는 정령 수
    private bool hasTriggeredSpiritPattern = false; // 체력 50% 이하 패턴 발동 체크
    private bool hasTriggeredFinalPhase = false;
    private bool isFinalPhase = false; // 마지막 패턴때 무적 플래그
    private SpriteRenderer sr;
    private EnemyStatus enemyStatus;
    private EnemyStatus activeKpStatus;

    // 플레이어 감지 관련 변수
    private Transform playerTransform;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        sr = GetComponentInChildren<SpriteRenderer>();
        bossAnim = GetComponentInChildren<Animator>();

        if (shieldObject != null)
        {
            shieldObject.SetActive(false);
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.Log("Player 태그 없음");
        }
    }

    private void Start()
    {
        StartBirdAttack();
        StartFrogAttack();
        StartFlowerAttack();
    }

    private void Update()
    {
        if (currentPushCooldown > 0f)
        {
            currentPushCooldown -= Time.deltaTime;
        }

        if (currentPushCooldown <= 0f && activeSpiritsCount == 0 && !isPushing)
        {
            CheckPlayerDistanceAndPushInstant();
        }
    }

    private void OnDisable()
    {
        StopAllBossCoroutines();

        try
        {
            FlowerTrap[] activeTraps = FindObjectsByType<FlowerTrap>(FindObjectsSortMode.None);
            foreach (FlowerTrap trap in activeTraps)
            {
                if (trap != null)
                {
                    trap.ForceClear();
                }
            }
        }
        catch {}
    }

    private void OnDestroy()
    {
        if (activeKpStatus != null)
        {
            activeKpStatus.OnEnemyDeath -= RevealBossAfterKP;
        }
    }

    #endregion

    #region Coroutine Management

    private void StopAllBossCoroutines()
    {
        if (birdRoutine != null) { StopCoroutine(birdRoutine); birdRoutine = null; }
        if (frogRoutine != null) { StopCoroutine(frogRoutine); frogRoutine = null; }
        if (flowerRoutine != null) { StopCoroutine(flowerRoutine); flowerRoutine = null; }
        if (pushRoutine != null) { StopCoroutine(pushRoutine); pushRoutine = null; }
        if (spiritRoutine != null) { StopCoroutine(spiritRoutine); spiritRoutine = null; }
        if (finalPhaseRoutine != null) { StopCoroutine(finalPhaseRoutine); finalPhaseRoutine = null; }
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
    }

    #endregion

    #region Pattern: Bird (새 소환)

    public void StartBirdAttack()
    {
        if (birdRoutine == null)
        {
            birdRoutine = StartCoroutine(SpawnBirdRoutine());
        }
    }

    public void StopBirdAttack()
    {
        if (birdRoutine != null)
        {
            StopCoroutine(birdRoutine);
            birdRoutine = null;
        }
    }

    private IEnumerator SpawnBirdRoutine()
    {
        while (true)
        {
            float centerY = Random.Range(minY + birdGap, maxY - birdGap);

            for (int i = 0; i < 3; i++)
            {
                float spawnY = centerY + ((i - 1) * birdGap);
                Vector3 spawnPosition = new Vector3(spawnX, spawnY, 0f);

                GameObject birdObj = PoolingManager.Instance.Get("GreenBossBird", spawnPosition, Quaternion.identity);

                if (birdObj != null)
                {
                    Bird bird = birdObj.GetComponent<Bird>();
                    if (bird != null)
                    {
                        bird.Init(warningTime);
                    }
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    #endregion

    #region Pattern: Wind Blast (바람)

    private void CheckPlayerDistanceAndPushInstant()
    {
        if (playerTransform == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance <= detectRange)
        {
            currentPushCooldown = pushCooldown;

            PlayerStatus pStatus = playerTransform.GetComponent<PlayerStatus>();
            if (pStatus != null)
            {
                pushRoutine = StartCoroutine(PushAndLockPlayerRoutine(pStatus));
            }
        }
    }

    private IEnumerator PushAndLockPlayerRoutine(PlayerStatus status)
    {
        isPushing = true;

        // 바람 이펙트 소환
        Vector3 effectPos = transform.position + new Vector3(-2f, 0f, 0f);
        GameObject windObj = PoolingManager.Instance.Get(windPoolKey, effectPos, Quaternion.identity);
        if (windObj == null)
        {
            Debug.LogWarning("Failed to pool wind effect object!");
        }

        // 왼쪽 넉백
        IHittable hittable = status.GetComponent<IHittable>();
        if (hittable != null)
        {
            hittable.TakeHit(Vector2.left * pushForce);
        }

        // 넉백이 어느 정도 적용된 직후 스턴
        yield return new WaitForSeconds(0.1f);

        // 스턴
        IStunnable stunnable = status.GetComponent<IStunnable>();
        if (stunnable != null)
        {
            stunnable.ApplyStun(lockDuration);
        }

        isPushing = false;
        pushRoutine = null;
    }

    #endregion

    #region Pattern: Spirit Shield (정령 보호막)

    public void StartSpiritAttack()
    {
        if (activeSpiritsCount > 0)
        {
            return;
        }
        spiritRoutine = StartCoroutine(SpiritAttackRoutine());
    }

    private IEnumerator SpiritAttackRoutine()
    {
        // Cast 애니메이션
        if (bossAnim != null)
        {
            bossAnim.SetTrigger("Cast");
        }

        yield return new WaitForSeconds(castingDuration);

        // 9마리 소환 (각 타입 3마리씩)
        activeSpiritsCount = spiritPoolKeys.Length * 3; // 9

        if (shieldObject != null)
        {
            shieldObject.SetActive(true);
        }

        for (int i = 0; i < spiritPoolKeys.Length; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                // spiritSpawnPoints와 그 요소들 null 체크 후 스폰 위치 결정
                Vector3 spawnPos = transform.position;
                if (spiritSpawnPoints != null && spiritSpawnPoints.Length > 0)
                {
                    Transform pt = spiritSpawnPoints[Random.Range(0, spiritSpawnPoints.Length)];
                    if (pt != null)
                    {
                        spawnPos = pt.position;
                    }
                }

                GameObject spiritObj = PoolingManager.Instance.Get(spiritPoolKeys[i], spawnPos, Quaternion.identity);
                if (spiritObj != null)
                {
                    BossSpirit spirit = spiritObj.GetComponent<BossSpirit>();
                    if (spirit != null)
                    {
                        spirit.Init(this, (BossSpirit.SpiritType)i, transform);
                    }
                }
            }
        }
        spiritRoutine = null;
    }

    public void OnSpiritReturned()
    {
        // 귀환한 정령 수 비례 회복
        if (enemyStatus != null)
        {
            enemyStatus.Heal(healPerReturnedSpirit);
        }

        activeSpiritsCount--;

        if (activeSpiritsCount <= 0)
        {
            activeSpiritsCount = 0;
            if (shieldObject != null)
            {
                shieldObject.SetActive(false);
            }
            if (bossAnim != null)
            {
                bossAnim.SetTrigger("Idle"); // Idle 복귀
            }
        }
    }

    // 정령이 죽었을 때 정령 스크립트에서 호출해 줄 콜백 함수
    public void OnSpiritDestroyed()
    {
        activeSpiritsCount--;

        // 모든 정령이 사라지면 무적 배리어 해제
        if (activeSpiritsCount <= 0)
        {
            activeSpiritsCount = 0;
            if (shieldObject != null)
            {
                shieldObject.SetActive(false);
            }
            if (bossAnim != null)
            {
                bossAnim.SetTrigger("Idle");
            }
        }
    }

    #endregion

    #region Pattern: Frog (개구리 소환)

    public void StartFrogAttack()
    {
        if (frogRoutine == null)
        {
            frogRoutine = StartCoroutine(SpawnFrogRoutine());
        }
    }

    public void StopFrogAttack()
    {
        if (frogRoutine != null)
        {
            StopCoroutine(frogRoutine);
            frogRoutine = null;
        }
    }

    private IEnumerator SpawnFrogRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(frogPatternInterval);

            if (playerTransform == null)
            {
                continue;
            }

            Vector3 spawnPos = playerTransform.position + (Vector3)frogSpawnOffset;
            GameObject frogObj = PoolingManager.Instance.Get(frogPoolKey, spawnPos, Quaternion.identity);
            if (frogObj == null)
            {
                Debug.LogWarning("Failed to pool frog object!");
            }
        }
    }

    #endregion

    #region Pattern: Flower Trap (꽃 함정)

    public void StartFlowerAttack()
    {
        if (flowerRoutine == null)
        {
            flowerRoutine = StartCoroutine(SpawnFlowerRoutine());
        }
    }

    public void StopFlowerAttack()
    {
        if (flowerRoutine != null)
        {
            StopCoroutine(flowerRoutine);
            flowerRoutine = null;
        }
    }

    private IEnumerator SpawnFlowerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(flowerPatternInterval);

            List<float> spawnXList = GetSpawnXPositions();

            foreach (float x in spawnXList)
            {
                SpawnOneFlower(x);
                yield return new WaitForSeconds(flowerSpawnInterval);
            }
        }
    }

    private List<float> GetSpawnXPositions()
    {
        List<float> positions = new List<float>();
        int maxTry = 30; // 무한루프 방지

        while (positions.Count < flowerSpawnCount && maxTry-- > 0)
        {
            float randomX = Random.Range(flowerMinX, flowerMaxX);

            // 기존 위치들과 간격 체크
            bool tooClose = false;
            foreach (float existing in positions)
            {
                if (Mathf.Abs(randomX - existing) < flowerMinGap)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                positions.Add(randomX);
            }
        }

        return positions;
    }

    private void SpawnOneFlower(float x)
    {
        Vector2 rayOrigin = new Vector2(x, 90f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 30f, groundLayer);

        if (hit.collider == null)
        {
            return;
        }

        Vector3 spawnPos = new Vector3(x, hit.point.y + flowerSpawnOffsetY, 0f);

        GameObject flowerObj = PoolingManager.Instance.Get(flowerTrapPoolKey, spawnPos, Quaternion.identity);
        if (flowerObj != null)
        {
            FlowerTrap trap = flowerObj.GetComponent<FlowerTrap>();
            if (trap != null)
            {
                trap.Init(flowerTrapPoolKey);
            }
        }
    }

    #endregion

    #region Pattern: Final Phase (마지막 소환수)

    private IEnumerator FinalPhaseRoutine()
    {
        isFinalPhase = true; // 무적

        // 모든 패턴 정지
        StopBirdAttack();
        StopFrogAttack();
        StopFlowerAttack();

        // 다른 부가 코루틴 정지
        if (pushRoutine != null) { StopCoroutine(pushRoutine); pushRoutine = null; }
        if (spiritRoutine != null) { StopCoroutine(spiritRoutine); spiritRoutine = null; }

        // 보스 투명화 페이드 아웃 (Nested coroutine inline yield)
        yield return FadeRoutine(0f, fadeOutDuration);

        // 마법진 프리팹이 있다면 생성
        if (finalEffectPrefab != null)
        {
            GameObject effect = Instantiate(finalEffectPrefab, transform.position + Vector3.up * effectOffset, Quaternion.identity);
            CastingSpell castingSpell = effect.GetComponent<CastingSpell>();

            if (castingSpell != null)
            {
                // 마법진 이펙트가 끝나는 타이밍에 소환수 소환
                castingSpell.Init(() =>
                {
                    SpawnKillerPlant();
                });
            }
            else
            {
                // 마법진 스크립트가 없다면 즉시 소환
                SpawnKillerPlant();
            }
        }
        else
        {
            // 마법진 프리팹 자체가 없다면 즉시 소환
            SpawnKillerPlant();
        }

        // 보스 제거
        yield return new WaitForSeconds(0.1f);
        gameObject.SetActive(false);
        finalPhaseRoutine = null;
    }

    private void SpawnKillerPlant()
    {
        if (finalMonsterPrefab == null)
        {
            return;
        }

        // 소환수(KillerPlant) 생성
        GameObject spawnedKP = Instantiate(finalMonsterPrefab, transform.position, Quaternion.identity);

        // 소환수의 EnemyStatus 보스 복귀 함수 연결
        if (spawnedKP.TryGetComponent(out EnemyStatus kpStatus))
        {
            activeKpStatus = kpStatus;
            activeKpStatus.OnEnemyDeath += RevealBossAfterKP;
        }
    }

    // 보스 복귀 함수
    public void RevealBossAfterKP()
    {
        if (activeKpStatus != null)
        {
            activeKpStatus.OnEnemyDeath -= RevealBossAfterKP;
            activeKpStatus = null;
        }

        gameObject.SetActive(true);
        isFinalPhase = false;
        
        fadeRoutine = StartCoroutine(FadeRoutine(1f, 1f));

        StartBirdAttack();
        StartFlowerAttack();
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (sr == null)
        {
            yield break;
        }

        Color color = sr.color;
        float startAlpha = color.a;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
            sr.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        sr.color = color;
    }

    #endregion

    #region Damage Callbacks

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (activeSpiritsCount > 0 || isFinalPhase)
        {
            return true; // 대미지 무효
        }
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        if (!hasTriggeredSpiritPattern && status != null)
        {
            float hpRatio = status.GetHPRatio();
            if (hpRatio > 0f && hpRatio <= 0.5f)
            {
                hasTriggeredSpiritPattern = true;
                StartSpiritAttack();
            }
        }
        if (!hasTriggeredFinalPhase && status != null)
        {
            float hpRatio = status.GetHPRatio();
            if (hpRatio > 0f && hpRatio <= 0.1f)
            {
                hasTriggeredFinalPhase = true;
                finalPhaseRoutine = StartCoroutine(FinalPhaseRoutine());
            }
        }
    }

    #endregion
}