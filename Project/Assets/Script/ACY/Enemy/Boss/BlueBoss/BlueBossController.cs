using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 공격패턴1. 할퀴기(Claw) : 플레이어에게 접근해 근접 공격
 * 공격패턴2. 레이저(Sonic) : 플레이어가 공격 범위 내에 접근하면 레이저 발사
 * 공격패턴3. 돌진(Dash) : 플레이어의 방향으로 돌진 (닿으면 대미지)
 * 공격패턴4. 물기둥(WaterSpout) : 플레이어 근처에 물기둥 생성 (닿으면 대미지 + 에어본)
 * 행동패턴1. 휴식(Rest) : 돌진 후 restPoint로 돌아가서 대기
 * 
 * 1페이즈 : 할퀴기, 돌진 랜덤
 * 2페이즈 (HP50%) : 할퀴기, 레이저, 돌진(2연속) 랜덤
 * 3페이즈 (10%발악패턴) : 무적 상태가 되며 경고선을 여러개 생성 후 순차적으로 돌진 공격
 */
public class BossController : MonoBehaviour, IHitReaction
{
    #region Settings & Variables

    private Animator anim;
    private Rigidbody2D rb;
    private EnemyStatus enemyStatus;
    private BlueBossFlip bossFlip;

    [Header("상태 설정")]
    [SerializeField] private float dashSpeed = 10f; // 대시 속도
    [SerializeField] private float patternCooldown = 1.5f; // 패턴 쿨타임
    [SerializeField] private float moveSpeed = 6f; // 이동속도
    [SerializeField] private float clawAttackYOffset = 0.5f; // 할퀴기 패턴 오프셋(플레이어보다 얼마나 위에서 때릴 것인가)
    [SerializeField] private float sonicThickness = 1.0f; // 레이저 범위 감지 폭

    [Header("휴식 패턴")] 
    [SerializeField] private Transform[] restPoints; //휴식 포인트
    [SerializeField] private Vector2 restPointOffset = Vector2.zero; //오프셋
    [SerializeField] private float restJumpHeight = 5f; // 휴식 끝난 후 점프 높이

    [Header("히트박스 설정")]
    [SerializeField] private Collider2D clawHitbox;  // 할퀴기
    [SerializeField] private Collider2D dashHitbox; // 대시

    [Header("레이저 패턴 설정")]
    public string sonicPoolKey = "BlueLaser";
    [SerializeField] private Transform sonicSpawnPoint; // FirePoint
    [SerializeField] private float sonicAngle = 45f;   // 기울기 
    [SerializeField] private float sonicRange = 20f;
    [SerializeField] private int sonicDamage = 10;
    [SerializeField] private LayerMask playerLayer;

    [Header("물기둥 패턴 설정")]
    public string waterSpoutPoolKey = "WaterSpout";
    [SerializeField] private float waterSpoutInitialDelay = 3.5f; // 전투 시작 후 첫 물기둥까지 대기 시간
    [SerializeField] private float waterSpoutInterval = 3f;  // 생성 주기
    [SerializeField] private float waterSpoutSpread = 3f;
    [SerializeField] private float waterSpoutYOffset = 0f; // 오프셋
    [SerializeField] private LayerMask groundLayer;

    [Header("발악 패턴 (3페이즈) 설정")]
    public string warningLinePoolKey = "WarningLine"; // 경고선 키
    [SerializeField] private float dashWarningDuration = 0.8f; // 경고선이 유지되는 시간 (페이드아웃 포함)
    [SerializeField] private float warningInterval = 0.4f; // 경고선들이 생성되는 간격
    [SerializeField] private int enrageDashCount = 4; // 발악 패턴 갈래(본체 + 분신) 수
    [SerializeField] private int enrageSetCount = 3; // 발악 돌진 세트 반복 수
    [SerializeField] private float enrageSplitLateralOffset = 14f; // 시작 시 양쪽으로 퍼지는 가로 거리
    [SerializeField] private float enrageSplitVerticalStep = 10f; // 양쪽 분신 상하 간격

    private bool isPhase3 = false; // 3페이즈 진입 여부
    private bool isInvincible = false; // 무적 상태 플래그

    [Header("대시 중 벽 충돌 설정")]
    [SerializeField] private Collider2D[] mapBoundaryColliders;

    [Header("사망 시 켜질 콜라이더")]
    [SerializeField] private Collider2D deathCollider;

    private Transform player;
    private bool isPhase2 = false;
    private bool isDead = false;
    private bool restJumpTriggered;

    // 코루틴 추적 변수
    private Coroutine waterSpoutLoop;
    private Coroutine currentLoop;
    private Coroutine deathRoutine;
    private Coroutine enrageMoveRoutine;
    private BlueBossDashParticles dashVfx;
    private readonly List<BlueBossWarningLine> activeWarningLines = new List<BlueBossWarningLine>();
    private readonly List<GameObject> activeDashPhantoms = new List<GameObject>();

    private static readonly Color PhantomTintColor = new Color(0.58f, 0.88f, 1f, 0.86f);

    private static readonly int AnimClaw = Animator.StringToHash("Claw");
    private static readonly int AnimSonic = Animator.StringToHash("Sonic");
    private static readonly int AnimDashStart = Animator.StringToHash("DashStart");
    private static readonly int AnimDash = Animator.StringToHash("Dash");
    private static readonly int AnimDashEnd = Animator.StringToHash("DashEnd");
    private static readonly int AnimRestStart = Animator.StringToHash("RestStart");
    private static readonly int AnimRest = Animator.StringToHash("Rest");
    private static readonly int AnimRest2 = Animator.StringToHash("Rest2");

    private struct EnrageDashPhantom
    {
        public GameObject root;
        public Rigidbody2D rb;
        public SpriteRenderer spriteRenderer;
        public Animator animator;
        public Collider2D hitbox;
        public BlueBossDashParticles vfx;
        public Vector2 targetPosition;
        public Vector2 dashDir;
        public bool arrived;
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        bossFlip = GetComponent<BlueBossFlip>();
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyStatus = GetComponent<EnemyStatus>();
        dashVfx = BlueBossDashParticles.Attach(gameObject);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        if (deathCollider != null)
        {
            deathCollider.enabled = false;
        }
        if (clawHitbox != null)
        {
            clawHitbox.enabled = false;
        }

        if (enemyStatus != null)
        {
            enemyStatus.OnEnemyDeath += OnDeath;
        }

        currentLoop = StartCoroutine(Phase1Loop());
        waterSpoutLoop = StartCoroutine(WaterSpoutLoop());
    }

    private void Update()
    {
        if (BossIntroCutscene.IsPlaying || isDead || isPhase3)
        {
            return;
        }

        if (enemyStatus == null)
        {
            return;
        }

        float hpRatio = enemyStatus.GetHPRatio();

        if (!isPhase2 && hpRatio <= 0.5f)
        {
            isPhase2 = true;
        }

        // 3페이즈 최우선 진입
        if (hpRatio <= 0.1f)
        {
            isPhase3 = true;
            isPhase2 = false; // 2페이즈 루프 정지
            if (currentLoop != null)
            {
                StopCoroutine(currentLoop);
            }
            currentLoop = StartCoroutine(Phase3Loop());
            return;
        }

        if (sonicSpawnPoint != null && !isDead && bossFlip != null)
        {
            Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
            Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;

            Vector2 orthoDir = new Vector2(-fireDirection.y, fireDirection.x) * (sonicThickness * 0.5f);

            Debug.DrawRay((Vector2)sonicSpawnPoint.position + orthoDir, fireDirection * sonicRange, Color.yellow);
            Debug.DrawRay((Vector2)sonicSpawnPoint.position - orthoDir, fireDirection * sonicRange, Color.yellow);
            Debug.DrawLine((Vector2)sonicSpawnPoint.position + orthoDir, (Vector2)sonicSpawnPoint.position - orthoDir, Color.yellow);
        }
    }

