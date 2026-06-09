using System.Collections;
using Mono.Cecil.Cil;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;


/*
 * class Player: 플레이어의 기본 시스템
 * 플레이어의 이동, 점프, 체력 등을 구현
 * 이동 방향에 따라 sprite 반전
 * 점프: 이단 점프 가능, 땅을 밟으면 카운트 초기화
 * 키 입력 시간에 따라 점프 높이 조절 가능
 * 이동은 linearVelocity
 * 공격, 스킬은 Cursor에서
 * 발판 생성은 groundCursor
 * 스킬 발동 여부는 함수에서 구분
 * 
 */
public class Player : MonoBehaviour
{
    PlayerStatus status;

    public GameObject cam;

    [Header("공격")]
    public GameObject cursorObject;
    Cursor cursor;

    [Header("발판")]
    public GameObject groundLine;
    Cursor groundCursor;

    [Header("공격 표시 커서")]
    public GameObject attackCursor;


    //유니티 컴포넌트
    Rigidbody2D rigid;
    BoxCollider2D col;
    Animator anim;
    SpriteRenderer sprite;
    LineRenderer dashLine;
    ParticleSystem particle;

    //이동값 변수
    float speed;
    Vector2 inputVec;
    bool isCanMove;

    //점프 횟수
    int jumpCount;

    //대시 준비 여부
    bool isDashReady;

    private bool isChargeInk;
    private bool isChargeSpecial;

    //마우스
    Transform mouse;
    Vector2 mouseDist;
    public float maxDist;
    bool isSkill;

    float usedInk;

    //씬 이동 시 초기화 방지
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        DontDestroyOnLoad(cursorObject);
        DontDestroyOnLoad(groundLine);
        DontDestroyOnLoad(attackCursor);
        DontDestroyOnLoad(cam);

        status = GetComponent<PlayerStatus>();
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        anim = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        dashLine = GetComponentInChildren<LineRenderer>();
        particle = GetComponentInChildren<ParticleSystem>();

