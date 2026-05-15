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

    [Header("스킬 공격")]
    public GameObject cursorObject;

    [Header("일반 공격")]
    public GameObject normalAttackObject;



    Rigidbody2D rigid;
    BoxCollider2D col;

    //이동값 변수
    float speed;
    Vector2 inputVec;
    bool isCanMove;
    bool isMove;

    //점프 횟수
    int jumpCount;

    //마우스
    Vector2 mouse;
    Vector2 mouseDist;
    public float maxDist;
    bool isSkill;


    //초기화
    void Start()
    {
        status = GetComponent<PlayerStatus>();
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        jumpCount = 2;

        speed = status.speed;

        isSkill = false;
    }


    void FixedUpdate()
    {
        //linearVelocity 기반 이동
        if (isCanMove)
        {
            Move();
        }
        GroundCheck();
    }

    //플레이어 이동
    public void ActionMove(InputAction.CallbackContext context)
    {
        //이동 키 변화 감지 시 true
        isCanMove = true;
        //이동 제한 조건식
        if (status.HP <= 0)
        {
            return;
        }
        //키 입력 시작
        if (context.started)
        {
            inputVec.x = context.ReadValue<Vector2>().x * speed;
        }
        //키 입력 종료
        if (context.canceled)
        {
            inputVec.x = 0;
        }
    }

    //플레이어 점프
    public void ActionJump(InputAction.CallbackContext context)
    {
        //점프 제한 조건식
        if (status.HP <= 0)
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
                jumpCount--;
            }
        }
        if (context.canceled)
        {
            //키 입력 종료 시 Y 속도값 0
            if (rigid.linearVelocityY > 0)
                rigid.linearVelocityY = 0;
        }
    }

    public void ActionDash(InputAction.CallbackContext context)
    {
        if (status.HP <= 0)
        {
            return;
        }

        if (context.started && jumpCount == 1)
        {
            //마우스 방향 구하기
            mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 dir = mouse - (Vector2)transform.position;

            //normalized된 방향으로 AddForce
            rigid.linearVelocity = Vector2.zero;
            rigid.AddForce(dir.normalized * 20, ForceMode2D.Impulse);
            //키 입력 영향 임시 제한
            isCanMove = false;

            jumpCount--;
        }
    }

    public void ActionAttack(InputAction.CallbackContext context)
    {
        if (status.HP <= 0)
            return;

        TrailRenderer trail = cursorObject.GetComponent<TrailRenderer>();
        Cursor cursor = cursorObject.GetComponent<Cursor>();
        if (isSkill)
        {
            if (context.started)
            {
                mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                cursorObject.transform.position = mouse;
                trail.startWidth = 0.25f;
                trail.Clear();
                cursor.lifeTime = 0;
            }
            if (context.canceled)
            {
                cursor.lifeTime = 0.5f;
                cursor.SetColliderPointsFromTrail();
                status.ink = status.maxInk;
            }
        }
        else
        {
            isCanMove = true;
            if (context.started)
            {
                isMove = true;
            }
            if (context.canceled)
            {
                inputVec.x = 0;
                rigid.linearVelocityX = 0;
                isMove = false;
            }
        }
    }

    //착지 판정 검사
    void GroundCheck()
    {
        if (rigid.linearVelocityY <= 0 &&
        Physics2D.BoxCast
                (transform.position, col.size, 0f, Vector2.down, 0.1f, LayerMask.GetMask("Ground")))
        {
            rigid.linearVelocityX = inputVec.x;
            jumpCount = 2;
            isCanMove = true;
        }
    }


    void Move()
    {
        mouseDist =
        (Vector2)normalAttackObject.transform.position - (Vector2)transform.position;
        float clampDist = Mathf.Clamp(mouseDist.x, -1, 1);
        if (isMove)
        {
            if (mouseDist.magnitude < maxDist)
            {
                inputVec.x = 0;
            }
            else
            {
                inputVec.x = clampDist * speed;
            }
            rigid.linearVelocityX = inputVec.x;
        }

    }

    //점프 후 착지 판정 보완
    private void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.gameObject.CompareTag("Ground"))
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f) //접촉 지점의 노멀 벡터가 위쪽을 향할 때만 착지 판정
                {
                    rigid.linearVelocityX = inputVec.x;
                    jumpCount = 2;
                    isCanMove = true;
                    break;
                }
            }
        }
    }
}
