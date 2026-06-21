using System.Collections;
using UnityEngine;

public class KillerPlantAttack : MonoBehaviour
{
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

    void Start()
    {
        movement = GetComponent<KillerPlantMove>();
        anim = GetComponentInChildren<Animator>();
        
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        meleeRange = movement.meleeRange;

        if (firePoint == null)
        {
            firePoint = this.transform;
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (movement != null)
        {
            movement.isAttacking = false;
        }
        actionRunning = false;
    }

    void Update()
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
        // 공격 중이라면 아래의 공격 트리거 조건문들을 실행하지 않고 리턴
        if (actionRunning || player == null)
        {
            return;
        }

        // 공격 발동 조건
        if (movement.intent == PlantIntent.InMeleeRange && meleeTimer <= 0f)
        {
            DoMelee();
        }
        else if (movement.intent == PlantIntent.InRangedRange && rangedTimer <= 0f)
        {
            StartCoroutine(DoRanged());
        }
    }

    void DoMelee()
    {
        actionRunning = true;
        movement.isAttacking = true;
        meleeTimer = meleeRate;

        anim.SetTrigger("Attack");
    }

    public void OnMeleeHit() // 애니메이션 이벤트로 호출
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

    public void OnMeleeEnd()
    {
        movement.isAttacking = false; 
        actionRunning = false;

        if (movement != null)
        {
            movement.intent = PlantIntent.Approach;
        }
    }

    IEnumerator DoRanged()
    {
        actionRunning = true;
        movement.isAttacking = true;
        rangedTimer = rangedRate;

        anim.SetTrigger("Shoot");
        yield return new WaitForSeconds(rangedFireDelay);

        FireProjectile();

        yield return new WaitForSeconds(0.3f);

        movement.isAttacking = false;
        actionRunning = false;
        movement.ResetRangedZone();
        movement.intent = PlantIntent.Approach;
    }

    void FireProjectile()
    {
        if (player == null)
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
}