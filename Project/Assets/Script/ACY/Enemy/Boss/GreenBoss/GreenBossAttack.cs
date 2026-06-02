using System.Collections;
using UnityEngine;

// IHitReaction을 상속받아 EnemyStatus의 피격을 정상적으로 가로챕니다.
public class GreenBossAttack : MonoBehaviour, IHitReaction
{
    [Header("새 소환 설정")]
    [SerializeField] private float warningTime = 1.0f;    // 경고선이 깜빡이는 시간
    [SerializeField] private float spawnInterval = 3.0f;  // 새 소환 주기
    [SerializeField] private float birdGap = 1.5f;        // 새 간격
    [SerializeField] private float spawnX = 15f;          // 화면 오른쪽 끝 (새가 생성될 X 좌표)
    [SerializeField] private float minY = -4f;            // 새가 생성될 최소 Y 좌표
    [SerializeField] private float maxY = 4f;            // 새가 생성될 최대 Y 좌표

    [Header("정령 소환 보호막 설정")]
    [SerializeField] private GameObject shieldObject;     // 보스 자식 배리어 오브젝트
    [SerializeField] private float spiritPatternInterval = 15f; // 정령 소환 주기
    [SerializeField] private int healAmountPerSpirit = 5; // 정령 1마리당 초당 회복량

    [SerializeField] private Transform[] spiritSpawnPoints;

    [Header("정령 3개 key 설정")]
    [SerializeField] private string[] spiritPoolKeys = new string[3] { "GreenSpirit", "MintSpirit", "YellowGreenSpirit" };

    // 코루틴 관리 변수 통합 및 정리
    private Coroutine birdRoutine;
    private Coroutine spiritRoutine;

    private int activeSpiritsCount = 0;       // 현재 살아있는 정령 수
    private float remainderHeal = 0f;         // 정수 회복을 위한 소수점 누적 변수
    private bool hasTriggeredSpiritPattern = false; // 체력 50% 이하 패턴 발동 체크
    private EnemyStatus enemyStatus;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();

        if (shieldObject != null)
        {
            shieldObject.SetActive(false);
        }
    }

    private void Start()
    {
        StartBirdAttack();
    }

    private void Update()
    {
        if (activeSpiritsCount > 0 && enemyStatus != null)
        {
            float totalHeal = healAmountPerSpirit * activeSpiritsCount * Time.deltaTime;
            RestoreHealth(totalHeal);
        }
    }

    private void OnDisable()
    {
        StopBirdAttack();
        StopSpiritAttack();
    }

    public void StartBirdAttack()
    {
        if (birdRoutine == null)
        {
            birdRoutine = StartCoroutine(SpawnBirdRoutine());
        }
    }

    public void StopBirdAttack()
    {
        if (birdRoutine != null)
        {
            StopCoroutine(birdRoutine);
            birdRoutine = null;
        }
    }

    private IEnumerator SpawnBirdRoutine()
    {
        while (true)
        {
            float centerY = Random.Range(minY + birdGap, maxY - birdGap);

            for (int i = 0; i < 3; i++)
            {
                float spawnY = centerY + ((i - 1) * birdGap);
                Vector3 spawnPosition = new Vector3(spawnX, spawnY, 0f);

                GameObject birdObj = PoolingManager.Instance.Get("GreenBossBird", spawnPosition, Quaternion.identity);

                if (birdObj != null)
                {
                    Bird bird = birdObj.GetComponent<Bird>();
                    if (bird != null)
                    {
                        bird.Init(warningTime);
                    }
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    public void StartSpiritAttack()
    {
        if (spiritRoutine == null)
        {
            spiritRoutine = StartCoroutine(SpawnSpiritRoutine());
        }
    }

    public void StopSpiritAttack()
    {
        if (spiritRoutine != null)
        {
            StopCoroutine(spiritRoutine);
            spiritRoutine = null;
        }
    }

    private IEnumerator SpawnSpiritRoutine()
    {
        while (true)
        {
            int spiritCount = spiritPoolKeys.Length;
            activeSpiritsCount = spiritCount;
            remainderHeal = 0f;

            if (shieldObject != null)
            {
                shieldObject.SetActive(true);
            }

            bool[] usedIndices = new bool[spiritSpawnPoints.Length];

            for (int i = 0; i < spiritCount; i++)
            {
                if (i >= spiritSpawnPoints.Length) break;

                int randomIndex;
                do
                {
                    randomIndex = Random.Range(0, spiritSpawnPoints.Length);
                } while (usedIndices[randomIndex]);

                usedIndices[randomIndex] = true;
                Vector3 spawnPosition = spiritSpawnPoints[randomIndex].position;

                string currentSpiritKey = spiritPoolKeys[i];
                GameObject spiritObj = PoolingManager.Instance.Get(currentSpiritKey, spawnPosition, Quaternion.identity);

                if (spiritObj != null)
                {
                    BossSpirit spirit = spiritObj.GetComponent<BossSpirit>();
                    if (spirit != null)
                    {
                        spirit.Init(this);
                    }
                }
                else
                {
                    Debug.LogError($"키 오류");
                }
            }

            yield return new WaitForSeconds(spiritPatternInterval);
        }
    }

    // 정령이 죽었을 때 정령 스크립트에서 호출해 줄 콜백 함수
    public void OnSpiritDestroyed()
    {
        activeSpiritsCount--;

        // 모든 정령이 사라지면 무적 배리어 해제
        if (activeSpiritsCount <= 0)
        {
            activeSpiritsCount = 0;
            if (shieldObject != null)
            {
                shieldObject.SetActive(false);
            }
        }
    }

    private void RestoreHealth(float amount)
    {
        remainderHeal += amount;
        int healToInt = Mathf.FloorToInt(remainderHeal);

        if (healToInt > 0)
        {
            remainderHeal -= healToInt;
            enemyStatus.Heal(healToInt); 
        }
    }

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        if (activeSpiritsCount > 0)
        {
            return true; // 대미지 연산 무효화
        }
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        if (!hasTriggeredSpiritPattern && status != null)
        {
            if (status.GetHPRatio() <= 0.5f)
            {
                hasTriggeredSpiritPattern = true; 
                StartSpiritAttack();
            }
        }
    }
}