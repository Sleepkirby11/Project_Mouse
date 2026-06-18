using System.Collections;
using UnityEngine;

public class BossController : MonoBehaviour
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
        if (!isDead && !isPhase2)
        {
            if (enemyStatus.GetHPRatio() <= 0.5f)
            {
                isPhase2 = true;
            }
        }

        if (sonicSpawnPoint != null && !isDead)
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

                yield return StartCoroutine(RestRoutine());
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
                yield return StartCoroutine(RestRoutine());
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
        Vector2 dashTarget = rb.position + dashDir * 20f; // 숫자 만큼 더 이동

        anim.SetTrigger(AnimDash);
        dashHitbox.enabled = true;

        yield return MoveToPosition(dashTarget, dashSpeed);
        dashHitbox.enabled = false;

        anim.SetTrigger(AnimDashEnd);
        yield return new WaitForSeconds(0.3f);
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

        // Raycast 방향도 단순하게
        Vector2 dir = bossFlip.isFacingRight ? Vector2.right : Vector2.left;
        Vector2 fireDirection = Quaternion.Euler(0, 0, bossFlip.isFacingRight ? -sonicAngle : sonicAngle) * dir;

        RaycastHit2D hit = Physics2D.Raycast(spawnPos, fireDirection, sonicRange, playerLayer);

        if (hit.collider != null)
        {
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(sonicDamage);
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