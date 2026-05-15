using UnityEngine;

public class EnemyStatus : MonoBehaviour, IDamageable
{
    [Header("적 체력 설정")]
    [SerializeField] private int maxHP = 10; //기본 체력, 적마다 인스펙터창에서 설정
    private int currentHP;

    [Header("사망 연출 설정")]
    [SerializeField] private float dieAnimationLength = 1f; //사망 애니메이션 차후 적마다 인스펙터창에서 애니메이션 길이에 맞춰 설정 
    private Animator anim; 

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        currentHP = maxHP;
    }

    public void TakeDamage(int damage) //IDamageable 인터페이스 구현
    {
        if (currentHP <= 0) //이미 사망한 적은 피해를 입지 않음
        {
            return;
        }

        currentHP -= damage; //적이 피해를 입을 때마다 체력 감소
        Debug.Log($"{gameObject.name}이 피해를 {damage}만큼 입음. 남은 HP : {currentHP}/{maxHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} 사망");

        if (anim != null)
        {
            anim.SetTrigger("Die"); //사망 애니메이션 트리거
        }
     
        if (TryGetComponent(out Collider2D col)) //죽으면 콜라이더 비활성화
        {
            col.enabled = false;
        }

        if (TryGetComponent(out Rigidbody2D rb)) //죽으면 움직임 멈추고 미끄러짐 방지
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic; // 미끄러짐 방지
        }

        Destroy(gameObject, dieAnimationLength); //사망 애니메이션 대기 후 삭제
    }
}