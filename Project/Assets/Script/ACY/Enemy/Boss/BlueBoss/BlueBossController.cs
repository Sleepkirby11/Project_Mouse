using System.Collections;
using UnityEngine;

public class BossController : MonoBehaviour
{
    [Header("Components")]
    private Animator animator;
    private Rigidbody2D rb;
    private EnemyStatus enemyStatus;

    [Header("Stats")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float clawRange = 2f;
    [SerializeField] private float patternCooldown = 1.5f;

    [Header("Hitboxes")]
    [SerializeField] private Collider2D clawHitbox;
    [SerializeField] private Collider2D sonicHitbox;

    private Transform player;
    private bool isPhase2 = false;
    private bool isDead = false;
    private Coroutine currentLoop;
    private bool isTransitioning = false;

    private static readonly int AnimClaw = Animator.StringToHash("Claw");
    private static readonly int AnimSonic = Animator.StringToHash("Sonic");
    private static readonly int AnimDashStart = Animator.StringToHash("DashStart");
    private static readonly int AnimDash = Animator.StringToHash("Dash");
    private static readonly int AnimDashEnd = Animator.StringToHash("DashEnd");
    private static readonly int AnimRestStart = Animator.StringToHash("RestStart");
    private static readonly int AnimRest = Animator.StringToHash("Rest");
    private static readonly int AnimRest2 = Animator.StringToHash("Rest2");
    private static readonly int AnimRestEnd = Animator.StringToHash("RestEnd");

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyStatus = GetComponent<EnemyStatus>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        clawHitbox.enabled = false;
        sonicHitbox.enabled = false;

        enemyStatus.OnEnemyDeath += OnDeath;

        currentLoop = StartCoroutine(Phase1Loop());
    }

    private void Update()
    {
        if (isDead || isPhase2 || isTransitioning)
            return;

        if (enemyStatus.GetHPRatio() <= 0.5f)
        {
            isPhase2 = true;
            isTransitioning = true;

            StopAllCoroutines();
            StartCoroutine(PhaseTransition());
        }
    }

    // ───────────────────────────────────────────
    // 페이즈 루프
    // ───────────────────────────────────────────

    private IEnumerator Phase1Loop()
    {
        while (!isDead)
        {
            int roll = Random.Range(0, 2);
            if (roll == 0) yield return StartCoroutine(PatternClaw());
            else yield return StartCoroutine(PatternDash());

            yield return new WaitForSeconds(patternCooldown);
        }
    }

    private IEnumerator Phase2Loop()
    {
        while (!isDead)
        {
            int roll = Random.Range(0, 3);
            if (roll == 0) yield return StartCoroutine(PatternClaw());
            else if (roll == 1) yield return StartCoroutine(PatternDash());
            else yield return StartCoroutine(PatternSonic());

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

        Vector2 targetPos = (Vector2)player.position + Vector2.up * clawRange;

        yield return MoveToPosition(targetPos, 4f);

        animator.SetTrigger(AnimClaw);

        yield return new WaitForSeconds(GetAnimLength("Claw"));
    }

    private IEnumerator PatternDash()
    {
        if (player == null)
            yield break;

        animator.SetTrigger(AnimDashStart);
        yield return new WaitForSeconds(0.2f);

        Vector2 dashDir =
            player.position.x > transform.position.x
            ? Vector2.right
            : Vector2.left;

        Vector2 dashTarget =
            rb.position + dashDir * 12f;

        animator.SetTrigger(AnimDash);

        yield return MoveToPosition(dashTarget, dashSpeed);

        animator.SetTrigger(AnimDashEnd);
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator PatternSonic()
    {
        if (player == null)
            yield break;

        Vector2 targetPos =
            new Vector2(transform.position.x, player.position.y);

        yield return MoveToPosition(targetPos, 4f);

        animator.SetTrigger(AnimSonic);

        yield return new WaitForSeconds(GetAnimLength("Sonic"));
    }

    // ───────────────────────────────────────────
    // 페이즈 전환
    // ───────────────────────────────────────────

    private IEnumerator PhaseTransition()
    {
        animator.SetTrigger(AnimRestStart);

        Vector2 groundPos =
            new Vector2(transform.position.x, -3f);

        yield return MoveToPosition(groundPos, 3f);

        int restCount = Random.Range(2, 4);

        for (int i = 0; i < restCount; i++)
        {
            animator.SetTrigger(
                Random.value > 0.5f
                ? AnimRest
                : AnimRest2);

            yield return new WaitForSeconds(1.5f);
        }

        animator.SetTrigger(AnimRestEnd);

        Vector2 airPos =
            new Vector2(transform.position.x, 2f);

        yield return MoveToPosition(airPos, 3f);

        isTransitioning = false;

        currentLoop = StartCoroutine(Phase2Loop());
    }

    // ───────────────────────────────────────────
    // 사망
    // ───────────────────────────────────────────

    private void OnDeath()
    {
        if (isDead)
            return;

        isDead = true;

        StopAllCoroutines();

        clawHitbox.enabled = false;
        sonicHitbox.enabled = false;

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

    // ───────────────────────────────────────────
    // 유틸
    // ───────────────────────────────────────────

    private IEnumerator MoveToPosition(Vector2 target, float speed)
    {
        while (!isDead &&
               Vector2.Distance(rb.position, target) > 0.1f)
        {
            rb.MovePosition(
                Vector2.MoveTowards(
                    rb.position,
                    target,
                    speed * Time.fixedDeltaTime));

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(target);
    }
    private float GetAnimLength(string clipName)
    {
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == clipName) return clip.length;
        return 1f;
    }
}