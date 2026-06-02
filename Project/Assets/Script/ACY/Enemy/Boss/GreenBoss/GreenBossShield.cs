using UnityEngine;

public class GreenBossShield : MonoBehaviour, IHitReaction
{
    [Header("보호막 설정")]
    [SerializeField] private GameObject shieldObject;

    private int activeSpiritsCount = 0; // 현재 살아있는 정령 수
    private int healAmountPerSpirit = 5; // 정령 1마리당 초당 회복량
    private EnemyStatus enemyStatus;

    private float remainderHeal = 0f;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        if (shieldObject != null)
        {
            shieldObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (activeSpiritsCount > 0 && enemyStatus != null)
        {
            float totalHeal = healAmountPerSpirit * activeSpiritsCount * Time.deltaTime;

            RestoreHealth(totalHeal);
        }
    }

    public void SetupSpirits(int count, int healPerSecond)
    {
        activeSpiritsCount = count;
        healAmountPerSpirit = healPerSecond;
        remainderHeal = 0f; 

        if (activeSpiritsCount > 0 && shieldObject != null)
        {
            shieldObject.SetActive(true);
        }
    }

    public void OnSpiritDestroyed()
    {
        activeSpiritsCount--;

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
            return true;
        }

        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage) { }
}