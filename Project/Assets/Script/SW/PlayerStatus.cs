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

    [Header("잉크 최소 요구량")]
    public int minInk;
    public int minSpecialInk;

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
    public bool IsStunned => isStunned;
    private Coroutine bindCoroutine;
    private Coroutine hitWhileBoundCoroutine;
    private Coroutine stunInvincibleCoroutine;
    private Coroutine invincibleFallbackCoroutine;
    private Coroutine shockVfxCoroutine;
    private MonsterShockParticles shockParticles;
    private float originGravityScale;
    private float savedControlAnimSpeed = 1f;
    private bool isControlAnimPaused;
    private int suppressHitParticleFrame = -1;
    private System.Collections.Generic.List<Collider2D> ignoredColliders = new System.Collections.Generic.List<Collider2D>();
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

    public void SuppressNextHitParticle()
    {
        suppressHitParticleFrame = Time.frameCount;
    }

    // IDamageable 인터페이스 구현을 통한 대미지 처리
    public void TakeDamage(int damage)
    {
        bool suppressHitParticle = suppressHitParticleFrame == Time.frameCount;
        suppressHitParticleFrame = -1;

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

        // 플레이어 피격 효과음 재생
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.PlayerHurt);
        }

        //플레이어의 체력에 따른 애니메이션 트리거 설정
        if (hp <= 0)
        {
            if (isBound)
            {
                ReleaseBind();
            }
            if (isStunned)
            {
                ClearStunState(clearShockImmediately: true);
            }
            ResumePlayerAnim();
            playerAnim.SetTrigger("Dead");
            Die();
        }
        else
        {
            if (isBound)
            {
                if (hitWhileBoundCoroutine != null)
                {
                    StopCoroutine(hitWhileBoundCoroutine);
                }
                hitWhileBoundCoroutine = StartCoroutine(PlayHitWhileBoundRoutine());
            }
            playerAnim.SetTrigger("Hit");
            if (!suppressHitParticle)
            {
                PlayerHitParticles.Play(transform, sprite);
            }
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

    private Coroutine stunCoroutine;
    private Coroutine knockbackCoroutine;

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
            if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = StartCoroutine(KnockbackRoutine(knockbackForce));
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
            if (stunCoroutine != null) StopCoroutine(stunCoroutine);
            if (stunInvincibleCoroutine != null)
            {
                StopCoroutine(stunInvincibleCoroutine);
                stunInvincibleCoroutine = null;
            }
            stunCoroutine = StartCoroutine(StunRoutine(duration));
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
        playerComp.CancleCursor();

        if (isInvincible)
        {
            stunInvincibleCoroutine = StartCoroutine(ClearInvincibleAfterHitDelay(0.583f));
        }

        // 고블린 등 몬스터와 동일하게 피격(Hurt) 포즈 프레임에 고정
        FreezeOnHurtFrame();

        if (shockVfxCoroutine != null)
        {
            StopCoroutine(shockVfxCoroutine);
            shockVfxCoroutine = null;
        }
        if (shockParticles == null)
        {
            shockParticles = MonsterShockParticles.GetOrCreate(this);
        }
        shockParticles?.PlayShock();

        yield return new WaitForSeconds(duration);

        isStunned = false;
        stunCoroutine = null;
        if (stunInvincibleCoroutine != null)
        {
            StopCoroutine(stunInvincibleCoroutine);
            stunInvincibleCoroutine = null;
            if (isInvincible)
            {
                SetInvincible(false);
            }
        }
        shockParticles?.StopShock(clearImmediately: false);

        if (!isBound)
        {
            ResumePlayerAnim();
            SyncAnimParamsAfterControlLoss();
            ExitHurtStateIfNeeded();
        }
    }

    private IEnumerator ClearInvincibleAfterHitDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        stunInvincibleCoroutine = null;
        if (isInvincible && hp > 0)
        {
            SetInvincible(false);
        }
    }

    /// <summary>
    /// 스턴 상태 이상 없이 피격 연출용 감전(스턴) 파티클만 일정 시간 재생합니다.
    /// </summary>
    public void PlayShockVfx(float duration = 0.55f)
    {
        if (hp <= 0 || !gameObject.activeInHierarchy) return;

        if (shockParticles == null)
        {
            shockParticles = MonsterShockParticles.GetOrCreate(this);
        }
        shockParticles?.PlayShock();

        if (shockVfxCoroutine != null)
        {
            StopCoroutine(shockVfxCoroutine);
        }
        shockVfxCoroutine = StartCoroutine(StopShockVfxAfterDelay(duration));
    }

    private IEnumerator StopShockVfxAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        shockVfxCoroutine = null;
        if (!isStunned)
        {
            shockParticles?.StopShock(clearImmediately: false);
        }
    }

    private void ClearStunState(bool clearShockImmediately)
    {
        if (stunCoroutine != null)
        {
            StopCoroutine(stunCoroutine);
            stunCoroutine = null;
        }
        if (stunInvincibleCoroutine != null)
        {
            StopCoroutine(stunInvincibleCoroutine);
            stunInvincibleCoroutine = null;
            if (isInvincible)
            {
                SetInvincible(false);
            }
        }
        if (shockVfxCoroutine != null)
        {
            StopCoroutine(shockVfxCoroutine);
            shockVfxCoroutine = null;
        }
        isStunned = false;
        shockParticles?.StopShock(clearImmediately: clearShockImmediately);
        if (!isBound)
        {
            ResumePlayerAnim();
            SyncAnimParamsAfterControlLoss();
            ExitHurtStateIfNeeded();
        }
    }
    public void SetPossessed(bool value)    //빙의
    {
        isPossessed = value;
        playerComp.CancleCursor();

        if (rb != null && value)
        {
            rb.linearVelocity = Vector2.zero;
        }
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
        if (hitWhileBoundCoroutine != null)
        {
            StopCoroutine(hitWhileBoundCoroutine);
            hitWhileBoundCoroutine = null;
        }

        if (isBound)
        {
            isBound = false;
            if (rb != null)
            {
                rb.gravityScale = originGravityScale;
                rb.linearVelocity = Vector2.zero;
            }
            if (playerComp != null && hp > 0)
            {
                playerComp.OnKnockbackEnd();
            }
            if (!isStunned)
            {
                ResumePlayerAnim();
                SyncAnimParamsAfterControlLoss();
                if (!isInvincible)
                {
                    ExitHurtStateIfNeeded();
                }
            }
        }
        else if (!isStunned)
        {
            ResumePlayerAnim();
        }
    }
    private IEnumerator BindRoutine(float duration)
    {
        if (isStunned)
        {
            ClearStunState(clearShockImmediately: true);
        }

        isBound = true;
        if (playerComp != null)
        {
            playerComp.CancleCursor();
        }
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;

        // Idle로 되돌리지 않고 현재 모션 프레임에서 즉시 정지
        PausePlayerAnim();

        yield return new WaitForSeconds(duration);

        isBound = false;
        bindCoroutine = null;
        if (hitWhileBoundCoroutine != null)
        {
            StopCoroutine(hitWhileBoundCoroutine);
            hitWhileBoundCoroutine = null;
        }
        rb.gravityScale = originGravityScale;

        rb.linearVelocity = Vector2.zero;
        if (playerComp != null && hp > 0)
        {
            playerComp.OnKnockbackEnd();
        }

        if (!isStunned)
        {
            ResumePlayerAnim();
            SyncAnimParamsAfterControlLoss();
            if (!isInvincible)
            {
                ExitHurtStateIfNeeded();
            }
        }
    }

    private void FreezeOnHurtFrame()
    {
        if (playerAnim == null) return;
        ResumePlayerAnim();
        playerAnim.ResetTrigger("Hit");
        playerAnim.SetBool("IsWalk", false);
        playerAnim.SetBool("IsJump", false);
        playerAnim.SetBool("IsFalling", false);
        playerAnim.Play("PlayerHit", 0, 0.35f);
        playerAnim.Update(0f);
        PausePlayerAnim();
    }

    private void PausePlayerAnim()
    {
        if (playerAnim == null) return;
        if (!isControlAnimPaused)
        {
            savedControlAnimSpeed = playerAnim.speed > 0f ? playerAnim.speed : 1f;
            isControlAnimPaused = true;
        }
        playerAnim.speed = 0f;
    }

    private void ResumePlayerAnim()
    {
        if (playerAnim == null) return;
        if (isControlAnimPaused)
        {
            playerAnim.speed = savedControlAnimSpeed > 0f ? savedControlAnimSpeed : 1f;
            isControlAnimPaused = false;
        }
        else if (playerAnim.speed <= 0f)
        {
            playerAnim.speed = 1f;
        }
    }

    private void ExitHurtStateIfNeeded()
    {
        if (playerAnim == null || hp <= 0) return;
        if (playerAnim.GetCurrentAnimatorStateInfo(0).IsName("PlayerHit"))
        {
            bool isGrounded = playerComp != null && playerComp.IsGrounded();
            if (!isGrounded)
            {
                playerAnim.Play("PlayerDown", 0, 0f);
            }
            else if (playerComp != null && Mathf.Abs(playerComp.inputVec.x) > 0.01f)
            {
                playerAnim.Play("PlayerWalk", 0, 0f);
            }
            else
            {
                playerAnim.Play("PlayerIdle", 0, 0f);
            }
            playerAnim.Update(0f);
        }
    }

    private void SyncAnimParamsAfterControlLoss()
    {
        if (playerAnim == null || hp <= 0) return;

        bool isGrounded = playerComp != null && playerComp.IsGrounded();
        bool isMoving = playerComp != null && Mathf.Abs(playerComp.inputVec.x) > 0.01f;
        playerAnim.SetBool("IsWalk", isMoving);
        playerAnim.SetBool("IsJump", false);
        playerAnim.SetBool("IsFalling", !isGrounded);
    }

    private IEnumerator PlayHitWhileBoundRoutine()
    {
        ResumePlayerAnim();
        yield return null;

        float elapsed = 0f;
        const float maxWait = 0.72f;
        while (elapsed < maxWait && isBound && hp > 0 && playerAnim != null)
        {
            if (!isInvincible)
            {
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        hitWhileBoundCoroutine = null;
        if (isBound && hp > 0)
        {
            if (isInvincible)
            {
                SetInvincible(false);
            }
            FreezeOnHurtFrame();
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

        rb.AddForce(Vector2.up * forceY, ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.15f);
        yield return new WaitUntil(() => rb.linearVelocityY <= 0f);
        yield return new WaitForFixedUpdate();

        isKnockbacked = false;
        rb.linearVelocityX = 0f;
        playerComp.OnKnockbackEnd();
    }

    public void SetInvincible(bool value)   //무적
    {
        isInvincible = value;
        Debug.Log(value ? "[PlayerStatus] 무적 상태" : "[PlayerStatus] 무적 해제");

        if (invincibleFallbackCoroutine != null)
        {
            StopCoroutine(invincibleFallbackCoroutine);
            invincibleFallbackCoroutine = null;
        }

        if (value)
        {
            if (gameObject.activeInHierarchy)
            {
                invincibleFallbackCoroutine = StartCoroutine(InvincibleSafetyFallbackRoutine(0.75f));
            }
        }
        else
        {
            // 무적 해제 시, 무시했던 에너미 콜라이더들과의 충돌을 다시 활성화
            Collider2D playerCollider = GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                foreach (var otherCol in ignoredColliders)
                {
                    if (otherCol != null)
                    {
                        Physics2D.IgnoreCollision(playerCollider, otherCol, false);
                    }
                }
            }
            ignoredColliders.Clear();
        }
    }

    private IEnumerator InvincibleSafetyFallbackRoutine(float maxDuration)
    {
        yield return new WaitForSeconds(maxDuration);
        invincibleFallbackCoroutine = null;
        if (isInvincible)
        {
            SetInvincible(false);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleEnemyCollisionIgnore(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleEnemyCollisionIgnore(collision);
    }

    private void HandleEnemyCollisionIgnore(Collision2D collision)
    {
        if (collision == null || collision.gameObject == null) return;

        // 플레이어가 무적 상태일 때, "Enemy" 태그를 가졌거나 EnemyStatus 컴포넌트가 있는 대상과의 충돌을 무시
        if (isInvincible && (collision.gameObject.CompareTag("Enemy") || collision.gameObject.GetComponent<EnemyStatus>() != null))
        {
            Collider2D playerCollider = GetComponent<Collider2D>();
            if (playerCollider != null && collision.collider != null)
            {
                Physics2D.IgnoreCollision(playerCollider, collision.collider, true);
                if (!ignoredColliders.Contains(collision.collider))
                {
                    ignoredColliders.Add(collision.collider);
                }
            }
        }
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
        ClearAttachedLockIcons();
        Player.instance.inputVec = Vector2.zero;
        AudioManager.instance.PlayBGM(false);
        Player.instance.SpriteOrder(5000);
        Player.instance.BlackBG.SetActive(true);

        // 플레이어 사망 시 활성화되어 있는 모든 보스 투사체 및 패턴 일괄 제거
        EnemyStatus[] bosses = FindObjectsByType<EnemyStatus>(FindObjectsSortMode.None);
        foreach (var boss in bosses)
        {
            if (boss != null)
            {
                boss.ClearBossProjectiles();
            }
        }
        ClearAttachedLockIcons();
    }

    private void ClearAttachedLockIcons()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            if (!child.name.StartsWith("Lock", System.StringComparison.Ordinal)) continue;

            if (PoolingManager.Instance != null)
            {
                child.SetParent(PoolingManager.Instance.transform);
                PoolingManager.Instance.Return("FlowerLockIcon", child.gameObject);
            }
            else
            {
                child.SetParent(null);
                child.gameObject.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearStunState(clearShockImmediately: true);
        ClearAttachedLockIcons();
        ResumePlayerAnim();
    }

    // 다음 씬으로 넘어갈때 모든 상태이상 해제 
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 씬 로드 시 플레이어가 이전 위치(사망 위치 등)에서 낙하하거나 충돌하는 것을 방지하기 위해 물리 정지
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // 사망 후 부활 시 체력 완전히 복구
        if (hp <= 0)
        {
            hp = maxHp;
        }

        isInvincible = false;

        if (scene.name == "Tutorial")
        {
            ResetStatus();
        }

        if (isPossessed)
        {
            SetPossessed(false);
            Transform possessionCanvas = transform.Find("PossessionGaugeCanvas");
            if (possessionCanvas != null)
            {
                possessionCanvas.gameObject.SetActive(false);
            }
        }

        if (isStunned) ClearStunState(clearShockImmediately: true);
        if (isKnockbacked)
        {
            isKnockbacked = false;
            if (playerComp != null) playerComp.OnKnockbackEnd();
        }
        if (isBound) ReleaseBind();
        ClearAttachedLockIcons();

        if (UI.Instance != null)
        {
            UI.Instance.UpdateHPBar();
            UI.Instance.UpdateInkBar();
            UI.Instance.UpdateSpedialInkBar();
            UI.Instance.UpdateCoolTimeBar();
        }

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayBGM(true);
        }
    }

    public void ResetStatus()
    {
        hp = maxHp;
        ink = maxInk;
        specialInk = maxSpecialInk;
        currentStance = Stance.White;
        combo = 0;
        currentCoolTime = coolTime;
        isInvincible = false;
        isKnockbacked = false;
        isStunned = false;
        isPossessed = false;
        isBound = false;
        ClearStunState(clearShockImmediately: true);
        ClearAttachedLockIcons();
        ResumePlayerAnim();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = originGravityScale;
        }

        if (playerComp != null)
        {
            playerComp.isSkill = false;
            playerComp.CloseSetting();
            if (playerComp.BlackBG != null)
            {
                playerComp.BlackBG.SetActive(false);
            }

            if (playerComp.cursorObject != null)
            {
                TrailRenderer trail = playerComp.cursorObject.GetComponent<TrailRenderer>();
                if (trail != null)
                {
                    trail.Clear();
                    trail.colorGradient = ChangeStance(currentStance);
                }
            }
            if (playerComp.groundLine != null)
            {
                TrailRenderer groundTrail = playerComp.groundLine.GetComponent<TrailRenderer>();
                if (groundTrail != null)
                {
                    groundTrail.Clear();
                    groundTrail.colorGradient = ChangeStance(currentStance);
                }
            }
            playerComp.ResetSkillParticle();
        }

        if (StatusImage.instance != null)
        {
            StatusImage.instance.ChangeImage((int)currentStance, false);
        }

        if (UI.Instance != null)
        {
            UI.Instance.UpdateHPBar();
            UI.Instance.UpdateInkBar();
            UI.Instance.UpdateSpedialInkBar();
            UI.Instance.UpdateCoolTimeBar();
        }

        // 빙의 게이지 UI 비활성화
        Transform possessionCanvas = transform.Find("PossessionGaugeCanvas");
        if (possessionCanvas != null)
        {
            possessionCanvas.gameObject.SetActive(false);
        }
    }
}
