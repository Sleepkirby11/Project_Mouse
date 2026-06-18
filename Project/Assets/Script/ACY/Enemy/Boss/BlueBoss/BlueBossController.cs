using System.Collections;
using UnityEngine;

/*
 * 공격패턴1. 할퀴기(Claw) : 플레이어에게 접근해 근접 공격
 * 공격패턴2. 레이저(Sonic) : 플레이어가 공격 범위 내에 접근하면 레이저 발사
 * 공격패턴3. 돌진(Dash) : 플레이어의 방향으로 돌진 (닿으면 대미지)
 * 행동패턴1. 휴식(Rest) : 돌진 후 50% 확률로 휴식(restPoint로 돌아가서 대기)
 * 
 * 1페이즈 : 할퀴기, 돌진 랜덤
 * 2페이즈 (HP50%) : 할퀴기, 레이저, 돌진(2연속) 랜덤
 */
public class BossController : MonoBehaviour, IHitReaction
{
    private Animator anim;
    private Rigidbody2D rb;
    private EnemyStatus enemyStatus;
    private BlueBossFlip bossFlip;

    [Header("상태 설정")]
    [SerializeField] private float dashSpeed = 10f; // 대시 속도
    [SerializeField] private float patternCooldown = 1.5f; // 패턴 쿨타임
    [SerializeField] private float moveSpeed = 6f; // 이동속도

    [Header("휴식 패턴")] 
    [SerializeField] private Transform[] restPoints; //휴식 포인트
    [SerializeField] private Vector2 restPointOffset = Vector2.zero; //오프셋
    [SerializeField] private float restJumpHeight = 5f; // 휴식 끝난 후 점프 높이

    [Header("히트박스 설정")]
    [SerializeField] private Collider2D clawHitbox;  // 할퀴기
    [SerializeField] private Collider2D dashHitbox; // 대시

    [Header("음파 설정")]
    public string sonicPoolKey = "BlueLaser";
    [SerializeField] private Transform sonicSpawnPoint; // FirePoint
    [SerializeField] private float sonicAngle = 45f;   // 기울기 
    [SerializeField] private float sonicRange = 20f;
    [SerializeField] private int sonicDamage = 10;
    [SerializeField] private LayerMask playerLayer;

    [Header("발악 패턴 (3페이즈) 설정")]
    public string warningLinePoolKey = "WarningLine"; // 경고선 키
    [SerializeField] private float dashWarningDuration = 0.8f; // 경고선이 유지되는 시간 (페이드아웃 포함)
    [SerializeField] private float warningInterval = 0.4f; // 경고선들이 생성되는 간격

    private bool isPhase3 = false; // 3페이즈 진입 여부
    private bool isInvincible = false; // 무적 상태 플래그

    [Header("대시 중 벽 충돌 설정")]
    [SerializeField] private Collider2D[] mapBoundaryColliders;

    private Transform player;
    private bool isPhase2 = false;
    private bool isDead = false;
    private bool restJumpTriggered;

    private Coroutine currentLoop;
    private static readonly int AnimClaw = Animator.StringToHash("Claw");
    private static readonly int AnimSonic = Animator.StringToHash("Sonic");
    private static readonly int AnimDashStart = Animator.StringToHash("DashStart");
    private static readonly int AnimDash = Animator.StringToHash("Dash");
    private static readonly int AnimDashEnd = Animator.StringToHash("DashEnd");
    private static readonly int AnimRestStart = Animator.StringToHash("RestStart");
    private static readonly int AnimRest = Animator.StringToHash("Rest");
    private static readonly int AnimRest2 = Animator.StringToHash("Rest2");

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

        clawHitbox.enabled = false;


        enemyStatus.OnEnemyDeath += OnDeath;

