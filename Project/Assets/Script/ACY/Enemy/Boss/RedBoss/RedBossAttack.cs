using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/*
 * 공격패턴1 : 화살을 3갈래로 발사. 화살은 플레이어를 추격하며 생존시간이 다 되거나 공격받은 경우 사라짐
 * 공격패턴2 : 보스 주변을 원형으로 도는 마법구체 생성 후 플레이어 방향으로 일직선으로 날려보냄
 * 공격패턴3 : 보스가 분신을 소환하며 캐스팅 시작. 캐스팅 중 공격받으면 캔슬 후 일정시간 경직.
 * 캐스팅 성공 시 하늘에서 메테오를 생성해 떨어트림
 * 공격패턴4 : 보스가 사라진 후 십자 모양의 경고선 표시 + 레이저 발사 후 90도로 회전
 * 특성 : 체력이 50% 이하로 하락 시 분노 연출 후 패턴 강화
 * 강화패턴 : 화살 3연속 발사, 마법구체 개수 증가, 레이저 역회전 추가
 * 발악패턴 : HP가 10%이하로 떨어질 시 일정시간 무적 + 원형 탄환 방출 및 회전 패턴 끝난 후 무적 해제
 */
public class RedBossAttack : MonoBehaviour, IStunnable, IHitReaction
{
    #region Settings & Variables

