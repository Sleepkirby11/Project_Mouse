using System.Collections;
using UnityEngine;

/*
[마법사] 
행동패턴은 다음을 따름
1. 기본상태 : 자신의 위치 반경에서 텔레포트함
2. 플레이어 감지 시 : 플레이어 위치 반경에서 텔레포트함

공격패턴: 행동패턴의 2번의 경우에만 발동
텔레포트 후 잠시 대기 -> 플레이어의 위치로 마법구 발사
 */
public class WizardEnemyController : MonoBehaviour
{
    private enum WizardState { Idle, Teleporting, Attacking } // 행동 상태 (기본, 순간이동 중, 공격 중)

    [Header("순간이동 설정")]
    [SerializeField] private float teleportCooldown = 1.2f; // 순간이동 쿨타임
    [SerializeField] private float failureCooldown = 1f; // 순간이동 실패 시 재시도까지 대기 시간
    [SerializeField] private float teleportRange = 6f; // 순간이동 최대 거리
    [SerializeField] private float minTeleportDistance = 2.5f; // 순간이동 최소 거리
    [SerializeField] private float actionDelayTime = 0.2f; // 순간이동 전후 행동 대기 시간

    [Header("감지 설정")]
    [SerializeField] private float detectionRange = 8f; // 플레이어 감지 범위
    [SerializeField] private float playerSearchInterval = 1f; // 플레이어 탐색 간격
    private float detectionRangeSqr; // 감지 범위의 제곱값
    private float playerSearchTimer = 0f; // 플레이어 탐색 타이머

    [Header("지형 체크")]
    [SerializeField] private LayerMask wallLayer; // 벽 레이어
    [SerializeField] private LayerMask groundLayer; // 바닥 레이어
    [SerializeField] private float groundCheckHeight = 5f; // 바닥 체크 시작 높이
    [SerializeField] private float groundStandOffset = 1.0f; // 바닥에서 캐릭터를 얼마나 띄울지
    [SerializeField] private float checkRadius = 0.5f; // 벽 충돌 체크 반경
    [SerializeField] private int maxRetryCount = 30; // 순간이동 최대 시도 횟수

    [Header("공격 설정")]
    [SerializeField] private MagicBall projectilePrefab; // 마법구
    [SerializeField] private Transform firePoint; // 발사 위치
   // [SerializeField] private float projectileSpeed = 6f; // 마법구 속도
    [SerializeField] private float attackDelayAfterTeleport = 0.3f; // 텔레포트 후 공격까지 대기 시간
    [SerializeField] private float attackEndDelay = 1f; // 공격 후 대기 시간

    [Header("파티클")]
    [SerializeField] private ParticleSystem appearEffect; // 사라질 때 파티클
    [SerializeField] private ParticleSystem disappearEffect; // 나타날 때 파티클

    [Header("참조")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform playerTransform;

    private WizardState currentState = WizardState.Idle;
    private float lastPatternTime = float.NegativeInfinity;
    private Rigidbody2D wizardRigidbody;

    private WaitForSeconds actionDelay;
    private WaitForSeconds attackDelay;
    private WaitForSeconds attackEndDelayWait;

    private readonly RaycastHit2D[] raycastResults = new RaycastHit2D[1];

    private void Awake()
    {
        wizardRigidbody = GetComponent<Rigidbody2D>();

        actionDelay = new WaitForSeconds(actionDelayTime);
        attackDelay = new WaitForSeconds(attackDelayAfterTeleport);
        attackEndDelayWait = new WaitForSeconds(attackEndDelay);

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        detectionRangeSqr = detectionRange * detectionRange;
    }

    private void Start()
    {
        if (minTeleportDistance > teleportRange)
        {
            float temp = minTeleportDistance;
            minTeleportDistance = teleportRange;
            teleportRange = temp;
        }

        TryFindPlayer();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            playerSearchTimer += Time.deltaTime;

            if (playerSearchTimer >= playerSearchInterval)
            {
                playerSearchTimer = 0f;
                TryFindPlayer();
            }

            return;
        }

        if (currentState != WizardState.Idle)
        {
            return;
        }

        if (Time.time - lastPatternTime >= teleportCooldown)
        {
            StartCoroutine(WizardPatternRoutine());
        }
    }

