using UnityEngine;

public class RedBossClone : MonoBehaviour, IDamageable
{
    #region Settings & Variables

    private RedBossAttack owner;

    [Header("타겟 설정")]
    private Transform playerTransform;
    public bool isFacingRight = true;

    private const string CLONE_KEY = "RedBossClone";

    private bool isDead;
    private Animator animator;

    private static readonly int CastTrigger = Animator.StringToHash("IsCasting");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }
        FlipToTarget();
    }

    #endregion

    #region Clone Initialization

    public void Init(RedBossAttack boss, GameObject spawnEffect, GameObject disappearEffect)
    {
        owner = boss;

        isDead = false;

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }
        gameObject.SetActive(true);
        FlipToTarget();

        // 생성 이펙트 재생 (몸체 중심 기준)
        RedBossTeleportParticles.PlayAppear(this);
        if (animator != null)
        {
            animator.SetBool(CastTrigger, false); // 초기화
        }
    }

    #endregion

    #region Animation Synchronization

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

    #endregion

    #region Combat & Damage Handlers

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
        // 제거 이펙트 재생 (몸체 중심 기준)
        RedBossTeleportParticles.PlayDisappear(this);

        if (owner != null)
        {
            owner.OnCloneDestroyed(gameObject);
        }
        owner = null;

        // 풀로 반환
        if (PoolingManager.Instance != null)
        {
            PoolingManager.Instance.Return(CLONE_KEY, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    #endregion

    #region Direction & Flips

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

    #endregion
}