using System.Collections;
using UnityEngine;

public class PlayerStatus : MonoBehaviour, IDamageable, IHittable, IStunnable
{
    [Header("플레이어 HP")]
    [SerializeField] private int maxHp;
    [SerializeField] private int hp;

    //플레이어 HP(참조용)
    public float HP => hp;
    public float MaxHP => maxHp;

    [Header("플레이어 잉크 게이지")]
    public float maxInk;
    public float ink;

    [Header("플레이어 이동 속도")]
    public float speed;

    private Rigidbody2D rb;
    private bool isKnockbacked; // 넉백 상태 여부
    private bool isStunned; // 스턴 상태 여부

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); 

        //리지드바디 세팅
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void Start()
    {
        hp = maxHp;
        ink = maxInk;
    }

    public bool CanMove => !isKnockbacked && !isStunned; // 넉백 또는 스턴 상태가 아닐 때 이동 가능

    public void TakeDamage(int damage) // IDamageable 인터페이스 구현
    {
        hp -= damage;
        Debug.Log($"[PlayerStatus] 플레이어 피격 현재 HP: {hp}/{maxHp}");

        if (hp <= 0)
        {
            Die();
        }
    }

    // IHittable 구현 : 넉백
    public void TakeHit(Vector2 knockbackForce)
    {
        if (rb == null || hp <= 0)
        {
            return;
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(KnockbackRoutine(knockbackForce));
        }
    }

    //IStunnable 구현 : 일정 시간 조작 불가(스턴) 
    public void ApplyStun(float duration)
    {
        if (hp <= 0)
        {
            return;
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(StunRoutine(duration));
        }
    }
    private IEnumerator KnockbackRoutine(Vector2 force) // 넉백
    {
        isKnockbacked = true;

        // 순간 관성 제거 후 방패병의 충격량 대입
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.2f);

        isKnockbacked = false;
    }

    private IEnumerator StunRoutine(float duration) // 스턴
    {
        isStunned = true;
        Debug.Log($"[PlayerStatus] 패링 성공 {duration}초간 스턴");

        yield return new WaitForSeconds(duration);

        isStunned = false;
        Debug.Log("[PlayerStatus] 플레이어 스턴 해제");
    }
    void Die()
    {
        Debug.Log("Die");
    }
}
