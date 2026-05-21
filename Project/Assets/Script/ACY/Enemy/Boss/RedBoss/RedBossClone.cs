using UnityEngine;

public class RedBossClone : MonoBehaviour, IDamageable
{
    private RedBossAttack owner;

    [Header("파티클")]
    private GameObject spawnVFX;
    private GameObject disappearVFX;

    private const string CLONE_KEY = "RedBossClone";

    private bool isDead;

    public void Init(RedBossAttack boss, GameObject spawnEffect, GameObject disappearEffect)
    {
        owner = boss;

        spawnVFX = spawnEffect;
        disappearVFX = disappearEffect;

        isDead = false;

        gameObject.SetActive(true);

        // 생성 이펙트
        PlayVFX(spawnVFX);
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        DestroyClone();
    }

    public void DestroyClone()
    {
        if (isDead == false)
        {
            isDead = true;
        }

        // 제거 이펙트
        PlayVFX(disappearVFX);

        owner = null;

        // 풀로 반환
        PoolingManager.Instance.Return(CLONE_KEY, gameObject);
    }

    private void PlayVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab == null)
        {
            return;
        }

        GameObject vfx = Instantiate
        (
            vfxPrefab,
            transform.position,
            Quaternion.identity
        );

        ParticleSystem ps = vfx.GetComponentInChildren<ParticleSystem>();

        if (ps != null)
        {
            ps.Play();

            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }
}