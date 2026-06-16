using System.Collections;
using UnityEngine;

public class KillerPlantAttack : MonoBehaviour
{
    [Header("근접 공격")]
    public int meleeDamage = 15;
    public float meleeRate = 1.2f;
    public float meleeHitDelay = 0.4f;

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
        player = GameObject.FindWithTag("Player").transform;
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
        if (actionRunning || player == null)
        {
            return;
        }
        meleeTimer = Mathf.Max(meleeTimer - Time.deltaTime, -1f);
        rangedTimer = Mathf.Max(rangedTimer - Time.deltaTime, -1f);

        if (movement.intent == PlantIntent.InMeleeRange && meleeTimer <= 0f)
        {
            StartCoroutine(DoMelee());
        }
        else if (movement.intent == PlantIntent.InRangedRange && rangedTimer <= 0f)
        {
            StartCoroutine(DoRanged());
        }
    }

    IEnumerator DoMelee()
    {
        actionRunning = true;
        movement.isAttacking = true;
        meleeTimer = meleeRate;

        anim.SetTrigger("Attack");
        yield return new WaitForSeconds(meleeHitDelay);

        if (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist <= meleeRange)
            {
                player.GetComponent<IDamageable>()?.TakeDamage(meleeDamage);
            }
        }

        yield return new WaitForSeconds(0.3f);

        movement.isAttacking = false;
        actionRunning = false;
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
    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(firePoint.position, 0.2f);
    }
}