    #endregion

    #region Coroutine Management

    private void StopAllBossCoroutines()
    {
        if (currentLoop != null)
        {
            StopCoroutine(currentLoop);
            currentLoop = null;
        }
        if (waterSpoutLoop != null)
        {
            StopCoroutine(waterSpoutLoop);
            waterSpoutLoop = null;
        }
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
        if (enrageMoveRoutine != null)
        {
            StopCoroutine(enrageMoveRoutine);
            enrageMoveRoutine = null;
        }
    }

    #endregion

    #region Phase Loops

    private IEnumerator Phase1Loop()
    {
        while (!isDead && !isPhase2)
        {
            while (BossIntroCutscene.IsPlaying)
            {
                yield return null;
            }
            int roll = Random.Range(0, 2); 

            if (roll == 0)
            {
                // 중첩 Coroutine 대신 인라인 yield return으로 순차 실행
                yield return PatternClaw();
            }
            else
            {
                yield return PatternDash();
                yield return RestRoutine();
            }

            yield return new WaitForSeconds(patternCooldown);
        }

        if (!isDead)
        {
            currentLoop = StartCoroutine(Phase2Loop());
        }
    }

    private IEnumerator Phase2Loop()
    {
        while (!isDead)
        {
            int roll = Random.Range(0, 3);
            if (roll == 0)
            {
                yield return PatternClaw();
            }
            else if (roll == 1)
            {
                yield return PatternDash();
                yield return PatternDash();
                yield return RestRoutine();
            }
            else
            {
                yield return PatternSonicApproach();
            }

            yield return new WaitForSeconds(patternCooldown);
        }
    }

    private IEnumerator Phase3Loop()
    {
        // 패턴 시작과 동시에 무적 전개 및 기존 공격 상태 정리
        isInvincible = true;
        if (clawHitbox != null) clawHitbox.enabled = false;
        if (dashHitbox != null) dashHitbox.enabled = false;
        dashVfx?.StopDash(clearImmediately: true);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        transform.rotation = Quaternion.identity;

        yield return PatternEnrageDesperationSequence();

        // 발악 패턴 종료 후 무적 해제 및 일반 패턴 복귀
        isInvincible = false;

        if (!isDead)
        {
            currentLoop = StartCoroutine(Phase2Loop());
        }
    }

    #endregion

    #region Pattern: Claw (할퀴기)

    private IEnumerator PatternClaw()
    {
        if (player == null || rb == null)
        {
            yield break;
        }

        Vector2 dir = ((Vector2)player.position - rb.position).normalized;

        Vector2 targetPos = (Vector2)player.position - (dir * 0.4f) + (Vector2.up * clawAttackYOffset);

        bool attackStarted = false;

        while (!isDead && Vector2.Distance(rb.position, targetPos) > 0.1f)
        {
            float distance = Vector2.Distance(rb.position, targetPos);

            // 목표에 거의 도착했을 때 공격 시작
            if (!attackStarted && distance < 2.5f)
            {
                attackStarted = true;
                if (anim != null)
                {
                    anim.SetTrigger(AnimClaw);
                }
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.PlaySFX(AudioManager.SFX.BlueBossClaw);
                }
            }

            rb.MovePosition(Vector2.MoveTowards(rb.position, targetPos, moveSpeed * 2f * Time.fixedDeltaTime));

            yield return new WaitForFixedUpdate();
        }
        
        rb.MovePosition(targetPos);

