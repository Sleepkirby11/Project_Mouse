using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStatus : MonoBehaviour, IDamageable, IHittable, IStunnable, IBindable
{
    public enum Stance
    {
        Red,
        Green,
        Blue,
        White
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

    [Header("플레이어 특수 잉크 게이지")]
    public float maxSpecialInk;
    public float specialInk;

    [Header("플레이어 이동 속도")]
    public float speed;

    [Header("플레이어 공격력")]
    public int damage;

    [Header("플레이어 점프 및 대시력")]
    public int jumpForce;
    public int dashForce;

    [Header("플레이어 스탠스")]
    [SerializeField] private Gradient redStance;
    [SerializeField] private Gradient greenStance;
    [SerializeField] private Gradient blueStance;
    [SerializeField] private Gradient whiteStance;
    public Stance currentStance;

    [Header("플레이어 콤보")]
    public int combo;

    [Header("잉크 차징 속도")]
    public float chargeSpeed;
    public float specialChargeSpeed;

    [Header("쿨타임")]
    public float coolTime;
    public float currentCoolTime;


    private Rigidbody2D rb;
    private Animator playerAnim;
    private SpriteRenderer sprite;
    private Player playerComp;

    private bool isKnockbacked; // 넉백 상태 여부
    private bool isStunned; // 스턴 상태 여부
    private bool isPossessed; // 빙의 상태 여부
    private bool isInvincible; // 무적 상태 여부
    private bool isBound; //속박 여부
    public bool IsPossessed => isPossessed;
    public bool IsKnockbacked => isKnockbacked;
    public bool IsInvincible => isInvincible;
    public bool IsBound => isBound;
    private Coroutine bindCoroutine;
    private float originGravityScale;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerAnim = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        playerComp = GetComponent<Player>();

        //리지드바디 세팅
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            originGravityScale = rb.gravityScale;
        }
    }
    //상태 초기화
    void Start()
    {
        hp = maxHp;
        ink = maxInk;
        specialInk = maxSpecialInk;

        isInvincible = false;
    }

    void Update()
    {
        if(!Player.instance.cursor.isMove)
            ChargeInk();
        if(!Player.instance.groundCursor.isMove)
        {
            if(Player.instance.isSkill && Player.instance.cursor.isMove)
            {
                return;
            }
            ChargeSpecialInk();
        }
        Player.instance.InkUIUpdate();
        if(currentCoolTime  < coolTime)
        {
            CoolTimeUpdate();
            UI.Instance.UpdateCoolTimeBar();
        }
    }

    public bool CanMove => !isKnockbacked && !isStunned && !isPossessed && !isBound; // 넉백 또는 스턴 상태가 아닐 때 이동 가능

    // IDamageable 인터페이스 구현을 통한 대미지 처리
    public void TakeDamage(int damage)
    {
        if (hp <= 0 || isInvincible) return;

        hp -= damage;
        Debug.Log($"[PlayerStatus] 플레이어 피격 현재 HP: {hp}/{maxHp}");

        if (UI.Instance != null)
        {
            UI.Instance.UpdateHPBar();
        }

        //점프 애니메이션 실행 이전 Invincible 활성화
        SetInvincible(true);

        //플레이어 애니메이션 및 행동 초기화
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

    public void Heal(int amount)
    {
        if(hp >= maxHp) return;

        hp += amount;               
        if(hp >= maxHp) hp = maxHp;

        UI.Instance.UpdateHPBar();
    }

    //잉크 충전
    void ChargeInk()
    {
        if(ink > maxInk)
        {
            ink = maxInk;
            return;
        }
        else if(ink < maxInk)
        {
            ink += Time.deltaTime * chargeSpeed;
        }
    }

    void ChargeSpecialInk()
    {
        if(specialInk > maxSpecialInk)
        {
            specialInk = maxSpecialInk;
            return;
        }
        else if(specialInk < maxSpecialInk)
        {
            specialInk += Time.deltaTime * specialChargeSpeed;
        }
    }

    public void CoolTimeUpdate()
    {
        if(currentCoolTime >= coolTime)
        {
            currentCoolTime = coolTime;
            return;
        }
        currentCoolTime += Time.deltaTime;
    }

    // IHittable 구현 : 넉백
    public void TakeHit(Vector2 knockbackForce)
    {
        if (rb == null || hp <= 0)
        {
            return;
        }
        if (isBound)
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
        if (isBound)
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

        //넉백 전 행동 캔슬
        playerComp.CancleCursor();

        // 순간 관성 제거 후 방패병의 충격량 대입
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.5f);

        isKnockbacked = false;
        rb.linearVelocityX = 0;// 넉백 종료 시 잔여 속도 제거
        playerComp.OnKnockbackEnd();
    }

    private IEnumerator StunRoutine(float duration) // 스턴
    {
        isStunned = true;
        Debug.Log($"[PlayerStatus] 패링 성공 {duration}초간 스턴");
        playerComp.CancleCursor();

        yield return new WaitForSeconds(duration);

        isStunned = false;
        Debug.Log("[PlayerStatus] 플레이어 스턴 해제");
    }
    public void SetPossessed(bool value)    //빙의
    {
        isPossessed = value;
        playerComp.CancleCursor();

        if (rb != null && value)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log(value ? "[PlayerStatus] 빙의 상태" : "[PlayerStatus] 빙의 해제");
    }
    public void ApplyBind(float duration) //속박
    {
        if (hp <= 0)
        {
            return;
        }
        if (gameObject.activeInHierarchy)
        {
            if (bindCoroutine != null)
            {
                StopCoroutine(bindCoroutine);
            }
            bindCoroutine = StartCoroutine(BindRoutine(duration));
        }
    }

    public void ReleaseBind()
    {
        if (bindCoroutine != null)
        {
            StopCoroutine(bindCoroutine);
            bindCoroutine = null;
        }

        if (isBound)
        {
            isBound = false;
            if (rb != null)
            {
                rb.gravityScale = originGravityScale;
                rb.linearVelocity = Vector2.zero;
            }
            if (playerComp != null)
            {
                playerComp.OnKnockbackEnd();
            }
            if (playerAnim != null)
            {
                playerAnim.SetBool("IsJump", false);
                playerAnim.SetBool("IsFalling", true);
            }
        }
    }
    private IEnumerator BindRoutine(float duration)
    {
        isBound = true;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        if (playerAnim != null)
        {
            playerAnim.SetBool("IsWalk", false);
            playerAnim.SetBool("IsJump", false);
            playerAnim.SetBool("IsFalling", false);
        }

        yield return new WaitForSeconds(duration);

        isBound = false;
        bindCoroutine = null;
        rb.gravityScale = originGravityScale;

        rb.linearVelocity = Vector2.zero;
        playerComp.OnKnockbackEnd();

        if (playerAnim != null)
        {
            playerAnim.SetBool("IsJump", false);
            playerAnim.SetBool("IsFalling", true); 
        }
    }
    public void LaunchByWater(float forceY) // 물기둥
    {
        StartCoroutine(WaterLaunchRoutine(forceY));
    }

    private IEnumerator WaterLaunchRoutine(float forceY) 
    {
        isKnockbacked = true;
        playerComp.CancleCursor();

        rb.linearVelocity = Vector2.zero;

        int playerLayer = LayerMask.NameToLayer("Player");
        int groundLayer = LayerMask.NameToLayer("Ground");
        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, true);

        rb.AddForce(Vector2.up * forceY, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.15f);
        yield return new WaitUntil(() => rb.linearVelocityY <= 0f);
        yield return new WaitForFixedUpdate();

        Physics2D.IgnoreLayerCollision(playerLayer, groundLayer, false);

        isKnockbacked = false;
        rb.linearVelocityX = 0f;
        playerComp.OnKnockbackEnd();
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
            case Stance.White:
                selectedGradient = whiteStance;
                break;
        }
        return selectedGradient;
    }

    void Die()  //사망
    {
        Debug.Log("Die");
    }
}