    private IEnumerator WizardPatternRoutine()
    {
        currentState = WizardState.Teleporting;

        Vector2 currentPosition = transform.position;
        bool isPlayerDetected = false;

        if (playerTransform != null)
        {
            Vector2 toPlayer = (Vector2)playerTransform.position - currentPosition;
            isPlayerDetected = toPlayer.sqrMagnitude <= detectionRangeSqr;
        }

        PlayParticle(appearEffect, currentPosition);
        SetVisible(false);

        yield return actionDelay;

        Vector2 targetDestination = currentPosition;
        bool targetFound = false;

        for (int i = 0; i < maxRetryCount; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection == Vector2.zero)
            {
                randomDirection = Vector2.right;
            }

            randomDirection.Normalize();

            float randomDistance = Random.Range(minTeleportDistance, teleportRange);

            Vector2 centerBasePosition =
                (isPlayerDetected && playerTransform != null)
                ? (Vector2)playerTransform.position
                : currentPosition;

            Vector2 samplePosition = centerBasePosition + randomDirection * randomDistance;

            if (!TryGetGroundedPosition(samplePosition, out Vector2 groundedPosition))
            {
                continue;
            }

            float finalDistance = Vector2.Distance(groundedPosition, centerBasePosition);

            if (finalDistance < minTeleportDistance)
            {
                continue;
            }

            Collider2D hit = Physics2D.OverlapCircle(
                groundedPosition,
                checkRadius,
                wallLayer
            );

            if (hit != null)
            {
                continue;
            }

            targetDestination = groundedPosition;
            targetFound = true;
            break;
        }

        if (!targetFound)
        {
            SetVisible(true);

            float retryDelay = Mathf.Min(failureCooldown, teleportCooldown);
            lastPatternTime = Time.time - (teleportCooldown - retryDelay);

            currentState = WizardState.Idle;
            yield break;
        }

        if (wizardRigidbody != null)
        {
            wizardRigidbody.position = targetDestination;
        }
        else
        {
            transform.position = targetDestination;
        }

        PlayParticle(disappearEffect, targetDestination);
        SetVisible(true);

        if (isPlayerDetected)
        {
            currentState = WizardState.Attacking;

            yield return attackDelay;

            ShootProjectile();

            yield return attackEndDelayWait;
        }

        lastPatternTime = Time.time;
        currentState = WizardState.Idle;
    }

    private void ShootProjectile()
    {
        if (projectilePrefab == null || playerTransform == null)
        {
            return;
        }

        Vector2 spawnPosition =
            firePoint != null
            ? firePoint.position
            : transform.position;

        Vector2 direction =
            ((Vector2)playerTransform.position - spawnPosition).normalized;

        MagicBall projectile = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        projectile.Initialize(direction);
    }

    private bool TryGetGroundedPosition(Vector2 samplePosition, out Vector2 groundedPosition)
    {
        groundedPosition = samplePosition;

        int hitCount = Physics2D.RaycastNonAlloc(
            samplePosition + Vector2.up * groundCheckHeight,
            Vector2.down,
            raycastResults,
            groundCheckHeight * 2f,
            groundLayer
        );

        if (hitCount == 0)
        {
            return false;
        }

        RaycastHit2D hit = raycastResults[0];

        groundedPosition = hit.point + hit.normal * groundStandOffset;

        return true;
    }

    private void TryFindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerSearchTimer = 0f;
        }
    }

    private void SetVisible(bool visible)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color spriteColor = spriteRenderer.color;
        spriteColor.a = visible ? 1f : 0f;
        spriteRenderer.color = spriteColor;
    }

    private void PlayParticle(ParticleSystem particle, Vector2 position)
    {
        if (particle == null)
        {
            return;
        }

        particle.transform.position = position;
        particle.Play();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, teleportRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minTeleportDistance);

        if (playerTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(playerTransform.position, teleportRange);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(playerTransform.position, minTeleportDistance);
        }
    }
}