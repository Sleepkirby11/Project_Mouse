using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerAnimTrigger : MonoBehaviour
{
    PlayerStatus status;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        status = GetComponentInParent<PlayerStatus>();
    }
    public void SetInvincibleTrue()   //무적 적용
    {
        status.SetInvincible(true);
    }
    public void SetInvincibleFalse()   //무적 해제
    {
        status.SetInvincible(false);
    }

    public void ReStart()
    {
        if(GameManager.instance != null)
        {
            status.Heal(100);
            GameManager.instance.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

}
