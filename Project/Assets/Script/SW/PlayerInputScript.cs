using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerInputScript : MonoBehaviour
{
    private Player player;


    private void Awake()
    {
        player = GetComponent<Player>();
    }

    //플레이어 이동
    public void ActionMove(InputAction.CallbackContext context)
    {
        //이동 키 변화 감지 시 true
        player.isCanMove = true;
        //사망 시에는 입력 무시
        if (player.status.HP <= 0)
        {
            return;
        }
        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }
        //키 입력 시작
        if (context.started)
        {
            player.anim.SetBool("IsWalk", true);
            player.inputVec.x = context.ReadValue<Vector2>().x * player.status.speed;

            if (player.status.CanMove)
            {
                player.rigid.linearVelocityX = player.inputVec.x;
                player.SpriteFlip();
            }
        }
        //키 입력 종료
        if (context.canceled)
        {
            player.anim.SetBool("IsWalk", false);
            player.inputVec.x = 0;
        }
    }

    //플레이어 급강하
    public void ActionDown(InputAction.CallbackContext context)
    {
        //이동 키 변화 감지 시 true
        player.isCanMove = true;
        //이동 제한 조건식
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }

        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        if (context.started)
        {
            if(player.rigid.linearVelocityY > -player.speed * 2)
                player.rigid.linearVelocityY = -player.speed * 2;
        }
    }

    //플레이어 점프
    public void ActionJump(InputAction.CallbackContext context)
    {
        //점프 제한 조건식
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }

        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        if (context.started)
        {
            //점프 키 입력을 1회로 한정
            if (player.jumpCount > 1)
            {
                //점프 가속 초기화
                player.rigid.linearVelocityY = 0;

                player.rigid.AddForceY(player.status.jumpForce, ForceMode2D.Impulse);

                player.JumpAnimUpdate(true);
                player.jumpCount--;
            }
        }
        if (context.canceled)
        {
            //키 입력 종료 시 Y 속도값 0
            if (player.rigid.linearVelocityY > 0 && player.jumpCount >= 1)
                player.rigid.linearVelocityY = 0;
        }
    }

    public void ActionInteract(InputAction.CallbackContext context)
    {
        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        if(context.started && player.interactable != null)
        {
            player.interactable.Interact();
        }
    }

    //대시 키 받아오기
    public void ActionDash(InputAction.CallbackContext context)
    {
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }

        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        if (context.started)
        {
            player.isDashReady = true;
        }

        if (context.canceled)
        {
            player.isDashReady = false;
            player.DashLine();
            if (player.jumpCount == 1)
            {
                //마우스 방향 구하기
                Vector2 dir = (Vector2)(player.mouse.transform.position - player.transform.position);

                //normalized된 방향으로 AddForce
                player.rigid.linearVelocity = Vector2.zero;
                player.rigid.AddForce(dir.normalized * player.status.dashForce, ForceMode2D.Impulse);
                //키 입력 영향 임시 제한
                player.isCanMove = false;

                player.SpriteFlip();
                player.JumpAnimUpdate(true);

                player.jumpCount--;
            }
        }
    }

    //공격 키 받아오기
    public void ActionAttack(InputAction.CallbackContext context)
    {
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }

        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        //스킬과 일반 공격 구분
        //각각 키 입력 변화 시 오브젝트 스크립트 + trail 호출
        //스킬 오브젝트 Component 호출
        TrailRenderer trail = player.cursorObject.GetComponent<TrailRenderer>();
        if (context.started && player.status.ink > 1)
        {
            //초기화
            player.cursorObject.transform.position = player.mouse.transform.position;
            trail.startWidth = 0.25f;
            player.cursor.mouse = player.mouse;
            trail.Clear();
            player.cursor.isMove = true;
            player.cursor.lifeTime = 0;
        }
        if (context.canceled && player.cursor.isMove)
        {
            player.ActiveAttack(player.cursor);
        }
    }

    //키 입력 종료 시 UI의 상태에 따른 스탠스 변환
    public void ActionStance(InputAction.CallbackContext context)
    {
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }

        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        if (context.started)
        {
                UI.Instance.ActivePal(true);
        }
        if (context.canceled)
        {
            TrailRenderer trail = player.cursorObject.GetComponent<TrailRenderer>();
            TrailRenderer groundTrail = player.groundLine.GetComponent<TrailRenderer>();

            trail.Clear();
            groundTrail.Clear();
            var main = player.particle.main;

            trail.colorGradient = player.status.ChangeStance(player.status.currentStance);
            groundTrail.colorGradient = player.status.ChangeStance(player.status.currentStance);
            main.startColor = player.status.ChangeStance(player.status.currentStance);

            StatusImage.instance.ChangeImage((int)player.status.currentStance, player.isSkill);
            UI.Instance.ActivePal(false);
        }
    }

    //원 버튼으로 On/Off
    public void ActionSkill(InputAction.CallbackContext context)
    {
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }
        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }
        if(player.status.currentCoolTime < player.status.coolTime)
        {
            return;
        }

        if (context.started)
        {
            if (!player.isSkill)
                player.SkillBool(true);
            else
                player.SkillBool(false);
            StatusImage.instance.ChangeImage((int)player.status.currentStance, player.isSkill);
        }
    }

    public void ActionMakeGround(InputAction.CallbackContext context)
    {
        if (player.status.HP <= 0 || !player.status.CanMove)
        {
            return;
        }

        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }

        //스킬과 일반 공격 구분
        //각각 키 입력 변화 시 오브젝트 스크립트 + trail 호출
        //스킬 오브젝트 Component 호출
        TrailRenderer trail = player.groundLine.GetComponent<TrailRenderer>();
        if (context.started && player.status.specialInk > 1)
        {
            //초기화
            player.groundLine.transform.position = player.mouse.transform.position;
            trail.startWidth = 0.25f;
            player.groundCursor.mouse = player.mouse;
            trail.Clear();
            player.groundCursor.isMove = true;
            player.groundCursor.lifeTime = 0;
        }
        if (context.canceled && player.groundCursor.isMove)
        {
            player.ActiveAttack(player.groundCursor);
        }
    }

    public void ActionCheatHeal(InputAction.CallbackContext context)
    {
        if(GameManager.instance != null)
        {
            if(GameManager.instance.isSetting)
            {
                return;
            }
        }
        if(context.started)
        {
            player.status.Heal(100);
            player.status.ink = player.status.maxInk;
            player.status.specialInk = player.status.maxSpecialInk;
        }
    }

    public void ActionMenu(InputAction.CallbackContext context)
    {
        if(context.started)
        {
            player.settingPanel.SetActive(!player.settingPanel.activeSelf);
            if(GameManager.instance != null)
            {
                GameManager.instance.PauseOnOff();
            }
        }
    }
}
