using UnityEngine;

public class BossSpirit : MonoBehaviour
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

    }

    private void Update()
    {
        if (isDead || enemyStatus == null)
        {
            return;
        }
        if (enemyStatus.GetHPRatio() <= 0)
        {
            isDead = true; // 중복 호출 방지를 위해 플래그를 가장 먼저 true로 만듭니다.

            if (bossAttack != null)
            {
                bossAttack.OnSpiritDestroyed(); // 보스에게 사망 보고
            }
        }
    }
}