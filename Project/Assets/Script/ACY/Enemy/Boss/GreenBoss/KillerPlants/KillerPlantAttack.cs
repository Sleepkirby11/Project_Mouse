using System.Collections;
using UnityEngine;

public class KillerPlantAttack : MonoBehaviour, IHitReaction
{
    #region Settings & Variables

    [Header("근접 공격")]
    public int meleeDamage = 15;
    public float meleeRate = 1.2f;

    [Header("원거리 공격")]
    public string projectilePoolKey = "KillerPlantBullet";
    public float projectileSpeed = 6f;
    public float rangedRate = 2f;
    public float rangedFireDelay = 0.4f;

    [Header("발사 위치")]
    [SerializeField] private Transform firePoint;

    private KillerPlantMove movement;
    private Animator anim;
    private Transform player;
    private float meleeRange;

    private float meleeTimer;
    private float rangedTimer;
    private bool actionRunning;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        movement = GetComponent<KillerPlantMove>();
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
    }

    private void OnDisable()
    {
        // OnDisable 시 기존 공격 관련 코루틴 정리
        StopAllCoroutines();
        if (movement != null)
        {
            movement.isAttacking = false;
        }
        actionRunning = false;
    }

    private void Update()
    {
        // 쿨타임 계산
        if (meleeTimer > 0f)
        {
            meleeTimer -= Time.deltaTime;
        }
        if (rangedTimer > 0f)
        {
            rangedTimer -= Time.deltaTime;
        }

        // 공격 중이거나 플레이어가 없으면 공격 생략
        if (actionRunning || player == null || movement == null)
        {
            return;
        }

        // 공격 발동 조건 분기
        if (movement.intent == PlantIntent.InMeleeRange && meleeTimer <= 0f)
        {
            DoMelee();
        }
        else if (movement.intent == PlantIntent.InRangedRange && rangedTimer <= 0f)
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
            anim.SetTrigger("Attack");
        }
    }

    public void OnMeleeHit() // 애니메이션 프레임 이벤트로 호출
    {
        if (player == null)
        {
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= meleeRange)
        {
            player.GetComponent<IDamageable>()?.TakeDamage(meleeDamage);
        }
    }

    public void OnMeleeEnd() // 애니메이션 프레임 이벤트로 호출
    {
        if (movement != null)
        {
            movement.isAttacking = false; 
            movement.intent = PlantIntent.Approach;
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
            anim.SetTrigger("Shoot");
        }
        yield return new WaitForSeconds(rangedFireDelay);

        FireProjectile();

        yield return new WaitForSeconds(0.3f);

        if (movement != null)
        {
            movement.isAttacking = false;
            movement.ResetRangedZone();
            movement.intent = PlantIntent.Approach;
        }
        actionRunning = false;
    }

    private void FireProjectile()
    {
        if (player == null || firePoint == null)
        {
            return;
        }

        float dirX = player.position.x - firePoint.position.x;
        GameObject obj = PoolingManager.Instance.Get(projectilePoolKey, firePoint.position, Quaternion.identity);
        if (obj == null)
        {
            return;
        }

        obj.GetComponent<KillerPlantBullet>()?.Launch(dirX, projectileSpeed);
    }

    #endregion

    #region IHitReaction Implementation

    public bool OnBeforeTakeDamage(EnemyStatus status, int damage)
    {
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus status, int damage)
    {
        if (movement != null)
        {
            movement.isAttacking = false;
            movement.intent = PlantIntent.Approach;
        }
        actionRunning = false;
        StopAllCoroutines();
    }

    #endregion
}