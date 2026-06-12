using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 공격패턴1 : 경고선 후 새를 세마리 소환하여 직선으로 돌진 
 * 공격패턴2 : 개구리를 플레이어 위에 소환하여 공격
 * 방어패턴 : 체력이 50% 미만이 되면 정령을 3체 소환(나비) 후 배리어 생성
 * 배리어가 있는 동안 보스는 체력 회복 + 무적
 * 정령을 모두 삭제해야 배리어가 사라짐
 */

public class GreenBossAttack : MonoBehaviour, IHitReaction
{
    [Header("새 소환 설정")]
    [SerializeField] private float warningTime = 1.0f;    // 경고선이 깜빡이는 시간
    [SerializeField] private float spawnInterval = 3.0f;  // 새 소환 주기
    [SerializeField] private float birdGap = 1.5f;        // 새 간격
    [SerializeField] private float spawnX = 15f;          // 화면 오른쪽 끝 (새가 생성될 X 좌표)
    [SerializeField] private float minY = -4f;            // 새가 생성될 최소 Y 좌표
    [SerializeField] private float maxY = 4f;            // 새가 생성될 최대 Y 좌표

    [Header("정령 소환 보호막 설정")]
    [SerializeField] private GameObject shieldObject;     // 보스 자식 배리어 오브젝트
    [SerializeField] private int healAmountPerSpirit = 5; // 정령 1마리당 초당 회복량

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
    [SerializeField] private float fadeOutDuration = 2f;

    private float currentPushCooldown = 0f;
    private bool isPushing = false; // 현재 밀어내는 패턴이 진행 중인지 체크

    // 코루틴 관리 변수
    private Coroutine birdRoutine;
    private Coroutine frogRoutine;
    private Coroutine flowerRoutine;

    private int activeSpiritsCount = 0;       // 현재 살아있는 정령 수
    private float remainderHeal = 0f;         // 정수 회복을 위한 소수점 누적 변수
    private bool hasTriggeredSpiritPattern = false; // 체력 50% 이하 패턴 발동 체크
    private bool hasTriggeredFinalPhase = false;
    private SpriteRenderer sr;
    private EnemyStatus enemyStatus;

    // 플레이어 감지 관련 변수
    private Transform playerTransform;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        sr = GetComponentInChildren<SpriteRenderer>();

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
        //StartBirdAttack();
        //StartFrogAttack();
        StartFlowerAttack();
    }

    private void Update()
    {
        if (activeSpiritsCount > 0 && enemyStatus != null)
        {
            float totalHeal = healAmountPerSpirit * activeSpiritsCount * Time.deltaTime;
            RestoreHealth(totalHeal);
        }

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
        StopBirdAttack();
        StopFrogAttack();
    }

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
                StartCoroutine(PushAndLockPlayerRoutine(pStatus));
            }
        }
    }

    private IEnumerator PushAndLockPlayerRoutine(PlayerStatus status)
    {
        isPushing = true;

        // 바람 이펙트 소환
        Vector3 effectPos = transform.position + new Vector3(-2f, 0f, 0f);
        PoolingManager.Instance.Get(windPoolKey, effectPos, Quaternion.identity);

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
    }

    public void StartSpiritAttack()
    {
        if (activeSpiritsCount > 0)
        {
            return;
        }
        int spiritCount = spiritPoolKeys.Length;
        activeSpiritsCount = spiritCount;
        remainderHeal = 0f;

        if (shieldObject != null)
        {
            shieldObject.SetActive(true);
        }

        bool[] usedIndices = new bool[spiritSpawnPoints.Length];

        for (int i = 0; i < spiritCount; i++)
        {
            Vector3 spawnPosition = transform.position;

            if (spiritSpawnPoints.Length > 0)
            {
                if (spiritSpawnPoints.Length >= spiritCount)
                {
                    int randomIndex;
                    do
                    { 
                        randomIndex = Random.Range(0, spiritSpawnPoints.Length);
                    }while (usedIndices[randomIndex]);

                    usedIndices[randomIndex] = true;
                    spawnPosition = spiritSpawnPoints[randomIndex].position;
                }
                else
                {
                    spawnPosition = spiritSpawnPoints[i % spiritSpawnPoints.Length].position;
                }
            }

            string currentSpiritKey = spiritPoolKeys[i];
            GameObject spiritObj = PoolingManager.Instance.Get(currentSpiritKey, spawnPosition, Quaternion.identity);

            if (spiritObj != null)
            {
                BossSpirit spirit = spiritObj.GetComponent<BossSpirit>();
                if (spirit != null)
                {
                    spirit.Init(this, (BossSpirit.SpiritType)i);
                }
            }
            else
            {
                Debug.LogError($"정령 소환 실패");
            }
        }
    }
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
            PoolingManager.Instance.Get(frogPoolKey, spawnPos, Quaternion.identity);
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
        }
    }
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
                positions.Add(randomX);
        }

        return positions;
    }
    private void SpawnOneFlower(float x)
    {
        Vector2 rayOrigin = new Vector2(x, 10f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 30f, groundLayer);

        if (hit.collider == null) return;

        Vector3 spawnPos = new Vector3(x, hit.point.y + flowerSpawnOffsetY, 0f);

        GameObject flowerObj = PoolingManager.Instance.Get(flowerTrapPoolKey, spawnPos, Quaternion.identity);
        if (flowerObj != null)
        {
            FlowerTrap trap = flowerObj.GetComponent<FlowerTrap>();
            if (trap != null) trap.Init(flowerTrapPoolKey);
        }
    }
    private IEnumerator FinalPhaseRoutine()
    {
        // 모든 패턴 정지
        StopBirdAttack();
        StopFrogAttack();
        StopFlowerAttack();

        // 페이드 아웃
        yield return StartCoroutine(FadeRoutine(0f, fadeOutDuration));

        if (finalEffectPrefab != null)
        {
            GameObject effect = Instantiate(finalEffectPrefab, transform.position, Quaternion.identity);
            CastingSpell castingSpell = effect.GetComponent<CastingSpell>();

            if (castingSpell != null)
            {
                castingSpell.Init(() =>
                {
                    // 마법진 끝나면 몬스터 소환
                    if (finalMonsterPrefab != null)
                    {
                        Instantiate(finalMonsterPrefab, transform.position, Quaternion.identity);
                    }
                });
            }
            else
            {
                // 스크립트 없으면 그냥 바로 소환
                if (finalMonsterPrefab != null)
                {
                    Instantiate(finalMonsterPrefab, transform.position, Quaternion.identity);
                }
            }
        }

        gameObject.SetActive(false);
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
    private void RestoreHealth(float amount)
    {
        remainderHeal += amount;
        int healToInt = Mathf.FloorToInt(remainderHeal);

        if (healToInt > 0)
        {
            remainderHeal -= healToInt;
            enemyStatus.Heal(healToInt); 
        }
    }

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (activeSpiritsCount > 0)
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
                StartCoroutine(FinalPhaseRoutine());
            }
        }
    }
}