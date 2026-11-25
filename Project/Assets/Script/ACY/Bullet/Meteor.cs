using UnityEngine;

public class Meteor : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float minfallSpeed = 6f;
    [SerializeField] private float maxfallSpeed = 10f;

    [Header("데미지")]
    [SerializeField] private int damage = 1;

    float fallSpeed;

    private Vector3 targetPos;
    private bool isFalling;

    private const string METEOR_KEY = "RedBossMeteor";
    private BossProjectileTrail trailVfx;

    private void Awake()
    {
        trailVfx = BossProjectileTrail.Attach(gameObject, BossProjectileTrail.TrailStyle.Meteor, 99);
    }

    public void Init(Vector3 targetPos)
    {
        this.targetPos = targetPos;
        fallSpeed = Random.Range(minfallSpeed, maxfallSpeed);
        isFalling = true;

        trailVfx?.SetActive(true, true, clearBefore: true);
        trailVfx?.EmitBurst(10);
    }

    private void Update()
    {
        if (!isFalling)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, fallSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) <= 0.05f)
        {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isFalling)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            Explode();
        }
    }

    private void Explode()
    {
        isFalling = false;
        PoolingManager.Instance.Return(METEOR_KEY, gameObject);
    }
}