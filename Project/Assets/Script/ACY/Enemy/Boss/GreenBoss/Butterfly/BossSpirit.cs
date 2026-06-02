using UnityEngine;

public class BossSpirit : MonoBehaviour, IHitReaction
{
    private GreenBossAttack bossAttack;
    private EnemyStatus enemyStatus;
    private bool isDead = false;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
    }

    public void Init(GreenBossAttack attack)
    {
        bossAttack = attack;
        isDead = false;

        if (enemyStatus != null)
        {
            enemyStatus.Heal(9999);
        }
    }

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage) => false;

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        if (isDead || status == null)
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

            gameObject.SetActive(false); // 오브젝트 비활성화
        }
    }
}