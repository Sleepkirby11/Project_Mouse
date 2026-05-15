using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hp = maxHp;
        ink = maxInk;
    }

    public void Damage(int damage)
    {
        hp -= damage;
        if (hp <= 0)
        {
            Die();
        }
    }
    void Die()
    {
        Debug.Log("Die");
    }
}
