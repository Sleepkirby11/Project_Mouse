using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public GameObject cursor;

    //보안을 위해 hp와 maxHp를 private로 설정
    private int hp;
    private int maxHp;

    //public getter 프로퍼티로 hp, maxHp 참조
    public int HP => hp;
    public int MaxHP => maxHp;


    Rigidbody2D rigid;

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

            jumpCount--;
        }
    }

    public void ActionAttack(InputAction.CallbackContext context)
    {
        if (hp <= 0)
            return;
        if(context.started)
        {
            mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            cursor.transform.position = mouse;
            cursor.GetComponent<TrailRenderer>().startWidth = 0.25f;
            cursor.GetComponent<TrailRenderer>().Clear();
            cursor.GetComponent<Cursor>().lifeTime = 0;
        }
        if(context.canceled)
        {
            cursor.GetComponent<Cursor>().lifeTime = 0.5f;
            cursor.GetComponent<Cursor>().SetColliderPointsFromTrail();
        }
    }

    //착지 판정 검사
    private void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.gameObject.CompareTag("Ground"))
        {
            //접촉 지점의 노멀 벡터가 위쪽을 향할 때만 착지 판정
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    jumpCount = 2;
                    break;
                }
            }
        }
    }

}
