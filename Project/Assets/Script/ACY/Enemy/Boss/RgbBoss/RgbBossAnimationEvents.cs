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
        if (BossIntroCutscene.IsPlaying || bossAttack == null || !bossAttack.enabled)
            return;

        bossAttack.SpawnLightning();
    }
    public void SpawnBossBullet()
    {
        if (BossIntroCutscene.IsPlaying || bossAttack == null || !bossAttack.enabled)
            return;

        bossAttack.SpawnBossBullet();
    }
}