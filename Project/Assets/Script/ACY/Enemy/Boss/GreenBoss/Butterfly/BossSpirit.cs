using System.Collections;
using UnityEngine;

public class BossSpirit : MonoBehaviour, IHitReaction
{
    public enum SpiritType { Green, Mint, YellowGreen}
    public enum SpiritState { Roaming, Returning }

    [Header("정령 타입")]
    [SerializeField] private SpiritType spiritType;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 4f;        // 비행 속도
    [SerializeField] private float waveSpeed = 3f;        // 위아래 넘실거리는 속도
    [SerializeField] private float waveAmount = 0.3f;     // 위아래 넘실거리는 높이
    [SerializeField] private float arrivalDistance = 0.5f;// 목적지에 도착했다고 판정할 거리

    [Header("맵 크기")]
    // 정령이 날아다닐 맵의 중심과 반지름
    [SerializeField] private Vector2 mapCenter = Vector2.zero;
    [SerializeField] private Vector2 mapRange = new Vector2(10f, 8f); // x축 반지름, y축 반지름

    [Header("소환 연출 설정")] //소환 후 무적 시간
    [SerializeField] private float spawnInvincibleTime = 1.5f;
    [SerializeField] private float roamDuration = 8f;   // 배회 시간
    [SerializeField] private float returnSpeed = 6f;    // 귀환 속도
    [SerializeField] private float returnArrivalDistance = 0.3f;

    [Header("페이드 설정")]
    [SerializeField] private float fadeTime = 1f;

    private string myPoolKey;
    private GreenBossAttack bossAttack;
    private EnemyStatus enemyStatus;
    private bool isDead = false;
    private float invincibleTimer = 0f; //무적 타이머
    private float roamTimer;
    private SpiritState state = SpiritState.Roaming;
    private Transform bossTransform;

    private Vector3 currentTargetPosition; // 현재 날아가고 있는 목적지
    private float waveTimer;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
        }

        if (state == SpiritState.Roaming)
        {
            roamTimer -= Time.deltaTime;
            RoamAroundMap();

            if (roamTimer <= 0f)
            {
                state = SpiritState.Returning;
            }
        }
        else if (state == SpiritState.Returning)
        {
            ReturnToBoss();
        }
    }

    public void Init(GreenBossAttack attack, SpiritType type, Transform boss)
    {
        bossAttack = attack;
        spiritType = type;
        isDead = false;
        invincibleTimer = spawnInvincibleTime;
        waveTimer = Random.Range(0f, 10f);

        myPoolKey = type switch
        {
            SpiritType.Green => "GreenSpirit",
            SpiritType.Mint => "MintSpirit",
            SpiritType.YellowGreen => "YellowGreenSpirit",
            _ => null
        };


        SetNewRandomTarget();
        if (enemyStatus != null)
        {
            enemyStatus.Heal(9999);
        }

        bossTransform = boss;
        state = SpiritState.Roaming;
        roamTimer = roamDuration;
    }

    private void SetNewRandomTarget()
    {
        float randomX = Random.Range(mapCenter.x - mapRange.x, mapCenter.x + mapRange.x);
        float randomY = Random.Range(mapCenter.y - mapRange.y, mapCenter.y + mapRange.y);

        currentTargetPosition = new Vector3(randomX, randomY, transform.position.z);
    }

    private void RoamAroundMap()
    {
        //  현재 목적지 방향 계산
        Vector3 direction = (currentTargetPosition - transform.position).normalized;

        // 목적지를 향해 기본 이동
        Vector3 movePosition = transform.position + direction * moveSpeed * Time.deltaTime;

        //  위아래로 넘실거리는 효과
        waveTimer += Time.deltaTime * waveSpeed;
        float waveOffset = Mathf.Sin(waveTimer) * waveAmount * Time.deltaTime;
        movePosition.y += waveOffset;

        transform.position = movePosition;

        // 이동 방향에 맞춰 좌우 이미지 반전
        if (direction.x > 0.05f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f); // 우측 비행
        }
        else if (direction.x < -0.05f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);  // 좌측 비행
        }

        if (Vector3.Distance(transform.position, currentTargetPosition) < arrivalDistance)
        {
            SetNewRandomTarget();
        }
    }

    // 정령이 날아다니는 영역 표시
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(mapCenter.x, mapCenter.y, 0), new Vector3(mapRange.x * 2, mapRange.y * 2, 0));
    }
    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (isDead)
        {
            return true;
        }
        if (invincibleTimer > 0f)
        {
             return true;
        }
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        if (isDead || status == null)
        {
            return;
        }
        if (invincibleTimer > 0f)
        {
            return;
        }
        if (status.GetHPRatio() <= 0)
        {
            isDead = true;

            if (bossAttack != null)
            {
                bossAttack.OnSpiritDestroyed(); // 보스에게 사망 보고
            }

            StartCoroutine(FadeAndReturn());
        }
    }
    private IEnumerator FadeAndReturn()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            float elapsed = 0f;
            Color original = sr.color;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                sr.color = new Color(original.r, original.g, original.b,  Mathf.Lerp(1f, 0f, elapsed / fadeTime));
                yield return null;
            }
            sr.color = original; // 반납 전 색상 복원
        }

        if (myPoolKey != null)
        {
            PoolingManager.Instance.Return(myPoolKey, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    private void ReturnToBoss()
    {
        if (bossTransform == null)
        {
            return;
        }

        Vector3 dir = (bossTransform.position - transform.position).normalized;

        // 좌우 반전
        if (dir.x > 0.05f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (dir.x < -0.05f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        transform.position = Vector3.MoveTowards(transform.position, bossTransform.position, returnSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, bossTransform.position) <= returnArrivalDistance)
        {
            // 보스에게 귀환 보고
            bossAttack?.OnSpiritReturned();

            if (myPoolKey != null)
            {
                PoolingManager.Instance.Return(myPoolKey, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}