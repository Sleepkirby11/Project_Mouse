using System.Collections;
using UnityEngine;

public class PlayerStatus : MonoBehaviour, IDamageable, IHittable, IStunnable
{
    public enum Stance
    {
        Red,
        Green,
        Blue,
        white
    }

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

    [Header("플레이어 공격력")]
    public int damage;

    [Header("플레이어 스탠스")]
    [SerializeField] private Gradient redStance;
    [SerializeField] private Gradient greenStance;
    [SerializeField] private Gradient blueStance;
    [SerializeField] private Gradient whiteStance;


    private Rigidbody2D rb;
    private bool isKnockbacked; // 넉백 상태 여부
    private bool isStunned; // 스턴 상태 여부
    private bool isPossessed; // 빙의 상태 여부
    private bool isInvincible; // 무적 상태 여부
    public bool IsPossessed => isPossessed;
    public bool IsKnockbacked => isKnockbacked;
    public bool IsInvincible => isInvincible;
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

        isInvincible = false;
    }

    public bool CanMove => !isKnockbacked && !isStunned && !isPossessed; // 넉백 또는 스턴 상태가 아닐 때 이동 가능

    // IDamageable 인터페이스 구현을 통한 대미지 처리
    public void TakeDamage(int damage)
    {
        if (hp <= 0 || isInvincible) return;

        hp -= damage;
        Debug.Log($"[PlayerStatus] 플레이어 피격 현재 HP: {hp}/{maxHp}");
        Animator playerAnim = GetComponentInChildren<Animator>();

        if (UI.Instance != null)
        {
            UI.Instance.TakeDamage(damage);
        }

        //플레이어 애니메이션 초기화
        playerAnim.SetBool("IsJump", false);
        playerAnim.SetBool("IsFalling", false);

        //플레이어의 체력에 따른 애니메이션 트리거 설정
        if (hp <= 0)
        {
            playerAnim.SetTrigger("Dead");
            Die();
        }
        else
        {
            playerAnim.SetTrigger("Hit");
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

        yield return new WaitForSeconds(0.5f);

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
    public void SetPossessed(bool value)    //빙의
    {
        isPossessed = value;

        if (rb != null && value)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log(value ? "[PlayerStatus] 빙의 상태" : "[PlayerStatus] 빙의 해제");
    }

    public void SetInvincible(bool value)   //무적
    {
        isInvincible = value;
        Debug.Log(value ? "[PlayerStatus] 무적 상태" : "[PlayerStatus] 무적 해제");
    }

    public Gradient ChangeStance(Stance newStance)   //스탠스 체인지
    {
        Gradient selectedGradient = whiteStance; // 기본값으로 whiteStance 설정

        switch (newStance)
        {
            case Stance.Red:
                selectedGradient = redStance;
                break;
            case Stance.Green:
                selectedGradient = greenStance;
                break;
            case Stance.Blue:
                selectedGradient = blueStance;
                break;
            case Stance.white:
                selectedGradient = whiteStance;
                break;
        }
        return selectedGradient;
    }
    void Die()
    {
        Debug.Log("Die");
    }
}
