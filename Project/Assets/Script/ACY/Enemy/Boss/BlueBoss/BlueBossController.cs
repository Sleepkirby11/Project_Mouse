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
    [SerializeField] private Collider2D sonicHitbox; // 음파
    [SerializeField] private Collider2D dashHitbox; // 대시

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
        sonicHitbox.enabled = false;

        enemyStatus.OnEnemyDeath += OnDeath;

        currentLoop = StartCoroutine(Phase1Loop());
    }

    private void Update()
    {
        if (isDead || isPhase2)
        {
            return;
        }

        if (enemyStatus.GetHPRatio() <= 0.5f)
        {
            isPhase2 = true;
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
                yield return StartCoroutine(PatternSonic());
            }
            else
            {
                yield return StartCoroutine(PatternDash());
                yield return StartCoroutine(PatternDash());

                yield return StartCoroutine(RestRoutine());
            }

            yield return new WaitForSeconds(patternCooldown);
        }
    }

    // ───────────────────────────────────────────
    // 패턴
    // ───────────────────────────────────────────

    private IEnumerator PatternClaw()
{
    if (player == null)
        yield break;

    Vector2 dir =
        ((Vector2)player.position - rb.position).normalized;

    Vector2 targetPos =
        (Vector2)player.position - dir * 0.4f;

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

        rb.MovePosition(
            Vector2.MoveTowards(
                rb.position,
                targetPos,
                moveSpeed * 2f * Time.fixedDeltaTime));

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

    private IEnumerator PatternSonic()
    {
        if (player == null)
        {
            yield break;
        }

        anim.SetTrigger(AnimSonic);

        yield return new WaitForSeconds(GetAnimLength("Sonic"));
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
        sonicHitbox.enabled = false;
        dashHitbox.enabled = false;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 2f;
    }

    // ───────────────────────────────────────────
    // 애니메이션 이벤트
    // ───────────────────────────────────────────

    public void ClawHitboxOn() => clawHitbox.enabled = true;
    public void ClawHitboxOff() => clawHitbox.enabled = false;

    public void SonicHitboxOn() => sonicHitbox.enabled = true;
    public void SonicHitboxOff() => sonicHitbox.enabled = false;

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