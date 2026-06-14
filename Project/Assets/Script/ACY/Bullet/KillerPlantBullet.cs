using System.Collections;
using UnityEngine;

public class KillerPlantBullet : MonoBehaviour
{
    public string poolKey = "KillerPlantBullet";
    public int damage = 8;
    public float lifetime = 4f;

    private Rigidbody2D rb;
    private Collider2D col;   
    private Animator anim;    
    private float timer;
    private bool isDestroying;  // 중복 충돌 및 중복 소멸 방지용 플래그

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        anim = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        timer = lifetime;
        isDestroying = false;

        if (col != null)
        {
            col.enabled = true;
        }

        rb.linearVelocity = Vector2.zero; // 물리 상태 초기화
        rb.angularVelocity = 0f;
    }

    void Update()
    {
        if (isDestroying)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            StartCoroutine(DestroySequence());
        }
    }

    public void Launch(Vector2 dir, float speed)
    {
        rb.linearVelocity = dir * speed;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDestroying)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            StartCoroutine(DestroySequence());
        }
        else if (!other.CompareTag("Enemy"))
        {
            StartCoroutine(DestroySequence()); 
        }
    }

    IEnumerator DestroySequence()
    {
        isDestroying = true;

        // 부딪힌 순간 정지 및 콜라이더를 끔
        rb.linearVelocity = Vector2.zero;
        if (col != null)
        {
            col.enabled = false;
        }
        if (anim != null) anim.SetTrigger("OnDestroy");

        // 애니메이션의 길이만큼 대기
        yield return new WaitForSeconds(0.3f);

        ReturnToPool();
    }

    void ReturnToPool()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        PoolingManager.Instance.Return(poolKey, gameObject);
    }
}