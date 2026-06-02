using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;


/*
 * class Player: 플레이어의 기본 시스템
 * 플레이어의 이동, 점프, 체력 등을 구현
 * 점프: 이단 점프 가능, 땅을 밟으면 카운트 초기화
 * 키 입력 시간에 따라 점프 높이 조절 가능
 * 이동은 linearVelocity
 * 공격은 Cursor에서
 * 
 */
public class Player : MonoBehaviour
{
    PlayerStatus status;

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

    //이동값 변수
    float speed;
    Vector2 inputVec;
    bool isCanMove;

    //점프 횟수
    int jumpCount;

    //대시 준비 여부
    bool isDashReady;

    //마우스
    Transform mouse;
    Vector2 mouseDist;
    public float maxDist;
    bool isSkill;


    //초기화
    private void Start()
    {
        status = GetComponent<PlayerStatus>();
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        anim = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        dashLine = GetComponentInChildren<LineRenderer>();
        jumpCount = 2;

        speed = status.speed;

        attackCursor.GetComponent<AttackCursor>().target = transform;
        mouse = attackCursor.gameObject.transform;

        cursor = cursorObject.GetComponent<Cursor>();
        groundCursor = groundLine.GetComponent<Cursor>();

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

                rigid.AddForceY(10, ForceMode2D.Impulse);

                anim.SetBool("IsJump", true);
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
                rigid.AddForce(dir.normalized * 20, ForceMode2D.Impulse);
                //키 입력 영향 임시 제한
                isCanMove = false;

                SpriteFlip();
                anim.SetBool("IsJump", true);

                jumpCount--;
            }
        }
    }

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
        if (context.started)
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

    public void ActionStance(InputAction.CallbackContext context)
    {
        if (status.HP <= 0 || !status.CanMove)
        {
            return;
        }

        if (context.started)
        {
            TrailRenderer trail = cursorObject.GetComponent<TrailRenderer>();
            TrailRenderer groundTrail = groundLine.GetComponent<TrailRenderer>();

            trail.Clear();
            groundTrail.Clear();

            trail.colorGradient = status.ChangeStance(PlayerStatus.Stance.Red);
            groundTrail.colorGradient = status.ChangeStance(PlayerStatus.Stance.Red);
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
        if (context.started)
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

    void DashLine()
    {
        dashLine.enabled = isDashReady;
        if(jumpCount != 1)
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
                rigid.linearVelocityX = inputVec.x;
                jumpCount = 2;
                isCanMove = true;
                anim.SetBool("IsFalling", false);
                return;
            }
            else if(jumpCount == 2) //Falling 상태 중 점프 카운트 조정
            {
                jumpCount = 1;
                anim.SetBool("IsJump", true);
                return;
            }

            anim.SetBool("IsJump", false);
            anim.SetBool("IsFalling", true);
        }
    }

    //잉크 소모량 계산 및 공격 발동
    void Attack(Cursor cursor)
    {
        if (cursor.isMove)
        {
            status.ink = status.maxInk - cursor.trailLength / 4;
            UI.Instance.UseInk(status.ink);
        }

        if (status.ink <= 0)
        {
            status.ink = 0;
            ActiveAttack(cursor);
        }
    }

    void ActiveAttack(Cursor cursor)
    {
        //스킬 발동, Collider 입히기
        cursor.isMove = false;
        cursor.lifeTime = 0.5f;
        if (cursor.gameObject.GetComponent<EdgeCollider2D>().isTrigger == true)
        {
            cursor.damage = status.damage;
        }
        cursor.SetColliderPointsFromTrail();
        status.ink = status.maxInk;
        UI.Instance.ChargeInk(status.ink);
        if (isSkill)
        {
            Debug.Log("스킬 발동");
        }
    }

    void Move()
    {
        rigid.linearVelocityX = inputVec.x;
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
                    rigid.linearVelocityX = inputVec.x;
                    SpriteFlip();
                    if(jumpCount < 2)
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
