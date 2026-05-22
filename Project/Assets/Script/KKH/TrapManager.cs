using UnityEngine;

public class TrapManager : MonoBehaviour
{

    public bool isActInteract;
    public void TakeDamage(int damage) //IDamageable 인터페이스 구현
    {
        if(damage > 0)
        {
            isActInteract = true;
        }
    }
}
