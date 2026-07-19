using System.Collections;
using UnityEngine;

public class MantisProjectile : MonoBehaviour
{
    public string poolKey = "MantisProjectile";
    public int damage = 10;
    public float lifetime = 4f;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private Animator anim;    
    private float timer;
    private bool isDestroying;  // 중복 충돌 및 중복 소멸 방지

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
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
        transform.rotation = Quaternion.identity; // 회전 상태 초기화
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

    public void Launch(Vector2 direction, float speed)
    {
        rb.linearVelocity = direction * speed;

        // 투사체가 날아가는 방향으로 회전 처리 (우향 기준 스프라이트)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDestroying)
        {
            return;
        }

        // 플레이어 피격 처리
        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            StartCoroutine(DestroySequence());
            return;
        }

        // 적(자신 포함) 또는 적 캐릭터 컴포넌트가 있는 경우 통과 (무시)
        if (other.CompareTag("Enemy") || other.GetComponent<EnemyStatus>() != null || other.GetComponentInParent<EnemyStatus>() != null)
        {
            return;
        }

        // 트리거 콜라이더(구역 센서, 카메라 경계 등)인 경우 통과 (무시)
        if (other.isTrigger)
        {
            return;
        }

        // 그 외(바닥, 벽 등) 장애물 충돌 시 소멸
        StartCoroutine(DestroySequence());
    }

    IEnumerator DestroySequence()
    {
        isDestroying = true;

        // 부딪힌 순간 콜라이더 비활성화 및 즉시 반환
        rb.linearVelocity = Vector2.zero;
        if (col != null)
        {
            col.enabled = false;
        }

        ReturnToPool();
        yield break;
    }



    void ReturnToPool()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        PoolingManager.Instance.Return(poolKey, gameObject);
    }
}