        currentLoop = StartCoroutine(Phase1Loop());
    }

    private void Update()
    {
        if (isDead || isPhase3) return;

        float hpRatio = enemyStatus.GetHPRatio();

        // 3페이즈 최우선 진입
        if (hpRatio <= 0.1f)
        {
            isPhase3 = true;
            isPhase2 = false; // 2페이즈 루프 정지
            if (currentLoop != null) StopCoroutine(currentLoop);
            currentLoop = StartCoroutine(Phase3Loop());
            return;
        }

        if (sonicSpawnPoint != null && !isDead) //레이저 범위 시각화
        {
            Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
            Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;
            Debug.DrawRay(sonicSpawnPoint.position, fireDirection * sonicRange, Color.yellow);
        }
    }

    // ───────────────────────────────────────────
    // 페이즈 루프
    // ───────────────────────────────────────────

    private IEnumerator Phase1Loop()
    {
        while (!isDead && !isPhase2)
        {
            int roll = Random.Range(0, 2); 

            if (roll == 0)
            {
                yield return StartCoroutine(PatternClaw());
            }
            else
            {
                yield return StartCoroutine(PatternDash());

                if (Random.value > 0.5f) //50% 확률로 휴식
                {
                    yield return StartCoroutine(RestRoutine());
                }
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
                yield return StartCoroutine(PatternClaw());
            }
            else if (roll == 1)
            {
                yield return StartCoroutine(PatternDash());
                yield return StartCoroutine(PatternDash());
                if (Random.value > 0.5f)
                {
                    yield return StartCoroutine(RestRoutine());
                }
            }
            else
            {
                yield return StartCoroutine(PatternSonicApproach());
            }

            yield return new WaitForSeconds(patternCooldown);
        }
    }

    private IEnumerator PatternSonicApproach()
    {
        if (player == null)
        {
            yield break;
        }

        bossFlip.facingMode = BlueBossFlip.FacingMode.Player; // 플레이어 바라보기

        while (!isDead)
        {
            // 플레이어 대각선 위 목표 위치 (플레이어 반대편 위)
            float offsetX = bossFlip.isFacingRight ? -sonicRange * 0.7f : sonicRange * 0.7f;
            float offsetY = sonicRange * 0.7f;
            Vector2 targetPos = (Vector2)player.position + new Vector2(offsetX, offsetY);

            rb.MovePosition(Vector2.MoveTowards(rb.position, targetPos, moveSpeed * Time.fixedDeltaTime));

            if (IsPlayerInLaserRange())
            {
                anim.SetTrigger(AnimSonic);
                yield return new WaitForSeconds(GetAnimLength("Sonic"));
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }
    }
    private IEnumerator Phase3Loop()
    {
        // 패턴 시작과 동시에 무적 전개
        isInvincible = true;
        rb.linearVelocity = Vector2.zero;

        // 3번 반복 (총 9번 돌진)
        for (int i = 0; i < 3; i++)
        {
            if (isDead) yield break;

            yield return StartCoroutine(PatternEnrageDesperation());

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
    // ───────────────────────────────────────────
    // 패턴
    // ───────────────────────────────────────────

    private IEnumerator PatternClaw()
    {
        if (player == null)
        {
            yield break;
        }

        Vector2 dir = ((Vector2)player.position - rb.position).normalized;

        Vector2 targetPos = (Vector2)player.position - dir * 0.4f;

        bool attackStarted = false;

        while (!isDead && Vector2.Distance(rb.position, targetPos) > 0.1f)
        {
            float distance = Vector2.Distance(rb.position, targetPos);

            // 목표에 거의 도착했을 때 공격 시작
            if (!attackStarted && distance < 2.5f)
            {
                attackStarted = true;
                anim.SetTrigger(AnimClaw);
            }

            rb.MovePosition(Vector2.MoveTowards(rb.position, targetPos, moveSpeed * 2f * Time.fixedDeltaTime));

            yield return new WaitForFixedUpdate();
        }
        
        rb.MovePosition(targetPos);

        // 혹시 너무 가까워서 위 조건을 못 탔을 경우
        if (!attackStarted)
        {
            anim.SetTrigger(AnimClaw);
        }

        yield return new WaitForSeconds(GetAnimLength("Claw"));
    }

    private IEnumerator PatternDash()
    {
        if (player == null)
        {
            yield break;
        }

        anim.SetTrigger(AnimDashStart);
        yield return new WaitForSeconds(0.2f);

        Vector2 dashDir = ((Vector2)player.position - rb.position).normalized; // 플레이어 방향
        float dashDistance = 20f;
        float remainedDistance = dashDistance;

        anim.SetTrigger(AnimDash);
        dashHitbox.enabled = true;
        bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;

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
                foreach (var wallCollider in mapBoundaryColliders)
                {
                    if (hit.collider == wallCollider)
                    {
                        isWall = true;
                        break;
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
            bossFlip.moveDirection = dashDir;
            rb.MovePosition(nextPos);

            remainedDistance -= moveStep;
            yield return new WaitForFixedUpdate();
        }

        dashHitbox.enabled = false;
        anim.SetTrigger(AnimDashEnd);
        bossFlip.facingMode = BlueBossFlip.FacingMode.Player;
        yield return new WaitForSeconds(0.3f);
    }
    private IEnumerator PatternEnrageDesperation()
    {
        if (player == null || mapBoundaryColliders == null || mapBoundaryColliders.Length == 0)
            yield break;

        // 맵 범위 계산
        Bounds mapBounds = mapBoundaryColliders[0].bounds;
        foreach (var col in mapBoundaryColliders) mapBounds.Encapsulate(col.bounds);

        float margin = 1.0f;
        float minX = mapBounds.min.x + margin;
        float maxX = mapBounds.max.x - margin;
        float minY = mapBounds.min.y + margin;
        float maxY = mapBounds.max.y - margin;
        Vector2 mapCenter = mapBounds.center; // 중심점이 될 곳

        SpriteRenderer bossSprite = GetComponentInChildren<SpriteRenderer>();

        // 패턴 시작: 보스 숨기기 및 무적/충돌 해제
        isInvincible = true;
        if (bossSprite != null) bossSprite.enabled = false;
        dashHitbox.enabled = false;
        rb.linearVelocity = Vector2.zero;

        int dashCount = 4; //선 개수
        Vector2[] startPositions = new Vector2[dashCount];
        Vector2[] targetPositions = new Vector2[dashCount];
        GameObject[] spawnedLines = new GameObject[dashCount];

        // 랜덤 각도로 맵을 가로지르는 * 자 모양 경고선 생성
        float baseAngleInterval = 180f / dashCount;
        float randomAngleOffset = Random.Range(0f, 30f); // 매 패턴마다 회전 각도 다르게

        for (int i = 0; i < dashCount; i++)
        {
            // 중심점을 지나갈 무작위 각도 계산
            float finalAngle = (baseAngleInterval * i) + randomAngleOffset + Random.Range(-5f, 5f);
            Vector2 dir = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));

            // 중심점(맵 중앙 혹은 플레이어 위치)을 기준으로 맵 끝과 끝 계산
            // 넉넉하게 큰 값을 곱한 뒤 외곽 벽에 걸리도록 세팅
            startPositions[i] = mapCenter - dir * 25f;
            targetPositions[i] = mapCenter + dir * 25f;

            // 맵 Bounds 안으로 클램핑(제한)하여 정확한 외곽 점 추출
            startPositions[i] = ClampToEdges(startPositions[i], minX, maxX, minY, maxY);
            targetPositions[i] = ClampToEdges(targetPositions[i], minX, maxX, minY, maxY);

            // 가끔 방향이 반대가 될 수 있도록 50% 확률로 시작점과 끝점 뒤집기 (랜덤성 극대화)
            if (Random.value > 0.5f)
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
        anim.SetTrigger(AnimDash);

        for (int i = 0; i < dashCount; i++)
        {
            if (isDead) yield break;

            // 보스 순간이동 및 등장
            rb.position = startPositions[i];
            if (bossSprite != null) bossSprite.enabled = true;
            dashHitbox.enabled = true; // 이때 충돌 판정 켬

            // 보스 방향 및 룩 설정
            Vector2 dashDir = (targetPositions[i] - rb.position).normalized;
            bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;
            bossFlip.moveDirection = dashDir;

            // 대각선 돌진 축 회전
            float dashAngle = Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? dashAngle : dashAngle + 180f);

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

            if (bossSprite != null) bossSprite.enabled = false;
            dashHitbox.enabled = false;

            yield return new WaitForSeconds(0.1f);
        }

        transform.rotation = Quaternion.identity;
        if (bossSprite != null) bossSprite.enabled = true; // 보스 다시 보이게
        dashHitbox.enabled = false;
        anim.SetTrigger(AnimDashEnd);
        bossFlip.facingMode = BlueBossFlip.FacingMode.Player;

        yield return new WaitForSeconds(0.5f);
    }

    // 외곽 경계선 밖으로 나가지 않도록 좌표를 잡아주는 유틸 함수
    private Vector2 ClampToEdges(Vector2 point, float minX, float maxX, float minY, float maxY)
    {
        point.x = Mathf.Clamp(point.x, minX, maxX);
        point.y = Mathf.Clamp(point.y, minY, maxY);
        return point;
    }

    // ───────────────────────────────────────────
    // 도우미 함수 (맵 외곽 랜덤 포인트 계산)
    // ───────────────────────────────────────────

    // 맵 경계선 위의 랜덤한 점을 반환
    private Vector2 GetRandomPointOnBounds(float minX, float maxX, float minY, float maxY)
    {
        int edge = Random.Range(0, 4); // 0:남, 1:북, 2:서, 3:동
        switch (edge)
        {
            case 0: return new Vector2(Random.Range(minX, maxX), minY); // 남
            case 1: return new Vector2(Random.Range(minX, maxX), maxY); // 북
            case 2: return new Vector2(minX, Random.Range(minY, maxY)); // 서
            case 3: return new Vector2(maxX, Random.Range(minY, maxY)); // 동
            default: return Vector2.zero;
        }
    }

    // 주어진 시작점의 반대편 벽 쪽 랜덤 포인트를 반환 (맵을 가로지르도록)
    private Vector2 GetOppositePointOnBounds(Vector2 startPoint, float minX, float maxX, float minY, float maxY)
    {
        // 간단하게 시작점 좌표를 반전시켜 맵 건너편 영역을 계산
        float targetX = (startPoint.x < (minX + maxX) * 0.5f) ? Random.Range(maxX - 2f, maxX) : Random.Range(minX, minX + 2f);
        float targetY = (startPoint.y < (minY + maxY) * 0.5f) ? Random.Range(maxY - 2f, maxY) : Random.Range(minY, minY + 2f);

        return new Vector2(targetX, targetY);
    }
    public void SonicFire()
    {
        if (sonicSpawnPoint == null)
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
        if (player == null || sonicSpawnPoint == null)
        {
            return false;
        }

        Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
        Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;

        RaycastHit2D hit = Physics2D.Raycast(sonicSpawnPoint.position, fireDirection, sonicRange, playerLayer);

        return hit.collider != null && hit.collider.CompareTag("Player");
    }
    private IEnumerator RestRoutine()
    {
        if (restPoints == null || restPoints.Length == 0)
        {
            yield break;
        }

        Transform restPoint = restPoints[Random.Range(0, restPoints.Length)];

        Vector2 restPos = (Vector2)restPoint.position + restPointOffset;
        yield return MoveToPosition(restPos, moveSpeed);

        anim.SetTrigger(AnimRestStart);

        yield return new WaitForSeconds(GetAnimLength("RestStart"));

        restJumpTriggered = false;

        anim.SetTrigger(Random.value > 0.5f ? AnimRest : AnimRest2);

        yield return new WaitUntil(() => restJumpTriggered);

        Vector2 aboveRest = (Vector2)restPoint.position + Vector2.up * restJumpHeight;
        yield return MoveToPosition(aboveRest, moveSpeed);
    }

    // ───────────────────────────────────────────
    // 사망
    // ───────────────────────────────────────────

    private void OnDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        StopAllCoroutines();

        clawHitbox.enabled = false;
        dashHitbox.enabled = false;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 2f;
    }

    // ───────────────────────────────────────────
    // 애니메이션 이벤트
    // ───────────────────────────────────────────

    public void ClawHitboxOn() => clawHitbox.enabled = true;
    public void ClawHitboxOff() => clawHitbox.enabled = false;


    public void RestJumpEvent()
    {
        restJumpTriggered = true;
    }

    // ───────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────
    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (isInvincible) // 발악 패턴 중 무적
        {
            return true;
        }
        return false; // 평소 대미지 정상
    }

    // 대미지가 들어온 직후에 호출됨 (필요 없다면 비워두기)
    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
    }
    private IEnumerator MoveToPosition(Vector2 target, float speed)
    {
        bossFlip.facingMode = BlueBossFlip.FacingMode.Movement;

        while (!isDead && Vector2.Distance(rb.position, target) > 0.1f)
        {
            bossFlip.moveDirection = (target - rb.position);
            rb.MovePosition(Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime));

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(target);
        bossFlip.facingMode = BlueBossFlip.FacingMode.Player; // 이동 끝나면 플레이어 바라보기
    }
    private float GetAnimLength(string clipName)
    {
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip.length;
            }
        }
        return 1f;
    }
}