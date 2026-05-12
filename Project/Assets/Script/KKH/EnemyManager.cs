using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    //// Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{
        
    //}

    //// Update is called once per frame
    //void Update()
    //{
        
    //}
    float enemyHP;
    public virtual void TakeDamage(float damage)
    {
        Debug.Log($"{gameObject.name}이 피해를 {damage}만큼 입음! 남은 HP : {enemyHP}");
        if (enemyHP <= 0) Die();
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} 사망");
        Destroy(gameObject);
    }
}