    [Header("공통")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform pivotPoint;
    [SerializeField] private float attackCooldown = 3f;   // 공격 간격 

    [Header("화살 설정")]
    [SerializeField] private float angleSpread = 15f;     // 각도 차이

    private const string ARROW_KEY = "RedBossArrow";

    [Header("캐스팅 UI")]
    [SerializeField] private Slider meteorCastSlider;

    [Header("메테오 설정")]
    [SerializeField] private int meteorWaveCount = 3;
    [SerializeField] private int meteorPerWave = 5;
    [SerializeField] private float waveInterval = 0.5f;
    [SerializeField] private float meteorCastTime = 2f;
    [SerializeField] private Vector2 meteorSpawnXRange = new Vector2(-6f, 6f);
    [SerializeField] private Vector2 meteorSpawnYRange = new Vector2(6f, 9f);
    [SerializeField] private float meteorTargetY = -2.5f;
    private const string METEOR_KEY = "RedBossMeteor";

    [Header("스턴 설정")]
    [SerializeField] private float stunDuration = 2f;

    [Header("분신 설정")]
    [SerializeField] private Transform[] teleportPoints;
    [SerializeField] private GameObject cloneDisappearVFX;
    [SerializeField] private GameObject cloneSpawnVFX;
    private bool hasClones = false;

    [Header("레이저 설정")]
    [SerializeField] private GameObject laserCrossPrefab;
    [SerializeField] private Transform laserSpawnPoint;
    private const string LASER_KEY = "RedBossLaser";

    [SerializeField] private float laserWarningTime = 1.5f;
    [SerializeField] private float laserDuration = 4f;

    [Header("구체 설정")]
    [SerializeField] private float orbOrbitRadius = 2f;    // 공전 반지름
    [SerializeField] private int orbCount = 3;
    [SerializeField] private float orbWaitTime = 2f;       // 발사 전 대기
    private const string ORB_KEY = "RedBossOrb";

    [Header("분노 설정")]
    [SerializeField] private GameObject enrageVFX; // 분노 이펙트
    [SerializeField] private float enrageTransitionTime = 2f; //이펙트 길이
    private bool isEnraged = false;
    private bool isEnrageTransitioning = false; // 분노 연출 중인지 체크

    [Header("발악 패턴 설정")]
    [SerializeField] private Transform centerPoint; // 고정될 화면 중앙 위치
    private bool isLastStand = false;
    private bool isLastStandTransitioning = false;
    private bool hasDoneLastStand = false;

    [System.Serializable]
    public struct PatternWeight
    {
        public string patternName;
        public int weight;
    }
    [Header("가중치 설정")]
    [SerializeField]
    private List<PatternWeight> normalWeights = new List<PatternWeight>()
    {
        new PatternWeight { patternName = "Arrow", weight = 40 },  // 자주 사용 (40%)
        new PatternWeight { patternName = "Orb",   weight = 35 },  // 자주 사용 (35%)
        new PatternWeight { patternName = "Meteor", weight = 15 },  // 덜 사용 (15%)
        new PatternWeight { patternName = "Laser",  weight = 10 }   // 덜 사용 (10%)
    };

    private string lastExecutedPattern = "";
    private GameObject[] currentClones;
    private List<GameObject> activeOrbs = new List<GameObject>();

    private bool isCastingMeteor = false;
    private bool isStunned = false;
    private bool isInvincible = false;
    public bool IsStunned => isStunned;
    public bool IsCastingMeteor => isCastingMeteor;
    public bool IsInvincible => isInvincible;

    private SpriteRenderer sr;
    private Color originalColor;
    private Animator anim;

    // 코루틴 추적 변수
    private Coroutine attackRoutine;
    private Coroutine currentPatternRoutine;
    private Coroutine stunRoutine;
    private Coroutine enrageRoutine;
    private Coroutine lastStandRoutine;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        EnemyStatus status = GetComponent<EnemyStatus>();
        if (status != null)
        {
            status.OnEnemyDeath += HandleDeath;
        }

        if (sr != null)
        {
            originalColor = sr.color;
        }

        if (meteorCastSlider != null)
        {
            meteorCastSlider.gameObject.SetActive(false);
        }

        // teleportPoints null 체크
        if (teleportPoints != null)
        {
            currentClones = new GameObject[teleportPoints.Length];
        }
        else
        {
            currentClones = new GameObject[0];
        }

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private void HandleDeath()
    {
        StopAllBossCoroutines();
    }

    private void OnDestroy()
    {
        EnemyStatus status = GetComponent<EnemyStatus>();
        if (status != null)
        {
            status.OnEnemyDeath -= HandleDeath;
        }
    }

    #endregion

    #region Coroutine Management

    // StopAllCoroutines 대신 특정 코루틴만 정지하여 오작동 방지
    private void StopAllBossCoroutines()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (currentPatternRoutine != null)
        {
            StopCoroutine(currentPatternRoutine);
            currentPatternRoutine = null;
        }
        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }
        if (enrageRoutine != null)
        {
            StopCoroutine(enrageRoutine);
            enrageRoutine = null;
        }
        if (lastStandRoutine != null)
        {
            StopCoroutine(lastStandRoutine);
            lastStandRoutine = null;
        }
    }

    #endregion

    #region Pattern Choice Logic

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            if (isLastStand) yield break;
            // 공격 간격 대기
            yield return new WaitForSeconds(attackCooldown);

            if (isLastStand) yield break;
            // 가중치 기반으로 다음 패턴 선택
            string nextPattern = ChooseNextPattern();

            // 선택된 패턴 실행 및 종료 대기 (직전 패턴 저장)
            lastExecutedPattern = nextPattern;

            switch (nextPattern)
            {
                case "Arrow":
                    currentPatternRoutine = StartCoroutine(AttackArrow());
                    yield return currentPatternRoutine;
                    break;
                case "Orb":
                    currentPatternRoutine = StartCoroutine(AttackOrb());
                    yield return currentPatternRoutine;
                    break;
                case "Meteor":
                    currentPatternRoutine = StartCoroutine(AttackMeteor());
                    yield return currentPatternRoutine;
                    break;
                case "Laser":
                    currentPatternRoutine = StartCoroutine(AttackLaser());
                    yield return currentPatternRoutine;
                    break;
            }
            currentPatternRoutine = null;

            if (isLastStand)
            {
                yield break;
            }
        }
    }

    // 가중치 계산 및 선택
    private string ChooseNextPattern()
    {
        int totalWeight = 0;
        List<PatternWeight> validPatterns = new List<PatternWeight>();

        // 현재 선택 가능한 패턴 필터링 (연속 사용 방지)
        foreach (var pattern in normalWeights)
        {
            // 직전에 실행한 패턴은 이번 선택에서 제외 (가중치 계산 안 함)
            if (pattern.patternName == lastExecutedPattern)
            {
                continue;
            }

            validPatterns.Add(pattern);
            totalWeight += pattern.weight;
        }

        // 만약 모든 패턴이 배제되는 예외 상황이 발생하면 전체 패턴으로 롤백
        if (validPatterns.Count == 0)
        {
            validPatterns = normalWeights;
            foreach (var pattern in validPatterns) totalWeight += pattern.weight;
        }

        // 0 ~ totalWeight 사이의 랜덤 값 생성
        int randomValue = Random.Range(0, totalWeight);
        int cumulativeWeight = 0;

        // 누적 가중치 비교를 통한 최종 패턴 결정
        foreach (var pattern in validPatterns)
        {
            cumulativeWeight += pattern.weight;
            if (randomValue < cumulativeWeight)
            {
                return pattern.patternName;
            }
        }

        return "Arrow"; // 예외 처리용 기본값
    }

    #endregion

    #region Arrow Pattern

    public IEnumerator AttackArrow()
    {
        // 분노 상태면 3연발, 아니면 1발
        int shootCount = isEnraged ? 3 : 1;

        // 연속 발사 간격
        float burstDelay = 0.5f;

        for (int i = 0; i < shootCount; i++)
        {
            if (anim != null)
            {
                anim.SetTrigger("Shoot");
            }

            // 첫 번째 발사는 1.2초, 이후 발사는 0.5초
            float waitTime = (i == 0) ? 1.2f : burstDelay;
            yield return new WaitForSeconds(waitTime);

            // 유도탄 발사 효과음 재생
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX(AudioManager.SFX.RedBossFire);
            }

            SpawnArrow(0f);              // 가운데
            SpawnArrow(-angleSpread);    // 아래쪽/왼쪽 방향
            SpawnArrow(angleSpread);     // 위쪽/오른쪽 방향
        }
    }

    private void SpawnArrow(float angleOffset)
    {
        // firePoint null 예외 처리
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject obj = PoolingManager.Instance.Get(ARROW_KEY, spawnPos, Quaternion.identity);
        if (obj == null)
        {
            return;
        }

        obj.GetComponent<FireArrow>()?.Init(angleOffset);
    }

    #endregion

    #region Magic Orb Pattern

    public IEnumerator AttackOrb()
    {
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(0.5f); 

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            yield break;
        }
        int currentOrbCount = isEnraged ? 4 : orbCount;
        activeOrbs.Clear();
        MagicOrb[] orbs = new MagicOrb[currentOrbCount];
        Vector3 spawnPos = pivotPoint != null ? pivotPoint.position : transform.position;

        for (int i = 0; i < currentOrbCount; i++)
        {
            float angle = i * (360f / currentOrbCount);

            GameObject obj = PoolingManager.Instance.Get(ORB_KEY, spawnPos, Quaternion.identity);

            if (obj == null)
            {
                continue;
            }
            activeOrbs.Add(obj);

            MagicOrb orb = obj.GetComponent<MagicOrb>();
            orb?.Init(pivotPoint != null ? pivotPoint : transform, angle, orbOrbitRadius);
            orbs[i] = orb;
        }

        // 대기 (공전 연출)
        yield return new WaitForSeconds(orbWaitTime);

        // 발사 시점의 플레이어 위치 고정 후 동시 발사
        Vector3 targetPos = player.transform.position;

        if (anim != null)
        {
            anim.SetTrigger("Shoot");
        }
        for (int i = 0; i < orbs.Length; i++)
        {
            if (orbs[i] == null)
            {
                continue;
            }
            orbs[i].Launch(targetPos);
        }

        // 구체 비행 시간만큼 대기 후 다음 패턴
        yield return new WaitForSeconds(2f);
        activeOrbs.Clear();
    }

    //남아있는 구체 제거 함수
    public void RemoveCurrentOrbs()
    {
        if (activeOrbs == null || activeOrbs.Count == 0)
        {
            return;
        }
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            // 이미 반납된 오브젝트는 건너뜀
            if (activeOrbs[i] == null || !activeOrbs[i].activeSelf)
            {
                continue;
            }

            PoolingManager.Instance.Return(ORB_KEY, activeOrbs[i]);
        }

        activeOrbs.Clear();
    }

    #endregion

    #region Meteor Pattern

    public IEnumerator AttackMeteor()
    {
        isCastingMeteor = true;

        if (anim != null)
        {
            anim.SetBool("IsCasting", true);
        }

        SpawnClone();

        // 중첩 Coroutine 대신 인라인 yield return으로 순차 실행
        yield return ShowMeteorCastUI();

        // 캐스팅 중 스턴당했으면 취소
        if (isStunned)
        {
            isCastingMeteor = false;
            if (anim != null)
            {
                anim.SetBool("IsCasting", false);
            }
            RemoveClone();
            yield break;
        }
        RemoveClone();
        isCastingMeteor = false;

        if (anim != null)
        {
            anim.SetBool("IsCasting", false);
        }

        yield return null;

        for (int wave = 0; wave < meteorWaveCount; wave++)
        {
            SpawnMeteorWave();

            yield return new WaitForSeconds(waveInterval);
        }
        isCastingMeteor = false;
    }

    private void SpawnMeteorWave()
    {
        float totalWidth = meteorSpawnXRange.y - meteorSpawnXRange.x;
        float sectionWidth = totalWidth / meteorPerWave;

        float diagonalOffset = 3f;

        for (int i = 0; i < meteorPerWave; i++)
        {
            float sectionMinX = meteorSpawnXRange.x + sectionWidth * i;
            float sectionMaxX = sectionMinX + sectionWidth;

            float randomX = Random.Range(sectionMinX, sectionMaxX);
            float randomY = Random.Range(meteorSpawnYRange.x, meteorSpawnYRange.y);

            Vector3 targetPos = new Vector3(randomX, meteorTargetY, 0f);

            Vector3 spawnPos = new Vector3(randomX - diagonalOffset, randomY, 0f);

            GameObject obj = PoolingManager.Instance.Get(METEOR_KEY, spawnPos, Quaternion.identity);
            if (obj != null)
            {
                obj.GetComponent<Meteor>()?.Init(targetPos);
            }
        }
    }

    private IEnumerator ShowMeteorCastUI()
    {
        if (meteorCastSlider == null)
        {
            yield break;
        }

        meteorCastSlider.gameObject.SetActive(true);
        meteorCastSlider.maxValue = meteorCastTime;
        meteorCastSlider.value = 0f;

        float timer = 0f;

        while (timer < meteorCastTime)
        {
            timer += Time.deltaTime;
            meteorCastSlider.value = timer;
            yield return null;
        }

        meteorCastSlider.value = meteorCastTime;
        meteorCastSlider.gameObject.SetActive(false);
    }

    #endregion

    #region Interruption & Stun

    public void ApplyStun(float duration)
    {
        if (isStunned || isInvincible || isEnrageTransitioning || isLastStandTransitioning)
        {
            return;
        }

        isStunned = true;
        isCastingMeteor = false;
        if (anim != null)
        {
            anim.SetBool("IsCasting", false);
        }

        // 남은 분신 제거
        RemoveClone();

        StopAllBossCoroutines();
        stunRoutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        if (meteorCastSlider != null)
        {
            meteorCastSlider.gameObject.SetActive(false);
        }

        if (sr != null)
        {
            sr.color = new Color32(0, 202, 255, 255); // #00CAFF 하늘색
        }

        yield return new WaitForSeconds(duration);

        if (sr != null)
        {
            sr.color = originalColor;
        }

        isStunned = false;

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    public void OnHitDuringCast()
    {
        if (!isCastingMeteor || isStunned)
        {
            return;
        }
        if (!hasClones)
        {
            return;
        }
        ApplyStun(stunDuration);
    }

    #endregion

    #region Clone Management

    private void SpawnClone()
    {
        if (teleportPoints == null || teleportPoints.Length == 0)
        {
            return;
        }

        hasClones = true;

        System.Array.Clear(currentClones, 0, currentClones.Length);

        for (int i = 0; i < teleportPoints.Length; i++)
        {
            if (teleportPoints[i] == null)
            {
                continue;
            }

            Vector3 spawnPos = teleportPoints[i].position;

            if (Vector3.Distance(spawnPos, transform.position) < 0.5f)
            {
                continue;
            }

            GameObject cloneObj = PoolingManager.Instance.Get("RedBossClone", spawnPos, Quaternion.identity);

            if (cloneObj == null)
            {
                continue;
            }

            RedBossClone clone = cloneObj.GetComponent<RedBossClone>();

            if (clone != null)
            {
                clone.Init(this, cloneSpawnVFX, cloneDisappearVFX);
                clone.PlayCastAnimation();
            }

            currentClones[i] = cloneObj;
        }
    }

    public void SwapCloneAndBoss(int oldIndex, int newIndex)
    {
        if (!isCastingMeteor || currentClones == null || teleportPoints == null) // 분신이 없는 상태면 스왑 불가
        {
            return;
        }

        if (newIndex >= 0 && newIndex < currentClones.Length && currentClones[newIndex] != null) // 이동할 위치에 분신이 있으면 제거
        {
            currentClones[newIndex].GetComponent<RedBossClone>()?.DestroyClone(); // 분신 제거
            currentClones[newIndex] = null; // 배열에서 제거
        }

        if (oldIndex >= 0 && oldIndex < teleportPoints.Length)
        {
            if (teleportPoints[oldIndex] == null)
            {
                return;
            }

            Vector3 spawnPos = teleportPoints[oldIndex].position;
            GameObject cloneObj = PoolingManager.Instance.Get("RedBossClone", spawnPos, Quaternion.identity);

            if (cloneObj != null)
            {
                RedBossClone clone = cloneObj.GetComponent<RedBossClone>();
                clone?.Init(this, cloneSpawnVFX, cloneDisappearVFX);

                if (anim != null)
                {
                    AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                    clone?.SyncAnimation(stateInfo.fullPathHash, stateInfo.normalizedTime);
                }
                else
                {
                    clone?.PlayCastAnimation();
                }

                currentClones[oldIndex] = cloneObj;
            }
        }
    }

    public void RemoveClone()
    {
        if (currentClones == null)
        {
            return;
        }
        for (int i = 0; i < currentClones.Length; i++)
        {
            if (currentClones[i] == null) continue;

            currentClones[i].GetComponent<RedBossClone>()?.DestroyClone();
            currentClones[i] = null;
        }

        hasClones = false;
    }

    public void OnCloneDestroyed(GameObject cloneObj)
    {
        if (currentClones == null) return;
        for (int i = 0; i < currentClones.Length; i++)
        {
            if (currentClones[i] == cloneObj)
            {
                currentClones[i] = null;
            }
        }
    }

    #endregion

    #region Laser Pattern

    public IEnumerator AttackLaser()
    {
        isInvincible = true;
        yield return FadeRoutine(0f, 0.5f);

        // laserSpawnPoint null 체크
        Vector3 spawnPos = laserSpawnPoint != null ? laserSpawnPoint.position : transform.position;
        GameObject laserObj = PoolingManager.Instance.Get(LASER_KEY, spawnPos, Quaternion.identity);
        
        if (laserObj != null)
        {
            LaserCross laser = laserObj.GetComponent<LaserCross>();
            if (laser != null)
            {
                laser.Init(laserWarningTime, laserDuration, isEnraged);
            }
        }
        else
        {
            Debug.LogWarning("laserObj returned from PoolingManager is null!");
        }

        float totalWaitTime = laserWarningTime + laserDuration + 0.5f;

        if (isEnraged)
        {
            totalWaitTime += 0.5f + laserDuration;
        }

        yield return new WaitForSeconds(totalWaitTime);

        yield return FadeRoutine(1f, 0.5f);
        isInvincible = false;
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

        // 마지막에 목표값으로 확실하게 고정
        color.a = targetAlpha;
        sr.color = color;
    }

    #endregion

    #region Damage Callbacks

    public bool OnBeforeTakeDamage(EnemyStatus enemy, int damage)
    {
        if (isInvincible)
        {
            return true;
        }

        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus enemy, int damage)
    {
        OnHitDuringCast(); // 메테오 캐스팅 중 피격 시 스턴

        if (enemy == null)
        {
            return;
        }

        float currentHPRatio = enemy.GetHPRatio();

        if (!hasDoneLastStand && !isLastStandTransitioning && currentHPRatio <= 0.1f)
        {
            hasDoneLastStand = true; // 다시는 이 조건문에 들어오지 않음
            StopAllBossCoroutines();     // 현재 하던 공격 취소
            lastStandRoutine = StartCoroutine(LastStandTransitionRoutine()); // 발악 패턴 돌입
            return;
        }

        if (!isEnraged && !isEnrageTransitioning && !isLastStandTransitioning)
        {
            if (currentHPRatio <= 0.5f)
            {
                StopAllBossCoroutines();
                enrageRoutine = StartCoroutine(EnrageTransitionRoutine());
            }
        }
    }

    #endregion

    #region Phase Transitions

    private IEnumerator EnrageTransitionRoutine()
    {
        isEnrageTransitioning = true;
        isEnraged = true;    // 분노 상태 
        isInvincible = true; // 무적

        if (sr != null)
        {
            sr.color = originalColor; // 스턴 도중 전환 시 색상 복구
        }

        // 혹시 메테오 캐스팅 중이거나 레이저 때문에 투명해진 상태였다면 원래대로 복구
        isCastingMeteor = false;
        isStunned = false;
        ToggleVisibility(true);
        RemoveClone();
        RemoveCurrentOrbs();

        if (anim != null)
        {
            anim.SetBool("IsCasting", false);
            anim.Play("RedBossIdle1"); // 강제로 대기 모션 적용
        }

        // 분노 이펙트 생성
        if (enrageVFX != null)
        {
            Vector3 spawnPos = pivotPoint != null ? pivotPoint.position : transform.position;
            GameObject vfx = Instantiate(enrageVFX, spawnPos, Quaternion.identity);
            Destroy(vfx, enrageTransitionTime); // 연출 시간에 맞춰 이펙트 삭제
        }

        yield return new WaitForSeconds(enrageTransitionTime);

        isInvincible = false;
        isEnrageTransitioning = false;

        attackRoutine = StartCoroutine(AttackRoutine()); // 공격 루틴 처음부터 다시 시작
    }

    // 보스 숨기기 및 나타내기 강제 적용 함수 (분노 연출 시 보스가 투명해져있으면 강제로 나타냄)
    private void ToggleVisibility(bool isVisible)
    {
        if (sr != null)
        {
            Color color = sr.color;
            color.a = isVisible ? 1f : 0f;
            sr.color = color;
        }
    }

    private IEnumerator LastStandTransitionRoutine()
    {
        isLastStandTransitioning = true;
        isLastStand = true;
        isInvincible = true;

        if (sr != null)
        {
            sr.color = originalColor; // 스턴 도중 전환 시 색상 복구
        }

        isCastingMeteor = false;
        isStunned = false;
        ToggleVisibility(true);
        RemoveClone();
        RemoveCurrentOrbs();

        if (meteorCastSlider != null)
        {
            meteorCastSlider.gameObject.SetActive(false);
        }
        if (anim != null)
        {
            anim.Play("RedBossIdle1");
        }

        if (centerPoint != null)
        {
            transform.position = centerPoint.position;
        }

        yield return new WaitForSeconds(1.5f);

        isLastStandTransitioning = false;

        yield return LastStandAttackRoutine();

        isLastStand = false; 
        isInvincible = false; 

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator LastStandAttackRoutine()
    {
        // 지속 시간
        float duration = 4f;
        float timer = 0f;

        // 탄 간격(짧으면 촘촘함)
        float spawnInterval = 0.08f;
        float spawnTimer = 0f;

        // 회오리 현재 각도
        float currentSpiralAngle = 0f;

        // 회전할 각도 (클수록 빠름)
        float rotateSpeedPerShot = 15f;

        // 뻗어나갈 줄기 수
        int streamCount = 4;
        float angleStep = 360f / streamCount;

        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        while (timer < duration)
        {
            timer += Time.deltaTime;
            spawnTimer += Time.deltaTime;

            if (spawnTimer >= spawnInterval)
            {
                spawnTimer = 0f;

                currentSpiralAngle += rotateSpeedPerShot;

                for (int i = 0; i < streamCount; i++)
                {
                    float finalAngle = (i * angleStep) + currentSpiralAngle;

                    Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
                    GameObject obj = PoolingManager.Instance.Get(ARROW_KEY, spawnPos, Quaternion.identity);
                    if (obj != null)
                    {
                        obj.GetComponent<FireArrow>()?.Init(finalAngle, false, true);
                    }
                }
            }

            yield return null;
        }

        yield return new WaitForSeconds(attackCooldown);
    }

    #endregion
}