        // 혹시 너무 가까워서 위 조건을 못 탔을 경우
        if (!attackStarted && anim != null)
        {
            anim.SetTrigger(AnimClaw);
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX(AudioManager.SFX.BlueBossClaw);
            }
        }

        yield return new WaitForSeconds(GetAnimLength("Claw"));
    }

    #endregion

    #region Pattern: Dash (돌진)

    private IEnumerator PatternDash()
    {
        if (player == null || rb == null)
        {
            yield break;
        }

        if (anim != null)
        {
            anim.SetTrigger(AnimDashStart);
        }
        yield return new WaitForSeconds(0.2f);

        Vector2 dashDir = ((Vector2)player.position - rb.position).normalized; // 플레이어 방향
        float dashDistance = 20f;
        float remainedDistance = dashDistance;

        if (anim != null)
        {
            anim.SetTrigger(AnimDash);
        }
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.BlueBossDash);
        }
        if (dashHitbox != null)
        {
            dashHitbox.enabled = true;
        }
        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
        }
        dashVfx?.StartDash(dashDir, isEnrage: false);

        while (!isDead && remainedDistance > 0f)
        {
            float moveStep = dashSpeed * Time.fixedDeltaTime;
            if (moveStep > remainedDistance)
            {
                moveStep = remainedDistance;
            }

            Physics2D.queriesHitTriggers = true;
            RaycastHit2D[] hits = Physics2D.RaycastAll(rb.position, dashDir, moveStep + 0.6f);

            foreach (var hit in hits)
            {
                bool isWall = false;
                if (mapBoundaryColliders != null)
                {
                    foreach (var wallCollider in mapBoundaryColliders)
                    {
                        if (wallCollider != null && hit.collider == wallCollider)
                        {
                            isWall = true;
                            break;
                        }
                    }
                }

                if (isWall)
                {
                    // 벽에 부딪혔을때 반사각 계산
                    Vector3 reflectDir = Vector3.Reflect(dashDir, hit.normal);
                    dashDir = reflectDir.normalized;

                    // 끼임 방지
                    rb.position = hit.point + hit.normal * 0.1f;
                    dashVfx?.OnWallBounce(hit.point, hit.normal, dashDir);
                    break; // 다른 충돌체 검사 중단
                }
            }

            // 이동 처리
            Vector2 nextPos = rb.position + dashDir * moveStep;
            if (bossFlip != null)
            {
                bossFlip.moveDirection = dashDir;
            }
            rb.MovePosition(nextPos);

            remainedDistance -= moveStep;
            yield return new WaitForFixedUpdate();
        }

        if (dashHitbox != null)
        {
            dashHitbox.enabled = false;
        }
        dashVfx?.StopDash(clearImmediately: false);
        if (anim != null)
        {
            anim.SetTrigger(AnimDashEnd);
        }
        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Player;
        }
        yield return new WaitForSeconds(0.3f);
    }

    #endregion

    #region Pattern: Laser (레이저/소닉)

    private IEnumerator PatternSonicApproach()
    {
        if (player == null || rb == null)
        {
            yield break;
        }

        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Player; // 플레이어 바라보기
        }

        while (!isDead)
        {
            // 플레이어 대각선 위 목표 위치 (플레이어 반대편 위)
            float offsetX = (bossFlip != null && bossFlip.isFacingRight) ? -sonicRange * 0.7f : sonicRange * 0.7f;
            float offsetY = sonicRange * 0.7f;
            Vector2 targetPos = (Vector2)player.position + new Vector2(offsetX, offsetY);

            rb.MovePosition(Vector2.MoveTowards(rb.position, targetPos, moveSpeed * 1.8f * Time.fixedDeltaTime));

            if (IsPlayerInLaserRange())
            {
                if (anim != null)
                {
                    anim.SetTrigger(AnimSonic);
                }
                yield return new WaitForSeconds(GetAnimLength("Sonic"));
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }

    public void SonicFire()
    {
        if (sonicSpawnPoint == null || bossFlip == null)
        {
            return;
        }

        Vector2 spawnPos = sonicSpawnPoint.position;

        Quaternion rot = bossFlip.isFacingRight ? Quaternion.Euler(0f, 0f, sonicAngle) : Quaternion.Euler(0f, 180f, sonicAngle); // Y 180으로 뒤집기

        GameObject laser = PoolingManager.Instance.Get(sonicPoolKey, spawnPos, rot);
        
        Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
        Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;

        if (laser != null)
        {
            laser.transform.localScale = Vector3.one;

            // 레이저 자식 컴포넌트 가져와서 초기화
            BlueLaser laserScript = laser.GetComponentInChildren<BlueLaser>();
            if (laserScript != null)
            {
                laserScript.InitializeLaser(fireDirection, sonicRange, sonicDamage, playerLayer);
            }
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.BlueBossLaser);
        }
    }

    private bool IsPlayerInLaserRange()
    {
        if (player == null || sonicSpawnPoint == null || bossFlip == null)
        {
            return false;
        }

        Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
        float actualAngle = bossFlip.isFacingRight ? -sonicAngle : sonicAngle;
        Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;

        Vector2 boxSize = new Vector2(0.1f, sonicThickness); // 진행방향 길이는 최소화하고 폭을 설정
        RaycastHit2D hit = Physics2D.BoxCast(sonicSpawnPoint.position, boxSize, actualAngle, fireDirection, sonicRange, playerLayer);

        return hit.collider != null && hit.collider.CompareTag("Player");
    }

    #endregion

    #region Pattern: Water Spout (물기둥)

    private IEnumerator WaterSpoutLoop()
    {
        while (BossIntroCutscene.IsPlaying)
        {
            yield return null;
        }

        if (waterSpoutInitialDelay > 0f)
        {
            yield return new WaitForSeconds(waterSpoutInitialDelay);
        }

        while (!isDead)
        {
            while (BossIntroCutscene.IsPlaying)
            {
                yield return null;
            }

            if (player == null)
            {
                yield return new WaitForSeconds(waterSpoutInterval);
                continue;
            }

            float randomX = player.position.x + Random.Range(-waterSpoutSpread, waterSpoutSpread);

            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(randomX,-98f),
                Vector2.down,              
                50f,                     
                groundLayer
            );

            if (hit.collider != null)
            {
                GameObject spoutObj = PoolingManager.Instance.Get(waterSpoutPoolKey, hit.point + Vector2.up * waterSpoutYOffset, Quaternion.identity);
                if (spoutObj == null)
                {
                    Debug.LogWarning("Failed to pool water spout object!");
                }
            }

            yield return new WaitForSeconds(waterSpoutInterval);
        }
    }

    #endregion

    #region Pattern: Rest (휴식)

    private IEnumerator RestRoutine()
    {
        if (restPoints == null || restPoints.Length == 0)
        {
            yield break;
        }

        // 유효한 포인트 필터링
        List<Transform> validRestPoints = new List<Transform>();
        foreach (var pt in restPoints)
        {
            if (pt != null)
            {
                validRestPoints.Add(pt);
            }
        }

        if (validRestPoints.Count == 0)
        {
            yield break;
        }

        Transform restPoint = validRestPoints[Random.Range(0, validRestPoints.Count)];
        if (restPoint == null)
        {
            yield break;
        }

        Vector2 restPos = (Vector2)restPoint.position + restPointOffset;
        yield return MoveToPosition(restPos, moveSpeed);

        if (anim != null)
        {
            anim.SetTrigger(AnimRestStart);
        }

        yield return new WaitForSeconds(GetAnimLength("RestStart"));

        restJumpTriggered = false;

        if (anim != null)
        {
            anim.SetTrigger(Random.value > 0.5f ? AnimRest : AnimRest2);
        }

        yield return new WaitUntil(() => restJumpTriggered);

        Vector2 aboveRest = (Vector2)restPoint.position + Vector2.up * restJumpHeight;
        yield return MoveToPosition(aboveRest, moveSpeed);
    }

    #endregion

    #region Pattern: Phase 3 Enrage (발악)

    private IEnumerator PatternEnrageDesperationSequence()
    {
        if (player == null || rb == null || !TryComputeEnrageMapBounds(out Bounds mapBounds))
        {
            yield break;
        }

        Vector2 mapCenter = mapBounds.center;
        SpriteRenderer bossSprite = GetComponentInChildren<SpriteRenderer>();
        if (bossSprite != null)
        {
            Color c = bossSprite.color;
            c.a = 1f;
            bossSprite.color = c;
        }

        int dashCount = Mathf.Max(2, enrageDashCount);
        int setCount = Mathf.Max(1, enrageSetCount);

        // 1. 무적 상태로 맵 중앙에 집결 후 기 모으기
        if (Vector2.Distance(rb.position, mapCenter) > 0.15f)
        {
            yield return MoveSingleBossRoutine(mapCenter, 0.30f, useDashVfx: true);
        }

        if (isDead) yield break;

        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Player;
        }
        if (anim != null)
        {
            anim.SetTrigger(AnimDashStart);
        }
        dashVfx?.EmitBurstPulse(16, 18, 1);
        yield return new WaitForSeconds(0.25f);

        if (isDead) yield break;

        // 2. 첫 번째 갈래 경로 계산 및 갈래 수만큼(본체 1 + 분신 dashCount-1) 본체에서 분신 생성
        BuildEnrageDashPaths(mapBounds, dashCount, out Vector2[] startPositions, out Vector2[] targetPositions, out bool[] dashFacesRight);

        List<EnrageDashPhantom> phantoms = new List<EnrageDashPhantom>(dashCount - 1);
        for (int i = 1; i < dashCount; i++)
        {
            Vector2 dir = (targetPositions[i] - startPositions[i]).normalized;
            EnrageDashPhantom phantom = CreateEnrageDashPhantom(i, mapCenter, targetPositions[i], dir, dashFacesRight[i], bossSprite, initialAlpha: 0f);
            PlayPhantomAnimation(phantom, "DashStart");
            phantoms.Add(phantom);
        }

        // 3. 본체와 분신들이 양쪽으로 겹치지 않게 충분히 퍼지며 등장
        dashVfx?.EmitBurstPulse(22, 24, 2);
        Vector2[] lateralTargets = ComputeLateralSpreadTargets(mapCenter, mapBounds, startPositions, bossSprite);
        yield return MoveBossAndPhantomsRoutine(
            lateralTargets,
            phantoms,
            duration: 0.48f,
            easeIn: false,
            fadePhantomsIn: true,
            fadePhantomsOut: false,
            orientToTargets: true);

        if (isDead)
        {
            ClearActiveDashPhantoms();
            yield break;
        }

        yield return new WaitForSeconds(0.25f);

        // 4. 양쪽에 퍼진 상태에서 발악 패턴(총 setCount 세트) 실행
        for (int setIdx = 0; setIdx < setCount; setIdx++)
        {
            if (isDead)
            {
                ClearActiveDashPhantoms();
                yield break;
            }

            bool isFollowUpSet = setIdx > 0;
            if (isFollowUpSet)
            {
                yield return new WaitForSeconds(0.25f);
                if (isDead)
                {
                    ClearActiveDashPhantoms();
                    yield break;
                }
                BuildEnrageDashPaths(mapBounds, dashCount, out startPositions, out targetPositions, out dashFacesRight);
            }

            for (int p = 0; p < phantoms.Count; p++)
            {
                EnrageDashPhantom phantom = phantoms[p];
                int laneIdx = p + 1;
                phantom.targetPosition = targetPositions[laneIdx];
                phantom.dashDir = (targetPositions[laneIdx] - startPositions[laneIdx]).normalized;
                phantom.arrived = false;
                phantoms[p] = phantom;
            }

            bool repositionDone = false;
            if (isFollowUpSet)
            {
                // 다음 세트 위치 결정 시에는 보이지 않는 상태에서 각 시작 위치로 이동·정렬
                TeleportBossAndPhantoms(startPositions, phantoms, dashFacesRight);
                repositionDone = true;
            }
            else
            {
                // 첫 세트는 양쪽으로 퍼진 상태에서 경고선이 깔리는 동안 각 갈래 시작 지점으로 이동
                float repositionDuration = Mathf.Clamp(dashCount * warningInterval, 0.35f, 0.55f);
                if (enrageMoveRoutine != null)
                {
                    StopCoroutine(enrageMoveRoutine);
                }
                enrageMoveRoutine = StartCoroutine(MoveBossAndPhantomsWithCallback(
                    startPositions,
                    phantoms,
                    repositionDuration,
                    easeIn: false,
                    fadePhantomsIn: false,
                    fadePhantomsOut: false,
                    orientToTargets: true,
                    () => repositionDone = true));
            }

            BlueBossWarningLine[] spawnedWarnings = new BlueBossWarningLine[dashCount];
            float maxWarningSafetyDuration = Mathf.Max(dashWarningDuration * 4f, dashCount * warningInterval + 6f);

            for (int i = 0; i < dashCount; i++)
            {
                Vector2 pathDir = (targetPositions[i] - startPositions[i]).normalized;
                float distance = Vector2.Distance(startPositions[i], targetPositions[i]);
                float angle = Mathf.Atan2(pathDir.y, pathDir.x) * Mathf.Rad2Deg;
                Quaternion lineRotation = Quaternion.Euler(0, 0, angle);

                ComputeDashHitboxSweep(pathDir, dashFacesRight[i] ? 1f : -1f, out float hitboxThickness, out float normalOffset);
                Vector2 pathNormal = new Vector2(-pathDir.y, pathDir.x);
                Vector2 centerPos = ((startPositions[i] + targetPositions[i]) * 0.5f) + (pathNormal * normalOffset);

                GameObject line = PoolingManager.Instance.Get(warningLinePoolKey, centerPos, lineRotation);
                if (line != null)
                {
                    line.transform.localScale = Vector3.one;
                    if (line.TryGetComponent(out BlueBossWarningLine warningLineScript))
                    {
                        spawnedWarnings[i] = warningLineScript;
                        activeWarningLines.Add(warningLineScript);
                        warningLineScript.ActivateWarning(distance, hitboxThickness, maxWarningSafetyDuration, warningLinePoolKey);
                    }
                }

                if (warningInterval > 0f)
                {
                    yield return new WaitForSeconds(warningInterval);
                }
            }

            while (!repositionDone && !isDead)
            {
                yield return null;
            }
            enrageMoveRoutine = null;

            if (isDead)
            {
                ClearActiveDashPhantoms();
                yield break;
            }

            // 돌진 방향 정렬 및 록온 펄스
            Vector2 primaryDashDir = (targetPositions[0] - startPositions[0]).normalized;
            if (bossFlip != null)
            {
                bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
                bossFlip.moveDirection = primaryDashDir;
            }
            for (int p = 0; p < phantoms.Count; p++)
            {
                SetPhantomFacing(phantoms[p], dashFacesRight[p + 1]);
                PlayPhantomAnimation(phantoms[p], "Dash");
            }
            if (anim != null)
            {
                anim.SetTrigger(AnimDash);
            }

            for (int i = 0; i < dashCount; i++)
            {
                spawnedWarnings[i]?.TriggerLockOnPulse();
            }

            if (isFollowUpSet)
            {
                yield return FadeBossAndPhantomsAlpha(bossSprite, phantoms, toVisible: true, duration: 0.14f);
                yield return new WaitForSeconds(0.14f);
            }
            else
            {
                yield return new WaitForSeconds(0.28f);
            }

            if (isDead)
            {
                ClearActiveDashPhantoms();
                yield break;
            }

            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX(AudioManager.SFX.BlueBossDash);
            }

            if (dashHitbox != null) dashHitbox.enabled = true;
            for (int p = 0; p < phantoms.Count; p++)
            {
                if (phantoms[p].hitbox != null)
                {
                    phantoms[p].hitbox.enabled = true;
                }
            }

            for (int i = 0; i < dashCount; i++)
            {
                if (spawnedWarnings[i] != null)
                {
                    activeWarningLines.Remove(spawnedWarnings[i]);
                    spawnedWarnings[i].DismissWarning();
                    spawnedWarnings[i] = null;
                }
            }

            dashVfx?.StartDash(primaryDashDir, isEnrage: true);
            for (int p = 0; p < phantoms.Count; p++)
            {
                phantoms[p].vfx?.StartDash(phantoms[p].dashDir, isEnrage: true);
            }

            // 동시 전력 돌진
            float enrageDashSpeed = dashSpeed * 3.5f;
            bool bossArrived = false;

            while (!isDead)
            {
                float step = enrageDashSpeed * Time.fixedDeltaTime;

                if (!bossArrived)
                {
                    if (Vector2.Distance(rb.position, targetPositions[0]) <= 0.2f)
                    {
                        rb.MovePosition(targetPositions[0]);
                        bossArrived = true;
                        dashVfx?.StopDash(clearImmediately: false);
                        if (dashHitbox != null) dashHitbox.enabled = false;
                    }
                    else
                    {
                        rb.MovePosition(Vector2.MoveTowards(rb.position, targetPositions[0], step));
                    }
                }

                bool allPhantomsArrived = true;
                for (int p = 0; p < phantoms.Count; p++)
                {
                    EnrageDashPhantom phantom = phantoms[p];
                    if (phantom.arrived || phantom.rb == null)
                    {
                        continue;
                    }

                    if (Vector2.Distance(phantom.rb.position, phantom.targetPosition) <= 0.2f)
                    {
                        phantom.rb.MovePosition(phantom.targetPosition);
                        phantom.arrived = true;
                        phantom.vfx?.StopDash(clearImmediately: false);
                        if (phantom.hitbox != null) phantom.hitbox.enabled = false;
                        phantoms[p] = phantom;
                    }
                    else
                    {
                        phantom.rb.MovePosition(Vector2.MoveTowards(phantom.rb.position, phantom.targetPosition, step));
                        allPhantomsArrived = false;
                    }
                }

                if (bossArrived && allPhantomsArrived)
                {
                    break;
                }

                yield return new WaitForFixedUpdate();
            }

            if (dashHitbox != null) dashHitbox.enabled = false;
            dashVfx?.StopDash(clearImmediately: false);
            for (int p = 0; p < phantoms.Count; p++)
            {
                if (phantoms[p].hitbox != null) phantoms[p].hitbox.enabled = false;
                phantoms[p].vfx?.StopDash(clearImmediately: false);
                PlayPhantomAnimation(phantoms[p], "Idle");
            }

            // 마지막 세트가 아니라면 다음 위치가 정해지는 동안 본체와 분신을 숨김
            if (setIdx < setCount - 1)
            {
                yield return FadeBossAndPhantomsAlpha(bossSprite, phantoms, toVisible: false, duration: 0.14f);
            }
        }

        if (isDead)
        {
            ClearActiveDashPhantoms();
            yield break;
        }

        // 5. 발악 패턴 종료 후 본체와 모든 분신이 다시 중앙 본체로 모여 합쳐지는 연출
        yield return new WaitForSeconds(0.20f);
        if (isDead)
        {
            ClearActiveDashPhantoms();
            yield break;
        }

        Vector2[] convergeTargets = new Vector2[dashCount];
        for (int i = 0; i < dashCount; i++)
        {
            convergeTargets[i] = mapCenter;
        }

        if (Vector2.Distance(rb.position, mapCenter) > 0.1f)
        {
            dashVfx?.StartDash((mapCenter - rb.position).normalized, isEnrage: false);
        }
        for (int p = 0; p < phantoms.Count; p++)
        {
            EnrageDashPhantom phantom = phantoms[p];
            if (phantom.rb != null)
            {
                Vector2 toCenter = (mapCenter - phantom.rb.position).normalized;
                PlayPhantomAnimation(phantom, "Dash");
                phantom.vfx?.StartDash(toCenter, isEnrage: false);
            }
        }

        yield return MoveBossAndPhantomsRoutine(
            convergeTargets,
            phantoms,
            duration: 0.48f,
            easeIn: true,
            fadePhantomsIn: false,
            fadePhantomsOut: true,
            orientToTargets: true);

        dashVfx?.StopDash(clearImmediately: false);
        ClearActiveDashPhantoms();

        rb.position = mapCenter;
        transform.rotation = Quaternion.identity;
        if (dashHitbox != null) dashHitbox.enabled = false;

        dashVfx?.EmitBurstPulse(24, 26, 2);
        if (anim != null)
        {
            anim.SetTrigger(AnimDashEnd);
        }
        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Player;
        }

        yield return new WaitForSeconds(0.45f);
    }

    private bool TryComputeEnrageMapBounds(out Bounds mapBounds)
    {
        mapBounds = new Bounds();
        if (mapBoundaryColliders == null || mapBoundaryColliders.Length == 0)
        {
            return false;
        }

        bool boundsInitialized = false;
        foreach (var col in mapBoundaryColliders)
        {
            if (col == null) continue;

            if (!boundsInitialized)
            {
                mapBounds = col.bounds;
                boundsInitialized = true;
            }
            else
            {
                mapBounds.Encapsulate(col.bounds);
            }
        }

        if (!boundsInitialized)
        {
            Debug.LogWarning("No valid boundary colliders found for Blue Boss!");
        }
        return boundsInitialized;
    }

    private void BuildEnrageDashPaths(
        Bounds mapBounds,
        int dashCount,
        out Vector2[] startPositions,
        out Vector2[] targetPositions,
        out bool[] dashFacesRight)
    {
        float actualMinX = mapBounds.min.x;
        float actualMaxX = mapBounds.max.x;
        float actualMinY = mapBounds.min.y;
        float actualMaxY = mapBounds.max.y;
        Vector2 mapCenter = mapBounds.center;

        startPositions = new Vector2[dashCount];
        targetPositions = new Vector2[dashCount];
        dashFacesRight = new bool[dashCount];

        List<int> dashIndices = new List<int>(dashCount);
        for (int i = 0; i < dashCount; i++)
        {
            dashIndices.Add(i);
        }
        for (int k = dashIndices.Count - 1; k > 0; k--)
        {
            int r = Random.Range(0, k + 1);
            int tmp = dashIndices[k];
            dashIndices[k] = dashIndices[r];
            dashIndices[r] = tmp;
        }

        float baseAngleInterval = 180f / dashCount;
        float randomAngleOffset = Random.Range(0f, 25f);

        for (int i = 0; i < dashCount; i++)
        {
            float finalAngle = (baseAngleInterval * i) + randomAngleOffset + Random.Range(-3f, 3f);
            Vector2 dir = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));

            float tPos = GetRayIntersectionDistance(mapCenter, dir, actualMinX, actualMaxX, actualMinY, actualMaxY);
            float tNeg = GetRayIntersectionDistance(mapCenter, -dir, actualMinX, actualMaxX, actualMinY, actualMaxY);

            Vector2 sWall = mapCenter - dir * tNeg;
            Vector2 tWall = mapCenter + dir * tPos;

            float bossOffset = 0.7f;
            Vector2 s = sWall + dir * bossOffset;
            Vector2 t = tWall - dir * bossOffset;

            int targetIdx = dashIndices[i];
            if (targetIdx % 2 == 0)
            {
                if (Mathf.Abs(dir.x) < 0.1f)
                {
                    if (s.y < t.y) { startPositions[targetIdx] = s; targetPositions[targetIdx] = t; }
                    else { startPositions[targetIdx] = t; targetPositions[targetIdx] = s; }
                }
                else
                {
                    if (s.x < t.x) { startPositions[targetIdx] = s; targetPositions[targetIdx] = t; }
                    else { startPositions[targetIdx] = t; targetPositions[targetIdx] = s; }
                }
            }
            else
            {
                if (Mathf.Abs(dir.x) < 0.1f)
                {
                    if (s.y > t.y) { startPositions[targetIdx] = s; targetPositions[targetIdx] = t; }
                    else { startPositions[targetIdx] = t; targetPositions[targetIdx] = s; }
                }
                else
                {
                    if (s.x > t.x) { startPositions[targetIdx] = s; targetPositions[targetIdx] = t; }
                    else { startPositions[targetIdx] = t; targetPositions[targetIdx] = s; }
                }
            }
        }

        bool currentFacingRight = bossFlip != null ? bossFlip.isFacingRight : transform.localScale.x >= 0f;
        for (int i = 0; i < dashCount; i++)
        {
            Vector2 pathDir = (targetPositions[i] - startPositions[i]).normalized;
            dashFacesRight[i] = Mathf.Abs(pathDir.x) > 0.0001f ? pathDir.x > 0f : currentFacingRight;
        }
    }

    private Vector2[] ComputeLateralSpreadTargets(Vector2 mapCenter, Bounds mapBounds, Vector2[] startPositions, SpriteRenderer bossSprite)
    {
        int count = startPositions.Length;
        Vector2[] targets = new Vector2[count];

        // 시작 위치 X 기준으로 좌우 절반씩 양쪽 측면으로 퍼지도록 배치
        List<int> sortedByX = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            sortedByX.Add(i);
        }
        sortedByX.Sort((a, b) => startPositions[a].x.CompareTo(startPositions[b].x));

        Vector2 bodySize = bossSprite != null
            ? new Vector2(Mathf.Max(6f, bossSprite.bounds.size.x), Mathf.Max(5f, bossSprite.bounds.size.y))
            : new Vector2(8.6f, 7.2f);

        float desiredLateralX = Mathf.Max(enrageSplitLateralOffset, bodySize.x * 1.45f);
        float safeLateralX = Mathf.Min(desiredLateralX, Mathf.Max(bodySize.x * 0.75f, mapBounds.extents.x * 0.56f));

        float desiredStepY = Mathf.Max(enrageSplitVerticalStep, bodySize.y * 1.35f);
        float safeMaxYOffset = Mathf.Max(bodySize.y * 0.6f, mapBounds.extents.y * 0.72f);

        int leftCount = count / 2;
        int rightCount = count - leftCount;

        float ResolveSideStepY(int sideCount)
        {
            if (sideCount <= 1) return 0f;
            float maxPossibleStep = (safeMaxYOffset * 2f) / (sideCount - 1);
            return Mathf.Min(desiredStepY, maxPossibleStep);
        }

        float leftStepY = ResolveSideStepY(leftCount);
        for (int i = 0; i < leftCount; i++)
        {
            int idx = sortedByX[i];
            float yCenterIndex = i - (leftCount - 1) * 0.5f;
            targets[idx] = mapCenter + new Vector2(-safeLateralX, yCenterIndex * leftStepY);
        }

        float rightStepY = ResolveSideStepY(rightCount);
        for (int i = 0; i < rightCount; i++)
        {
            int idx = sortedByX[leftCount + i];
            float yCenterIndex = i - (rightCount - 1) * 0.5f;
            targets[idx] = mapCenter + new Vector2(safeLateralX, yCenterIndex * rightStepY);
        }

        return targets;
    }

    private void TeleportBossAndPhantoms(Vector2[] startPositions, List<EnrageDashPhantom> phantoms, bool[] dashFacesRight)
    {
        if (rb != null && startPositions.Length > 0)
        {
            Vector2 bossPos = startPositions[0];
            rb.position = bossPos;
            transform.position = new Vector3(bossPos.x, bossPos.y, transform.position.z);

            Vector2 primaryDir = dashFacesRight.Length > 0 && dashFacesRight[0]
                ? Vector2.right
                : Vector2.left;
            if (bossFlip != null)
            {
                bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
                bossFlip.moveDirection = primaryDir;
            }
        }

        for (int p = 0; p < phantoms.Count; p++)
        {
            int laneIdx = p + 1;
            if (laneIdx >= startPositions.Length) continue;

            Vector2 pos = startPositions[laneIdx];
            if (phantoms[p].rb != null)
            {
                phantoms[p].rb.position = pos;
            }
            if (phantoms[p].root != null)
            {
                phantoms[p].root.transform.position = new Vector3(pos.x, pos.y, transform.position.z);
            }
            if (laneIdx < dashFacesRight.Length)
            {
                SetPhantomFacing(phantoms[p], dashFacesRight[laneIdx]);
            }
        }
    }

    private IEnumerator FadeBossAndPhantomsAlpha(
        SpriteRenderer bossSprite,
        List<EnrageDashPhantom> phantoms,
        bool toVisible,
        float duration)
    {
        float bossStart = bossSprite != null ? bossSprite.color.a : (toVisible ? 0f : 1f);
        float bossTarget = toVisible ? 1f : 0f;

        float[] phantomStarts = new float[phantoms.Count];
        float phantomTarget = toVisible ? PhantomTintColor.a : 0f;
        for (int p = 0; p < phantoms.Count; p++)
        {
            phantomStarts[p] = phantoms[p].spriteRenderer != null ? phantoms[p].spriteRenderer.color.a : (toVisible ? 0f : PhantomTintColor.a);
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (!isDead && elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);

            if (bossSprite != null)
            {
                Color c = bossSprite.color;
                c.a = Mathf.Lerp(bossStart, bossTarget, t);
                bossSprite.color = c;
            }

            for (int p = 0; p < phantoms.Count; p++)
            {
                SpriteRenderer sr = phantoms[p].spriteRenderer;
                if (sr == null) continue;
                Color c = sr.color;
                c.a = Mathf.Lerp(phantomStarts[p], phantomTarget, t);
                sr.color = c;
            }

            yield return null;
        }

        if (bossSprite != null)
        {
            Color c = bossSprite.color;
            c.a = bossTarget;
            bossSprite.color = c;
        }
        for (int p = 0; p < phantoms.Count; p++)
        {
            SpriteRenderer sr = phantoms[p].spriteRenderer;
            if (sr == null) continue;
            Color c = sr.color;
            c.a = phantomTarget;
            sr.color = c;
        }
    }

    private IEnumerator MoveSingleBossRoutine(Vector2 target, float duration, bool useDashVfx)
    {
        if (rb == null) yield break;

        Vector2 start = rb.position;
        Vector2 dir = target - start;
        if (dir.sqrMagnitude > 0.0001f && bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
            bossFlip.moveDirection = dir.normalized;
        }

        if (useDashVfx && dir.sqrMagnitude > 0.25f)
        {
            if (anim != null) anim.SetTrigger(AnimDash);
            dashVfx?.StartDash(dir.normalized, isEnrage: false);
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (!isDead && elapsed < safeDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            rb.MovePosition(Vector2.Lerp(start, target, ease));
            yield return new WaitForFixedUpdate();
        }

        if (!isDead)
        {
            rb.MovePosition(target);
        }
        dashVfx?.StopDash(clearImmediately: false);
    }

    private IEnumerator MoveBossAndPhantomsWithCallback(
        Vector2[] targetSlots,
        List<EnrageDashPhantom> phantoms,
        float duration,
        bool easeIn,
        bool fadePhantomsIn,
        bool fadePhantomsOut,
        bool orientToTargets,
        System.Action onCompleted)
    {
        yield return MoveBossAndPhantomsRoutine(
            targetSlots,
            phantoms,
            duration,
            easeIn,
            fadePhantomsIn,
            fadePhantomsOut,
            orientToTargets);
        onCompleted?.Invoke();
    }

    private IEnumerator MoveBossAndPhantomsRoutine(
        Vector2[] targetSlots,
        List<EnrageDashPhantom> phantoms,
        float duration,
        bool easeIn,
        bool fadePhantomsIn,
        bool fadePhantomsOut,
        bool orientToTargets)
    {
        if (rb == null || targetSlots == null || targetSlots.Length == 0)
        {
            yield break;
        }

        Vector2 bossStart = rb.position;
        Vector2 bossTarget = targetSlots[0];
        Vector2 bossMoveDir = bossTarget - bossStart;
        if (orientToTargets && bossMoveDir.sqrMagnitude > 0.0001f && bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
            bossFlip.moveDirection = bossMoveDir.normalized;
        }

        Vector2[] phantomStarts = new Vector2[phantoms.Count];
        float[] startAlphas = new float[phantoms.Count];
        for (int p = 0; p < phantoms.Count; p++)
        {
            phantomStarts[p] = phantoms[p].rb != null ? phantoms[p].rb.position : Vector2.zero;
            startAlphas[p] = phantoms[p].spriteRenderer != null ? phantoms[p].spriteRenderer.color.a : PhantomTintColor.a;

            int slotIdx = p + 1;
            if (orientToTargets && slotIdx < targetSlots.Length)
            {
                Vector2 moveDir = targetSlots[slotIdx] - phantomStarts[p];
                if (Mathf.Abs(moveDir.x) > 0.01f)
                {
                    SetPhantomFacing(phantoms[p], moveDir.x > 0f);
                }
            }
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);

        while (!isDead && elapsed < safeDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float ease = easeIn
                ? t * t * t
                : 1f - Mathf.Pow(1f - t, 3f);

            rb.MovePosition(Vector2.Lerp(bossStart, bossTarget, ease));

            for (int p = 0; p < phantoms.Count; p++)
            {
                int slotIdx = p + 1;
                if (slotIdx >= targetSlots.Length) continue;

                if (phantoms[p].rb != null)
                {
                    phantoms[p].rb.MovePosition(Vector2.Lerp(phantomStarts[p], targetSlots[slotIdx], ease));
                }

                if ((fadePhantomsIn || fadePhantomsOut) && phantoms[p].spriteRenderer != null)
                {
                    float targetAlpha = fadePhantomsIn ? PhantomTintColor.a : 0.2f;
                    float alphaT = fadePhantomsIn ? Mathf.Clamp01(t / 0.35f) : t;
                    Color c = phantoms[p].spriteRenderer.color;
                    c.a = Mathf.Lerp(startAlphas[p], targetAlpha, alphaT);
                    phantoms[p].spriteRenderer.color = c;
                }
            }

            yield return new WaitForFixedUpdate();
        }

        if (!isDead)
        {
            rb.MovePosition(bossTarget);
            for (int p = 0; p < phantoms.Count; p++)
            {
                int slotIdx = p + 1;
                if (slotIdx < targetSlots.Length && phantoms[p].rb != null)
                {
                    phantoms[p].rb.MovePosition(targetSlots[slotIdx]);
                }

                if (fadePhantomsIn && phantoms[p].spriteRenderer != null)
                {
                    Color c = phantoms[p].spriteRenderer.color;
                    c.a = PhantomTintColor.a;
                    phantoms[p].spriteRenderer.color = c;
                }
            }
        }
    }

    private float GetRayIntersectionDistance(Vector2 center, Vector2 dir, float minX, float maxX, float minY, float maxY)
    {
        float tMaxX = float.PositiveInfinity;
        float tMaxY = float.PositiveInfinity;

        if (dir.x > 0.0001f)
        {
            tMaxX = (maxX - center.x) / dir.x;
        }
        else if (dir.x < -0.0001f)
        {
            tMaxX = (minX - center.x) / dir.x;
        }

        if (dir.y > 0.0001f)
        {
            tMaxY = (maxY - center.y) / dir.y;
        }
        else if (dir.y < -0.0001f)
        {
            tMaxY = (minY - center.y) / dir.y;
        }

        return Mathf.Min(tMaxX, tMaxY);
    }

    private void ComputeDashHitboxSweep(Vector2 pathDir, float predictedFacingSign, out float thickness, out float normalOffset)
    {
        thickness = 1f;
        normalOffset = 0f;

        if (dashHitbox == null)
        {
            return;
        }

        Vector2 normalizedDir = pathDir.sqrMagnitude > 0.0001f ? pathDir.normalized : Vector2.right;
        Vector2 pathNormal = new Vector2(-normalizedDir.y, normalizedDir.x);

        Vector3 rootLossy = transform.lossyScale;
        float scaleX = Mathf.Abs(rootLossy.x) * (predictedFacingSign >= 0f ? 1f : -1f);
        float scaleY = rootLossy.y;

        bool hasSample = false;
        float minProj = float.PositiveInfinity;
        float maxProj = float.NegativeInfinity;

        void AccumulateBossLocalPoint(Vector2 bossLocalPt)
        {
            Vector2 worldOffset = new Vector2(bossLocalPt.x * scaleX, bossLocalPt.y * scaleY);
            float proj = Vector2.Dot(worldOffset, pathNormal);
            if (!hasSample)
            {
                hasSample = true;
                minProj = proj;
                maxProj = proj;
            }
            else
            {
                if (proj < minProj) minProj = proj;
                if (proj > maxProj) maxProj = proj;
            }
        }

        Vector2 ColliderLocalToBossLocal(Vector2 colliderLocalPt)
        {
            if (dashHitbox.transform == transform)
            {
                return colliderLocalPt;
            }
            Vector3 worldPt = dashHitbox.transform.TransformPoint(colliderLocalPt);
            Vector3 bossLocal = transform.InverseTransformPoint(worldPt);
            return new Vector2(bossLocal.x, bossLocal.y);
        }

        if (dashHitbox is PolygonCollider2D poly)
        {
            Vector2 polyOffset = poly.offset;
            for (int p = 0; p < poly.pathCount; p++)
            {
                Vector2[] pts = poly.GetPath(p);
                for (int k = 0; k < pts.Length; k++)
                {
                    AccumulateBossLocalPoint(ColliderLocalToBossLocal(pts[k] + polyOffset));
                }
            }
        }
        else if (dashHitbox is BoxCollider2D box)
        {
            Vector2 half = box.size * 0.5f;
            Vector2 c = box.offset;
            AccumulateBossLocalPoint(ColliderLocalToBossLocal(c + new Vector2(-half.x, -half.y)));
            AccumulateBossLocalPoint(ColliderLocalToBossLocal(c + new Vector2(half.x, -half.y)));
            AccumulateBossLocalPoint(ColliderLocalToBossLocal(c + new Vector2(half.x, half.y)));
            AccumulateBossLocalPoint(ColliderLocalToBossLocal(c + new Vector2(-half.x, half.y)));
        }
        else if (dashHitbox is CircleCollider2D circle)
        {
            Vector2 bossLocalCenter = ColliderLocalToBossLocal(circle.offset);
            Vector2 worldCenterOffset = new Vector2(bossLocalCenter.x * scaleX, bossLocalCenter.y * scaleY);
            float centerProj = Vector2.Dot(worldCenterOffset, pathNormal);
            Vector3 colLossy = dashHitbox.transform.lossyScale;
            float radiusScale = Mathf.Max(Mathf.Abs(colLossy.x), Mathf.Abs(colLossy.y));
            float worldRadius = circle.radius * radiusScale;
            minProj = centerProj - worldRadius;
            maxProj = centerProj + worldRadius;
            hasSample = true;
        }
        else
        {
            Bounds b = dashHitbox.bounds;
            if (b.size.sqrMagnitude > 0.0001f)
            {
                Vector2 c = (Vector2)b.center - (Vector2)transform.position;
                Vector2 ext = b.extents;
                Vector2[] corners =
                {
                    c + new Vector2(-ext.x, -ext.y),
                    c + new Vector2(ext.x, -ext.y),
                    c + new Vector2(ext.x, ext.y),
                    c + new Vector2(-ext.x, ext.y)
                };
                for (int k = 0; k < corners.Length; k++)
                {
                    float proj = Vector2.Dot(corners[k], pathNormal);
                    if (!hasSample) { hasSample = true; minProj = proj; maxProj = proj; }
                    else { if (proj < minProj) minProj = proj; if (proj > maxProj) maxProj = proj; }
                }
            }
        }

        if (hasSample)
        {
            thickness = Mathf.Max(0.1f, maxProj - minProj);
            normalOffset = (minProj + maxProj) * 0.5f;
        }
    }

    private EnrageDashPhantom CreateEnrageDashPhantom(
        int index,
        Vector2 startPos,
        Vector2 targetPos,
        Vector2 dashDir,
        bool faceRight,
        SpriteRenderer sourceSprite,
        float initialAlpha)
    {
        GameObject root = new GameObject($"BlueBossDashPhantom_{index}");
        root.transform.position = new Vector3(startPos.x, startPos.y, transform.position.z);

        Vector3 bossScale = transform.localScale;
        float absX = Mathf.Abs(bossScale.x);
        root.transform.localScale = new Vector3(faceRight ? absX : -absX, bossScale.y, bossScale.z);

        Rigidbody2D phantomRb = root.AddComponent<Rigidbody2D>();
        phantomRb.bodyType = RigidbodyType2D.Kinematic;
        phantomRb.gravityScale = 0f;
        phantomRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        phantomRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        phantomRb.constraints = RigidbodyConstraints2D.FreezeRotation;

        SpriteRenderer phantomSr = null;
        Animator phantomAnim = null;
        if (sourceSprite != null)
        {
            GameObject spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(root.transform, false);
            spriteChild.transform.localPosition = sourceSprite.transform.localPosition;
            spriteChild.transform.localRotation = sourceSprite.transform.localRotation;
            spriteChild.transform.localScale = sourceSprite.transform.localScale;

            phantomSr = spriteChild.AddComponent<SpriteRenderer>();
            phantomSr.sprite = sourceSprite.sprite;
            phantomSr.sharedMaterial = sourceSprite.sharedMaterial;
            phantomSr.sortingLayerID = sourceSprite.sortingLayerID;
            phantomSr.sortingOrder = sourceSprite.sortingOrder - 1;
            phantomSr.flipX = sourceSprite.flipX;
            phantomSr.flipY = sourceSprite.flipY;
            Color c = PhantomTintColor;
            c.a = initialAlpha;
            phantomSr.color = c;

            if (anim != null && anim.runtimeAnimatorController != null)
            {
                phantomAnim = spriteChild.AddComponent<Animator>();
                phantomAnim.runtimeAnimatorController = anim.runtimeAnimatorController;
                phantomAnim.updateMode = anim.updateMode;
                phantomAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                phantomAnim.Play("Dash", 0, 0f);
                phantomAnim.Update(0f);
            }
        }

        Collider2D phantomHitbox = null;
        if (dashHitbox != null)
        {
            GameObject hitboxGo = Instantiate(dashHitbox.gameObject, root.transform, false);
            hitboxGo.name = "DashHitBox";
            hitboxGo.transform.localPosition = dashHitbox.transform.localPosition;
            hitboxGo.transform.localRotation = dashHitbox.transform.localRotation;
            hitboxGo.transform.localScale = dashHitbox.transform.localScale;
            phantomHitbox = hitboxGo.GetComponent<Collider2D>();
            if (phantomHitbox != null)
            {
                phantomHitbox.enabled = false;
            }
        }

        BlueBossDashParticles phantomVfx = BlueBossDashParticles.Attach(root);
        activeDashPhantoms.Add(root);

        return new EnrageDashPhantom
        {
            root = root,
            rb = phantomRb,
            spriteRenderer = phantomSr,
            animator = phantomAnim,
            hitbox = phantomHitbox,
            vfx = phantomVfx,
            targetPosition = targetPos,
            dashDir = dashDir,
            arrived = false
        };
    }

    private static void PlayPhantomAnimation(EnrageDashPhantom phantom, string stateName)
    {
        if (phantom.animator == null || !phantom.animator.isActiveAndEnabled)
        {
            return;
        }
        phantom.animator.Play(stateName, 0, 0f);
    }

    private static void SetPhantomFacing(EnrageDashPhantom phantom, bool faceRight)
    {
        if (phantom.root == null) return;
        Vector3 scale = phantom.root.transform.localScale;
        float absX = Mathf.Abs(scale.x);
        scale.x = faceRight ? absX : -absX;
        phantom.root.transform.localScale = scale;
    }

    private void ClearActiveDashPhantoms()
    {
        for (int i = activeDashPhantoms.Count - 1; i >= 0; i--)
        {
            if (activeDashPhantoms[i] != null)
            {
                Destroy(activeDashPhantoms[i]);
            }
        }
        activeDashPhantoms.Clear();
    }

    private void ClearActiveWarningLines()
    {
        for (int i = activeWarningLines.Count - 1; i >= 0; i--)
        {
            BlueBossWarningLine warning = activeWarningLines[i];
            if (warning != null)
            {
                warning.DismissWarning(immediate: true);
            }
        }
        activeWarningLines.Clear();
    }

    #endregion

    #region Death & Utilities

    private void OnDisable()
    {
        if (enrageMoveRoutine != null)
        {
            StopCoroutine(enrageMoveRoutine);
            enrageMoveRoutine = null;
        }
        ClearActiveWarningLines();
        ClearActiveDashPhantoms();
        dashVfx?.StopDash(clearImmediately: true);
    }

    private void OnDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        StopAllBossCoroutines();
        ClearActiveWarningLines();
        ClearActiveDashPhantoms();

        if (clawHitbox != null)
        {
            clawHitbox.enabled = false;
        }
        if (dashHitbox != null)
        {
            dashHitbox.enabled = false;
        }
        dashVfx?.StopDash(clearImmediately: false);

        deathRoutine = StartCoroutine(DeathFallRoutine());
    }

    private IEnumerator DeathFallRoutine()
    {
        yield return null;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 2f;
            rb.linearVelocity = Vector2.zero;
        }

        if (deathCollider != null)
        {
            deathCollider.enabled = true;
        }
    }

    public void ClawHitboxOn()
    {
        if (clawHitbox != null)
        {
            clawHitbox.enabled = true;
        }
    }

    public void ClawHitboxOff()
    {
        if (clawHitbox != null)
        {
            clawHitbox.enabled = false;
        }
    }

    public void RestJumpEvent()
    {
        restJumpTriggered = true;
    }

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (isInvincible) // 발악 패턴 중 무적
        {
            return true;
        }
        return false; // 평소 대미지 정상
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
    }

    private IEnumerator MoveToPosition(Vector2 target, float speed)
    {
        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
        }

        while (!isDead && rb != null && Vector2.Distance(rb.position, target) > 0.1f)
        {
            if (bossFlip != null)
            {
                bossFlip.moveDirection = (target - rb.position);
            }
            rb.MovePosition(Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime));

            yield return new WaitForFixedUpdate();
        }

        if (rb != null)
        {
            rb.MovePosition(target);
        }
        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Player; // 이동 끝나면 플레이어 바라보기
        }
    }

    private float GetAnimLength(string clipName)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
        {
            return 1f;
        }

        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip.length;
            }
        }
        return 1f;
    }

    #endregion
}