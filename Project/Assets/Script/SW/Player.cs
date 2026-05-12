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
    public GameObject cursorObject;

    //보안을 위해 hp와 maxHp를 private로 설정
    private int hp;
    private int maxHp;

    //public getter 프로퍼티로 hp, maxHp 참조
    public int HP => hp;
    public int MaxHP => maxHp;


    Rigidbody2D rigid;
    BoxCollider2D col;

    //이동값 변수
    public float speed;
    Vector2 inputVec;
    bool isCanMove;

    //점프 횟수
    int jumpCount;

    Vector2 mouse;


    //초기화
    void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        jumpCount = 2;

        maxHp = 5;
        hp = maxHp;
    }


    void FixedUpdate()
    {
        //linearVelocity 기반 이동
        if (isCanMove)
        {
            rigid.linearVelocityX = inputVec.x;
        }
        GroundCheck();
    }

    //플레이어 이동
    public void ActionMove(InputAction.CallbackContext context)
    {
        //이동 키 변화 감지 시 true
        isCanMove = true;
        //이동 제한 조건식
        if (hp <= 0)
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
        if (hp <= 0)
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
        if (hp <= 0)
        {
            return;
        }

        if (context.started && jumpCount > 0)
        {
            //마우스 방향 구하기
            mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 dir = mouse - (Vector2)transform.position;

            //normalized된 방향으로 AddForce
            rigid.linearVelocity = Vector2.zero;
            rigid.AddForce(dir.normalized * 20, ForceMode2D.Impulse);
            //키 입력 영향 임시 제한
            isCanMove = false;

            if(rigid.linearVelocityY <= 0)
                return;
            jumpCount--;
        }
    }

    public void ActionAttack(InputAction.CallbackContext context)
    {
        if (hp <= 0)
            return;

        TrailRenderer trail = cursorObject.GetComponent<TrailRenderer>();
        Cursor cursor = cursorObject.GetComponent<Cursor>();
        if(context.started)
        {
            mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            cursorObject.transform.position = mouse;
            trail.startWidth = 0.25f;
            trail.Clear();
            cursor.lifeTime = 0;
        }
        if(context.canceled)
        {
            cursor.lifeTime = 0.5f;
            cursor.SetColliderPointsFromTrail();
        }
    }

    //착지 판정 검사
    void GroundCheck()
    {
        if( rigid.linearVelocityY < 0 &&
        Physics2D.BoxCast
                (transform.position, col.size, 0f, Vector2.down, 0.2f, LayerMask.GetMask("Ground")))
        {
            rigid.linearVelocityX = 0;
            jumpCount = 2;
        }
    }

}
