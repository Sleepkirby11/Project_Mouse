using System.Collections;
using UnityEngine;

public class MantisAttack : MonoBehaviour, IHitReaction
{
    #region Settings & Variables

    [Header("내려찍기 (Slam) 공격")]
    public int slamDamage = 5;

    [Header("연속 찌르기 (Stab) 공격")]
    public int stabDamage = 2;
    public float stabCooldown = 6f;

    [Header("공격 주기")]
    public float meleeRate = 1.5f;

    [Header("원거리 공격")]
    public string projectilePoolKey = "MantisProjectile";
    public float projectileSpeed = 8f;
    public float rangedRate = 2.5f;
    public float rangedFireDelay = 0.4f;

    [Header("발사 위치")]
    [SerializeField] private Transform firePoint;

    private MantisMove movement;
    private Animator anim;
    private Transform player;
    private float meleeRange;

    private float meleeTimer;
    private float rangedTimer;
    private float stabCooldownTimer;
    private bool actionRunning;

    public bool IsRangedReady() => rangedTimer <= 0f;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        movement = GetComponent<MantisMove>();
        anim = GetComponentInChildren<Animator>();
        
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        if (movement != null)
        {
            meleeRange = movement.meleeRange;
        }
        else
        {
            meleeRange = 1.8f;
        }

        if (firePoint == null)
        {
            firePoint = this.transform;
        }

        // 처음 시작 시 Stab을 바로 쓸 수 있도록 쿨타임 초기화
        stabCooldownTimer = 0f;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (movement != null)
        {
            movement.isAttacking = false;
        }
        actionRunning = false;
    }

    private void Update()
    {
        // 쿨타임 감소
        if (meleeTimer > 0f) meleeTimer -= Time.deltaTime;
        if (rangedTimer > 0f) rangedTimer -= Time.deltaTime;
        if (stabCooldownTimer > 0f) stabCooldownTimer -= Time.deltaTime;

        // 공격 중이거나 플레이어 또는 이동 컴포넌트가 없으면 동작하지 않음
        if (actionRunning || player == null || movement == null)
        {
            return;
        }

        // 공격 발동 조건 판단
        if (movement.intent == MantisIntent.InMeleeRange && meleeTimer <= 0f)
        {
            DoMelee();
        }
        else if (movement.intent == MantisIntent.InRangedRange && rangedTimer <= 0f)
        {
            StartCoroutine(DoRanged());
        }
    }

    #endregion

    #region Melee Attack Logic

    private void DoMelee()
    {
        actionRunning = true;
        if (movement != null)
        {
            movement.isAttacking = true;
        }
        meleeTimer = meleeRate;

        if (anim != null)
        {
            // Stab 쿨타임 완료 여부에 따른 스킬 분기 (복합 패턴)
            if (stabCooldownTimer <= 0f)
            {
                anim.SetTrigger("Stab");
                stabCooldownTimer = stabCooldown;
            }
            else
            {
                anim.SetTrigger("Slam");
            }
        }
    }

    // Slam 타격 프레임 이벤트에서 호출
    public void OnSlamHit()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= meleeRange)
        {
            player.GetComponent<IDamageable>()?.TakeDamage(slamDamage);
        }
    }

    // Stab 타격 프레임 이벤트에서 호출 (연타형 공격이므로 매 프레임 호출 가능)
    public void OnStabHit()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= meleeRange)
        {
            PlayerStatus pStatus = player.GetComponent<PlayerStatus>();
            if (pStatus != null)
            {
                pStatus.SetInvincible(false); // 무적 상태 강제 해제 (다단 히트 적용)
                pStatus.TakeDamage(stabDamage);
            }
            else
            {
                player.GetComponent<IDamageable>()?.TakeDamage(stabDamage);
            }
        }
    }

    // 씬 애니메이션 이벤트 통합 지원 (호환용)
    public void OnMeleeHit()
    {
        if (stabCooldownTimer > 0f && (stabCooldownTimer >= stabCooldown - 0.5f))
        {
            OnStabHit();
        }
        else
        {
            OnSlamHit();
        }
    }

    // 근접 공격 완료 프레임 이벤트에서 호출
    public void OnMeleeEnd()
    {
        EndAttack();
    }

    // 원거리 공격 완료 프레임 이벤트에서 호출
    public void OnRangedEnd()
    {
        EndAttack();
    }

    private void EndAttack()
    {
        if (movement != null)
        {
            movement.isAttacking = false;
            movement.intent = MantisIntent.Approach;
        }
        actionRunning = false;
    }

    #endregion

    #region Ranged Attack Logic

    private IEnumerator DoRanged()
    {
        actionRunning = true;
        if (movement != null)
        {
            movement.isAttacking = true;
        }
        rangedTimer = rangedRate;

        if (anim != null)
        {
            anim.SetTrigger("AttackCut");
        }
        yield break;
    }

    // 칼날 발사 프레임 이벤트에서 호출
    public void OnRangedShoot()
    {
        FireProjectile();
    }

    private void FireProjectile()
    {
        if (player == null || firePoint == null) return;

        // 플레이어 방향 2D 벡터 산출 (대각선 발사 지원)
        Vector2 targetDir = (player.position - firePoint.position).normalized;

        GameObject obj = PoolingManager.Instance.Get(projectilePoolKey, firePoint.position, Quaternion.identity);
        if (obj == null) return;

        MantisProjectile proj = obj.GetComponent<MantisProjectile>();
        if (proj != null)
        {
            proj.Launch(targetDir, projectileSpeed);
        }
    }

    #endregion

    #region IHitReaction Implementation

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        // 피격 시 진행 중이던 공격 초기화
        if (movement != null)
        {
            movement.isAttacking = false;
            movement.intent = MantisIntent.Approach;
        }
        actionRunning = false;
        StopAllCoroutines();

        if (anim != null)
        {
            anim.ResetTrigger("Slam");
            anim.ResetTrigger("Stab");
            anim.ResetTrigger("AttackCut");
        }
    }

    #endregion
}
