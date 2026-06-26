using System.Collections;
using UnityEngine;

public class FireGear : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float riseSpeed = 3f;
    [SerializeField] private float riseHeight = 5f;

    [Header("Damage")]
    [SerializeField] private float damageInterval = 0.12f;
    [SerializeField] private int damageAmount = 5;

    private Coroutine grindRoutine;
    private Coroutine riseRoutine;

    private Vector3 originPos;

    private float animLength;
    private PlayerStatus capturedPlayer;
    private Rigidbody2D capturedRb;

    private void Awake()
    {
        Animator anim = GetComponentInChildren<Animator>();
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "FireGear")
            {
                animLength = clip.length;
                break;
            }
        }
    }
    private void OnEnable()
    {
        originPos = transform.position;

        if (riseRoutine != null)
            StopCoroutine(riseRoutine);

        riseRoutine = StartCoroutine(RiseRoutine());
    }

    private void OnDisable()
    {
        ReleasePlayer();

        if (riseRoutine != null)
        {
            StopCoroutine(riseRoutine);
            riseRoutine = null;
        }

        if (grindRoutine != null)
        {
            StopCoroutine(grindRoutine);
            grindRoutine = null;
        }
    }

    private IEnumerator RiseRoutine()
    {
        Vector3 targetPos = originPos + Vector3.up * riseHeight;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                riseSpeed * Time.deltaTime);

            if (capturedRb != null)
            {
                capturedRb.linearVelocity = Vector2.zero;

                capturedRb.MovePosition(
                    new Vector2(
                        capturedRb.position.x,
                        transform.position.y + 0.5f));
            }

            yield return null;
        }

        // 상승 종료 후 플레이어 해제
        ReleasePlayer();

        riseRoutine = null;
    }

    public void OnAnimationEnd()
    {
        PoolingManager.Instance.Return("FireGear", gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerStatus player = other.GetComponent<PlayerStatus>();
        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();

        if (player == null || rb == null)
            return;

        capturedPlayer = player;
        capturedRb = rb;
        HitStopManager.Instance.DoHitStop(0.04f, 0f);
        capturedPlayer.ApplyBind(animLength / 2f);

        // 중복 실행 방지
        if (grindRoutine == null)
        {
            grindRoutine = StartCoroutine(GrindRoutine(capturedPlayer));
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        ReleasePlayer();

        if (grindRoutine != null)
        {
            StopCoroutine(grindRoutine);
            grindRoutine = null;
        }
    }

    private void ReleasePlayer()
    {
        capturedRb = null;
        capturedPlayer = null;
    }

    private IEnumerator GrindRoutine(PlayerStatus player)
    {
        while (player != null && player.gameObject.activeInHierarchy)
        {
            player.TakeDamage(damageAmount);

            yield return new WaitForSecondsRealtime(damageInterval);
        }

        grindRoutine = null;
    }
}