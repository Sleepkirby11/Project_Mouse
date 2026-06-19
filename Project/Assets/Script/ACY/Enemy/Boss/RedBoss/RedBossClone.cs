using UnityEngine;

public class RedBossClone : MonoBehaviour, IDamageable
{
    private RedBossAttack owner;

    [Header("타겟 설정")]
    private Transform playerTransform;
    public bool isFacingRight = true;

    [Header("파티클")]
    private GameObject spawnVFX;
    private GameObject disappearVFX;

    private const string CLONE_KEY = "RedBossClone";

    private bool isDead;
    private Animator animator;

    private static readonly int CastTrigger = Animator.StringToHash("IsCasting");
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }
    public void Init(RedBossAttack boss, GameObject spawnEffect, GameObject disappearEffect)
    {
        owner = boss;

        spawnVFX = spawnEffect;
        disappearVFX = disappearEffect;

        isDead = false;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

        }
        gameObject.SetActive(true);

        // 생성 이펙트
        PlayVFX(spawnVFX);
        FlipToTarget();
        if (animator != null)
        {
            animator.SetBool(CastTrigger, false); // 초기화
        }
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }
        FlipToTarget();
    }
    public void PlayCastAnimation()
    {
        if (isDead || animator == null)
        {
            return;
        }
       animator.SetBool(CastTrigger, true);
    }

    public void SyncAnimation(int stateHash, float normalizedTime)
    {
        if (isDead || animator == null)
        {
            return;
        }
        animator.SetBool(CastTrigger, true); // 캐스팅 애니메이션으로 전환
        animator.Play(stateHash, 0, normalizedTime); // 보스와 애니메이션 동기화
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        DestroyClone();
    }

    public void DestroyClone()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
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

        GameObject vfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity);

        ParticleSystem ps = vfx.GetComponentInChildren<ParticleSystem>();

        if (ps != null)
        {
            ps.Play();

            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }
    private void FlipToTarget()
    {
        if (playerTransform == null)
        {
            return;
        }

        float direction = playerTransform.position.x - transform.position.x;

        if (direction > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (direction < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}