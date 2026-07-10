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
    [SerializeField] private float waterSpoutInterval = 3f;  // 생성 주기
    [SerializeField] private float waterSpoutSpread = 3f;
    [SerializeField] private float waterSpoutYOffset = 0f; // 오프셋
    [SerializeField] private LayerMask groundLayer;

    [Header("발악 패턴 (3페이즈) 설정")]
    public string warningLinePoolKey = "WarningLine"; // 경고선 키
    [SerializeField] private float dashWarningDuration = 0.8f; // 경고선이 유지되는 시간 (페이드아웃 포함)
    [SerializeField] private float warningInterval = 0.4f; // 경고선들이 생성되는 간격

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

    private static readonly int AnimClaw = Animator.StringToHash("Claw");
    private static readonly int AnimSonic = Animator.StringToHash("Sonic");
    private static readonly int AnimDashStart = Animator.StringToHash("DashStart");
    private static readonly int AnimDash = Animator.StringToHash("Dash");
    private static readonly int AnimDashEnd = Animator.StringToHash("DashEnd");
    private static readonly int AnimRestStart = Animator.StringToHash("RestStart");
    private static readonly int AnimRest = Animator.StringToHash("Rest");
    private static readonly int AnimRest2 = Animator.StringToHash("Rest2");

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        bossFlip = GetComponent<BlueBossFlip>();
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyStatus = GetComponent<EnemyStatus>();

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
        if (isDead || isPhase3)
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
    }

    #endregion

    #region Phase Loops

    private IEnumerator Phase1Loop()
    {
        while (!isDead && !isPhase2)
        {
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
        // 패턴 시작과 동시에 무적 전개
        isInvincible = true;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // 3번 반복 (총 9번 돌진)
        for (int i = 0; i < 3; i++)
        {
            if (isDead)
            {
                yield break;
            }

            yield return PatternEnrageDesperation();

            // 세트 사이의 짧은 휴식 타임 (조율 가능)
            yield return new WaitForSeconds(1.0f);
        }

        // 발악 패턴 종료 후 처리 (무적 해제 및 일반 패턴 복귀 혹은 즉시 사망 처리)
        isInvincible = false;

        // 만약 발악 패턴 끝나고도 살아있다면 다시 2페이즈 루프로 돌려보내기
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
        if (laser != null)
        {
            laser.transform.localScale = Vector3.one;
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.BlueBossLaser);
        }

        Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
        Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;

        Collider2D[] bossColliders = GetComponentsInChildren<Collider2D>();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(playerLayer); // 플레이어 레이어만 충돌 검사하도록 설정
        filter.useLayerMask = true;
        filter.useTriggers = true;

        RaycastHit2D[] hits = new RaycastHit2D[5];
        int hitCount = Physics2D.Raycast(spawnPos, fireDirection, filter, hits, sonicRange);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = hits[i];

            // 자신의 콜라이더거나 자식 히트박스라면 무시하고 다음 타겟 검사
            bool isSelf = false;
            foreach (var bossCollider in bossColliders)
            {
                if (hit.collider == bossCollider)
                {
                    isSelf = true;
                    break;
                }
            }
            if (isSelf)
            {
                continue;
            }

            // 본인이 아니라면 타격 처리 후 반복문 탈출
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(sonicDamage);
                break; // 플레이어를 맞췄으므로 멈춤
            }
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
        while (!isDead)
        {
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

    private IEnumerator PatternEnrageDesperation()
    {
        if (player == null || mapBoundaryColliders == null || mapBoundaryColliders.Length == 0 || rb == null)
        {
            yield break;
        }

        // 맵 범위 계산
        Bounds mapBounds = new Bounds();
        bool boundsInitialized = false;

        foreach (var col in mapBoundaryColliders)
        {
            if (col != null)
            {
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
        }

        if (!boundsInitialized)
        {
            Debug.LogWarning("No valid boundary colliders found for Blue Boss!");
            yield break;
        }

        float margin = 1.0f;
        float minX = mapBounds.min.x + margin;
        float maxX = mapBounds.max.x - margin;
        float minY = mapBounds.min.y + margin;
        float maxY = mapBounds.max.y - margin;
        Vector2 mapCenter = mapBounds.center; // 중심점이 될 곳

        SpriteRenderer bossSprite = GetComponentInChildren<SpriteRenderer>();

        // 패턴 시작: 보스 숨기기 및 무적/충돌 해제
        isInvincible = true;

        if (dashHitbox != null)
        {
            dashHitbox.enabled = false;
        }
        rb.linearVelocity = Vector2.zero;

        yield return FadeOut(0.4f);
        // 완전히 사라진 상태로 잠깐 유지
        yield return new WaitForSeconds(0.1f);

        int dashCount = 4; //선 개수
        Vector2[] startPositions = new Vector2[dashCount];
        Vector2[] targetPositions = new Vector2[dashCount];
        GameObject[] spawnedLines = new GameObject[dashCount];

        // 랜덤 각도로 맵을 가로지르는 * 자 모양 경고선 생성
        float baseAngleInterval = 180f / dashCount;
        float randomAngleOffset = Random.Range(0f, 30f); // 매 패턴마다 회전 각도 다르게
        bool startFromLeft = Random.value > 0.5f;

        for (int i = 0; i < dashCount; i++)
        {
            float finalAngle = (baseAngleInterval * i) + randomAngleOffset + Random.Range(-5f, 5f);
            Vector2 dir = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));

            startPositions[i] = mapCenter - dir * 25f;
            targetPositions[i] = mapCenter + dir * 25f;

            startPositions[i] = ClampToEdges(startPositions[i], minX, maxX, minY, maxY);
            targetPositions[i] = ClampToEdges(targetPositions[i], minX, maxX, minY, maxY);

            // 홀짝으로 교차 뒤집기
            bool flip = startFromLeft ? (i % 2 == 0) : (i % 2 != 0);
            if (flip)
            {
                Vector2 temp = startPositions[i];
                startPositions[i] = targetPositions[i];
                targetPositions[i] = temp;
            }

            // 경고선 배치
            Vector2 pathDir = (targetPositions[i] - startPositions[i]).normalized;
            float distance = Vector2.Distance(startPositions[i], targetPositions[i]);
            float angle = Mathf.Atan2(pathDir.y, pathDir.x) * Mathf.Rad2Deg;
            Quaternion lineRotation = Quaternion.Euler(0, 0, angle);

            Vector2 centerPos = (startPositions[i] + targetPositions[i]) * 0.5f;
            GameObject line = PoolingManager.Instance.Get(warningLinePoolKey, centerPos, lineRotation);

            if (line != null)
            {
                line.transform.localScale = new Vector3(distance, 1f, 1f);
                spawnedLines[i] = line;

                if (line.TryGetComponent(out BlueBossWarningLine warningLineScript))
                {
                    warningLineScript.ActivateWarning(dashWarningDuration * 2.5f, warningLinePoolKey);
                }
            }

            yield return new WaitForSeconds(warningInterval); // 선들이 차례대로 깔림
        }

        // 모든 선이 선명해질 때까지 잠시 대기
        yield return new WaitForSeconds(0.4f);

        // 순간이동 및 돌진 실행
        if (anim != null)
        {
            anim.SetTrigger(AnimDash);
        }

        for (int i = 0; i < dashCount; i++)
        {
            if (isDead) yield break;

            // 보스 순간이동 및 등장
            rb.position = startPositions[i];
            yield return FadeIn(0.1f);
            if (dashHitbox != null)
            {
                dashHitbox.enabled = true; // 이때 충돌 판정 켬
            }

            // 보스 방향 및 룩 설정
            Vector2 dashDir = (targetPositions[i] - rb.position).normalized;
            if (bossFlip != null)
            {
                bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
                bossFlip.moveDirection = dashDir;
            }

            // 출발 시 해당 경고선 끄기
            if (spawnedLines[i] != null) spawnedLines[i].SetActive(false);

            // 전력 질주 속도
            float enrageDashSpeed = dashSpeed * 3.5f;

            // 통과 처리
            while (Vector2.Distance(rb.position, targetPositions[i]) > 0.2f)
            {
                rb.MovePosition(Vector2.MoveTowards(rb.position, targetPositions[i], enrageDashSpeed * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }

            rb.MovePosition(targetPositions[i]); // 끝점 안착

            if (bossSprite != null)
            {
                yield return FadeOut(0.15f);
            }
            if (dashHitbox != null)
            {
                dashHitbox.enabled = false;
            }

            yield return new WaitForSeconds(0.1f);
        }

        yield return FadeIn(0.15f);
        transform.rotation = Quaternion.identity;
        if (dashHitbox != null)
        {
            dashHitbox.enabled = false;
        }
        if (anim != null)
        {
            anim.SetTrigger(AnimDashEnd);
        }
        if (bossFlip != null)
        {
            bossFlip.facingMode = BlueBossFlip.FacingMode.Player;
        }

        yield return new WaitForSeconds(0.5f);
    }

    private Vector2 ClampToEdges(Vector2 point, float minX, float maxX, float minY, float maxY)
    {
        point.x = Mathf.Clamp(point.x, minX, maxX);
        point.y = Mathf.Clamp(point.y, minY, maxY);
        return point;
    }

    private IEnumerator FadeOut(float duration)
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            yield break;
        }

        float startAlpha = sr.color.a;

        Color color = sr.color;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            color.a = Mathf.Lerp(startAlpha, 0f, t / duration);
            sr.color = color;

            yield return null;
        }

        color.a = 0f;
        sr.color = color;
    }

    private IEnumerator FadeIn(float duration)
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            yield break;
        }

        float startAlpha = sr.color.a;

        Color color = sr.color;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            color.a = Mathf.Lerp(startAlpha, 1f, t / duration);
            sr.color = color;

            yield return null;
        }

        color.a = 1f;
        sr.color = color;
    }

    #endregion

    #region Death & Utilities

    private void OnDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        StopAllBossCoroutines();

        if (clawHitbox != null)
        {
            clawHitbox.enabled = false;
        }
        if (dashHitbox != null)
        {
            dashHitbox.enabled = false;
        }

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