        attackCursor.GetComponent<AttackCursor>().target = transform;
        cursor = cursorObject.GetComponent<Cursor>();
        groundCursor = groundLine.GetComponent<Cursor>();
    }

    //초기화
    private void Start()
    {
        jumpCount = 2;
        speed = status.speed;
        mouse = attackCursor.gameObject.transform;

        isSkill = false;
        isDashReady = false;
    }


    private void FixedUpdate()
    {

        //linearVelocity 기반 이동
        if (isCanMove && status.CanMove) //status 스크립트에서 움직임 가능 여부 받아오게 수정
        {
            Move();
        }
        else if (!status.CanMove && !status.IsPossessed && !status.IsKnockbacked)
        {
            rigid.linearVelocityX = 0;
        }

        if (cursor.isMove == true)
            Attack(cursor);
        if (groundCursor.isMove == true)
            Attack(groundCursor);

        if (isDashReady && jumpCount == 1)
            DashLine();
        GroundCheck();
    }

    //플레이어 이동
    public void ActionMove(InputAction.CallbackContext context)
    {
        //이동 키 변화 감지 시 true
        isCanMove = true;
        //이동 제한 조건식
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }
        //키 입력 시작
        if (context.started)
        {
            anim.SetBool("IsWalk", true);
            inputVec.x = context.ReadValue<Vector2>().x * speed;

            rigid.linearVelocityX = inputVec.x;
            SpriteFlip();
        }
        //키 입력 종료
        if (context.canceled)
        {
            anim.SetBool("IsWalk", false);
            inputVec.x = 0;
        }
    }
    //플레이어 급강하
    public void ActionDown(InputAction.CallbackContext context)
    {
        //이동 키 변화 감지 시 true
        isCanMove = true;
        //이동 제한 조건식
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }

        if (context.started)
        {
            rigid.linearVelocityY = -speed * 2;
        }
    }

    //플레이어 점프
    public void ActionJump(InputAction.CallbackContext context)
    {
        //점프 제한 조건식
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }

        if (context.started)
        {
            //점프 키 입력을 1회로 한정
            if (jumpCount > 1)
            {
                //점프 가속 초기화
                rigid.linearVelocityY = 0;

                rigid.AddForceY(status.jumpForce, ForceMode2D.Impulse);

                JumpAnimUpdate(true);
                jumpCount--;
            }
        }
        if (context.canceled)
        {
            //키 입력 종료 시 Y 속도값 0
            if (rigid.linearVelocityY > 0 && jumpCount >= 1)
                rigid.linearVelocityY = 0;
        }
    }

    //대시 키 받아오기
    public void ActionDash(InputAction.CallbackContext context)
    {
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }
        //아래가 수정 전
        //if (status.HP <= 0)
        //{
        //    return;
        //}

        if (context.started)
        {
            isDashReady = true;
        }

        if (context.canceled)
        {
            isDashReady = false;
            DashLine();
            if (jumpCount == 1)
            {
                //마우스 방향 구하기
                Vector2 dir = (Vector2)(mouse.transform.position - transform.position);

                //normalized된 방향으로 AddForce
                rigid.linearVelocity = Vector2.zero;
                rigid.AddForce(dir.normalized * status.dashForce, ForceMode2D.Impulse);
                //키 입력 영향 임시 제한
                isCanMove = false;

                SpriteFlip();
                JumpAnimUpdate(true);

                jumpCount--;
            }
        }
    }

    //공격 키 받아오기
    public void ActionAttack(InputAction.CallbackContext context)
    {
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }
        //아래가 수정 전
        //if (status.HP <= 0)
        //    return;

        //스킬과 일반 공격 구분
        //각각 키 입력 변화 시 오브젝트 스크립트 + trail 호출
        //스킬 오브젝트 Component 호출
        TrailRenderer trail = cursorObject.GetComponent<TrailRenderer>();
        if (context.started && status.ink > 0)
        {
            //초기화
            cursorObject.transform.position = mouse.transform.position;
            trail.startWidth = 0.25f;
            cursor.mouse = mouse;
            trail.Clear();
            cursor.isMove = true;
            cursor.lifeTime = 0;
        }
        if (context.canceled && cursor.isMove)
        {
            ActiveAttack(cursor);
        }

    }

    //키 입력 종료 시 UI의 상태에 따른 스탠스 변환
    public void ActionStance(InputAction.CallbackContext context)
    {
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }

        if (context.started)
        {

        }
        if (context.canceled)
        {
            TrailRenderer trail = cursorObject.GetComponent<TrailRenderer>();
            TrailRenderer groundTrail = groundLine.GetComponent<TrailRenderer>();

            trail.Clear();
            groundTrail.Clear();
            var main = particle.main;

            trail.colorGradient = status.ChangeStance(status.currentStance);
            groundTrail.colorGradient = status.ChangeStance(status.currentStance);
            main.startColor = status.ChangeStance(status.currentStance);
        }
    }

    //원 버튼으로 On/Off
    public void ActionSkill(InputAction.CallbackContext context)
    {
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }

        if (context.started)
        {
            if(!isSkill)
                SkillBool(true);
            else
                SkillBool(false);
        }
    }

    public void ActionMakeGround(InputAction.CallbackContext context)
    {
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }
        //아래가 수정 전
        //if (status.HP <= 0)
        //    return;

        //스킬과 일반 공격 구분
        //각각 키 입력 변화 시 오브젝트 스크립트 + trail 호출
        //스킬 오브젝트 Component 호출
        TrailRenderer trail = groundLine.GetComponent<TrailRenderer>();
        if (context.started && status.ink > 0)
        {
            //초기화
            groundLine.transform.position = mouse.transform.position;
            trail.startWidth = 0.25f;
            groundCursor.mouse = mouse;
            trail.Clear();
            groundCursor.isMove = true;
            groundCursor.lifeTime = 0;
        }
        if (context.canceled && groundCursor.isMove)
        {
            ActiveAttack(groundCursor);
        }

    }

    void SpriteFlip()
    {
        //플레이어 이동 방향에 따른 스프라이트 반전
        if (rigid.linearVelocityX > 0)
        {
            sprite.flipX = false;
        }
        else if (rigid.linearVelocityX < 0)
        {
            sprite.flipX = true;
        }
    }

    //Dash 방향 미리보기 표시
    void DashLine()
    {
        dashLine.enabled = isDashReady;
        if (jumpCount != 1)
        {
            dashLine.enabled = false;
            return;
        }
        if (isDashReady)
        {
            Vector3[] positions = new Vector3[2];
            positions[0] = transform.position;
            positions[1] = mouse.position;
            dashLine.SetPositions(positions);
        }
    }

    //착지 판정 검사
    void GroundCheck()
    {
        if (!status.CanMove) //추가함
        {
            return;
        }
        //낙하중 + BoxCast로 착지 판정 검사

        if (rigid.linearVelocityY <= 0)
        {
            if (Physics2D.BoxCast
                (transform.position, col.size, 0f, Vector2.down, 0.25f, LayerMask.GetMask("Ground")))
            {
                //이동 가능 + input 값 이어서 받기
                if (!status.IsKnockbacked)
                    rigid.linearVelocityX = inputVec.x;
                jumpCount = 2;
                isCanMove = true;
                anim.SetBool("IsFalling", false);
                return;
            }
            else if (jumpCount == 2) //Falling 상태 중 점프 카운트 조정
            {
                jumpCount = 1;
                JumpAnimUpdate(true);
                return;
            }

            JumpAnimUpdate(false);
            anim.SetBool("IsFalling", true);
        }
    }

    //매개변수 상태와 무적 판정에 따른 Animation 전환
    void JumpAnimUpdate(bool isUpate)
    {
        if (!status.IsInvincible)
        {
            anim.SetBool("IsJump", isUpate);
        }
    }

    //잉크 소모량 계산 및 공격 발동
    void Attack(Cursor cursor)
    {
        if (cursor.isMove && usedInk != cursor.lastLength)
        {
            //스킬 | 커서 | 일반 공격 종류 걸러내기
            if (isSkill)
                status.specialInk -= cursor.lastLength / 6;
            else if(groundCursor.isMove)
                status.specialInk -= cursor.lastLength / 8;
            else
                status.ink -= cursor.lastLength / 4;
            InkUIUpdate();
            usedInk = 0;
            cursor.lastLength = 0;
        }

        if (status.ink <= 0)
        {
            status.ink = 0;
            ActiveAttack(cursor);
            return;
        }
        else if (status.specialInk <= 0)
        {
            status.specialInk = 0;
            ActiveAttack(cursor);
            return;
        }
    }

    //매개변수 상태에 따른 particle, isSkill 변환
    void SkillBool(bool Skill)
    {
        var main = particle.main;
        isSkill = Skill;
        if (Skill)
        {
            main.startLifetime = 0.25f;
        }
        else
        {
            main.startLifetime = 0;
        }
    }

    //공격 발동
    void ActiveAttack(Cursor cursor)
    {
        bool isGroundCursor = false;
        if (cursor.gameObject.CompareTag("Ground"))
        {
            isGroundCursor = true;
        }

        //발동, Collider 입히기
        cursor.isMove = false;
        cursor.lifeTime = 0.5f;

        //isTrigger = 여기선 공격인가에 대한 여부
        if (cursor.gameObject.GetComponent<EdgeCollider2D>().isTrigger == true)
        {
            DamageCalculate(cursor);
        }

        cursor.SetColliderPointsFromTrail();

        //잉크 초기화
        if (isSkill || isGroundCursor)
        {
            status.specialInk = status.maxSpecialInk;
        }
        else
        {
            status.ink = status.maxInk;
        }
        InkUIUpdate();

        //후처리
        if (isGroundCursor)
            isGroundCursor = false;
        if(isSkill)
            SkillBool(false);
    }

    //공격 | 스킬의 대미지 계산 함수
    void DamageCalculate(Cursor cursor)
    {
        float nowDamage = status.damage;
        float calculateNum = 0;
        float inkBonus;
        if (isSkill)
        {
            Debug.Log("스킬 발동");
            if (status.currentStance == PlayerStatus.Stance.Red)
            {
                calculateNum += 1;
            }
            //잉크 소모량에 비례한 공격 보너스 계산
            inkBonus = status.specialInk / status.maxSpecialInk;
            switch (inkBonus)
            {
                case float f when f <= 1f && f > 0.8f:    //ink 잔여량 100%
                    inkBonus = 1;
                    break;
                case float f when f <= 0.8f && f > 0.6f:
                    inkBonus = 0.7f;
                    break;
                case float f when f <= 0.6f && f > 0.4f:
                    inkBonus = 0.5f;
                    break;
                case float f when f <= 0.4f && f > 0.2f:
                    inkBonus = 0.35f;
                    break;
                case float f when f <= 0.2f && f > 0:
                    inkBonus = 0.2f;
                    break;
                case float f when f <= 0:
                    inkBonus = 0.1f;
                    break;
            }
            calculateNum += inkBonus;
            nowDamage = nowDamage * calculateNum;
        }
        cursor.damage = (int)nowDamage;
    }

    //외부 참조가 가능하게 한 선 그리기 캔슬
    public void CursorCancle()
    {
        if (cursor.isMove == true)
            ActiveAttack(cursor);
        if (groundCursor.isMove == true)
            ActiveAttack(groundCursor);
    }

    //Ink UI를 업데이트하는 함수
    void InkUIUpdate()
    {
        if (UI.Instance != null)
            UI.Instance.ChargeInk(status.ink);
    }


    //이동 함수
    void Move()
    {
        if (status.IsKnockbacked) //추가함
        {
            return;
        }
        rigid.linearVelocityX = inputVec.x;
    }

    //넉백 상태 종료 후 처리 함수
    public void OnKnockbackEnd()
    {
        // 현재 키가 눌려있으면 그 값 그대로 복원, 안눌려있으면 0
        inputVec.x = Keyboard.current != null
            ? (Keyboard.current.dKey.isPressed ? speed : 0)
            + (Keyboard.current.aKey.isPressed ? -speed : 0)
            : 0;
        if (inputVec.x > 0)
        {
            sprite.flipX = false;
        }
        else if (inputVec.x < 0)
        {
            sprite.flipX = true;
        }
    }

    //점프 후 착지 판정 보완
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!status.CanMove) //추가함
        {
            return;
        }

        if (collision.gameObject.CompareTag("Ground"))
        {
            anim.SetBool("IsFalling", false);
            anim.SetBool("IsJump", false);
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f) //접촉 지점의 노멀 벡터가 위쪽을 향할 때만 착지 판정
                {
                    //이동 가능 + input 값 이어서 받기
                    if (!status.IsKnockbacked)
                        rigid.linearVelocityX = inputVec.x;
                    SpriteFlip();
                    if (jumpCount < 2)
                    {
                        jumpCount = 2;
                    }
                    isCanMove = true;
                    DashLine();
                    break;
                }
            }
        }
    }
}
