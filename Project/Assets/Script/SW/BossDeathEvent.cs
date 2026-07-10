using UnityEngine;

public class BossDeathEvent : MonoBehaviour
{
    private string particleName = "Particle_BossDeath";
    private enum BossColor
    {
        Red,
        Green,
        Blue,
        RGB
    }
    [SerializeField] private BossColor bossColor;

    //보스 사망 SFX 소환 함수
    public void SummonParticle()
    {
        
        // 사망 효과음 재생
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.Death);
        }

        if (PoolingManager.Instance != null)
        {
            GameObject hitParticle = PoolingManager.Instance.Get(particleName, this.transform.position, this.transform.rotation);
            var main = hitParticle.GetComponent<ParticleSystem>().main;

            Color currentColor;
            currentColor = Color.white;
            switch (bossColor)
            {
                case BossColor.Red:
                    currentColor = Color.red;
                    break;
                case BossColor.Green:
                    currentColor = Color.green;
                    break;
                case BossColor.Blue:
                    currentColor = Color.blue;
                    break;
                case BossColor.RGB:
                    currentColor = Color.black;
                    break;
            }
            currentColor.a = 0.25f;
            main.startColor = currentColor;
        }
    }
}
