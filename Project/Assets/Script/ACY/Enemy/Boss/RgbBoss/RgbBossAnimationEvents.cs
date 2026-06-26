using UnityEngine;

public class RgbBossAnimationEvents : MonoBehaviour
{
    private RgbBossAttack bossAttack;

    private void Awake()
    {
        bossAttack = GetComponentInParent<RgbBossAttack>();
    }

    public void SpawnLightning()
    {
        bossAttack.SpawnLightning();
    }
    public void SpawnBossBullet()
    {
        bossAttack.SpawnBossBullet();
    }
}