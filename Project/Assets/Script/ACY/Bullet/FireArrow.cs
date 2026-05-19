using System.Collections;
using UnityEngine;

public class FireArrow : MonoBehaviour, IDamageable
{
    [Header("이동 설정")]
    public float speed = 8f;
    public float rotateSpeed = 180f;
    public float lifeTime = 6f;

    [Header("데미지")]
    public int damage = 10;

    [Header("폭발 설정")]
    public float bombDuration = 0.3f;
    public float bombSound = 2f;

    [Header("효과음")]
    public AudioClip flightSound;
    public AudioClip explosionSound;

    private AudioSource audioSource;
    private Transform target;
    private float timer;
    private Animator anim;
    private Collider2D col;
    private bool isExploding = false;
    private float angleOffset = 0f;    // 각도 오프셋 (3연발 각도 차이)

    private const string POOL_KEY = "RedBossArrow";

    void Awake()
    {
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        col = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        isExploding = false;
        timer = 0f;

        if (col != null) col.enabled = true;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        if (audioSource != null && flightSound != null)
        {
            audioSource.clip = flightSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    // 발사 시 초기화 (angleOffset: 3연발 각도 차이)
    public void Init(float angleOffset = 0f)
    {
        this.angleOffset = angleOffset;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;

        timer = 0f;
        isExploding = false;

        // 플레이어 방향 + 각도 오프셋 적용
        if (target != null)
        {
            Vector2 dir = (target.position - transform.position).normalized;
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        }
    }

    void Update()
    {
        if (isExploding) return;

        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Explode();
            return;
        }

        if (target == null)
        {
            transform.Translate(Vector2.right * speed * Time.deltaTime);
            return;
        }

        Vector2 dir = (target.position - transform.position).normalized;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isExploding) return;

        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            Explode();
            return;
        }

        if (other.CompareTag("Cursor"))
        {
            Explode();
            return;
        }
    }

    public void TakeDamage(int damage) => Explode();

    public void Explode()
    {
        if (isExploding) return;
        isExploding = true;

        transform.rotation = Quaternion.identity;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            if (explosionSound != null)
                audioSource.PlayOneShot(explosionSound);
        }

        if (col != null) col.enabled = false;

        if (anim != null)
        {
            anim.SetTrigger("Bomb");
            anim.Play("arrow_bomb", 0, 0f);
        }

        StartCoroutine(DelayedReturn());
    }

    IEnumerator DelayedReturn()
    {
        yield return new WaitForSeconds(bombDuration);

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        yield return new WaitForSeconds(bombSound - bombDuration);

        PoolingManager.Instance.Return(POOL_KEY, gameObject);
    }
}