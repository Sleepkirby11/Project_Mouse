using System.Collections;
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

    // 파라미터가 있는지 확인하는 함수 (Hurt, Death 확인 용)
    private bool HasParameter(string paramName)
    {
        if (anim == null)
        {
            return false;
        }
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName)
            {
                return true;
            }
        }
        return false;
    }
    public float GetHPRatio() // 보스 페이즈 용 체력 비율 반환
    {
        if (maxHP <= 0)
        {
            return 0f;
        }
        return (float)currentHP / maxHP;
    }
    public void TakeDamage(int damage) //IDamageable 인터페이스 구현
    {
        if (anim != null)
        {
            if (HasParameter("Hurt"))
            {
                anim.SetTrigger("Hurt");
            }
            else
            {
                StartCoroutine(FlashRoutine()); // 깜빡임
            }
        }
        if (currentHP <= 0) //이미 사망한 적은 피해를 입지 않음
        {
            return;
        }


        IHitReaction hitReaction = GetComponent<IHitReaction>();

        if (hitReaction != null)
        {
            bool blocked = hitReaction.OnBeforeTakeDamage(this, damage);

            if (blocked)
            {
                return;
            }
        }
        currentHP -= damage; //적이 피해를 입을 때마다 체력 감소
        Debug.Log($"{gameObject.name}이 피해를 {damage}만큼 입음. 남은 HP : {currentHP}/{maxHP}");

        if (hitReaction != null)
        {
            hitReaction.OnAfterTakeDamage(this, damage);
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }
    private IEnumerator FlashRoutine()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            yield break;
        }

        for (int i = 0; i < 3; i++)
        {
            sr.enabled = false;
            yield return new WaitForSeconds(0.05f);
            sr.enabled = true;
            yield return new WaitForSeconds(0.05f);
        }
    }


    private void Die()
    {

        if (anim != null)
        {
            if (HasParameter("Death"))
            {
                anim.SetTrigger("Death");
            }
            else
            {
                StartCoroutine(FadeRoutine()); // 투명해짐
            }
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
    private IEnumerator FadeRoutine()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Color original = sr.color;

        while (elapsed < dieAnimationLength)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / dieAnimationLength);
            sr.color = new Color(original.r, original.g, original.b, alpha);
            yield return null;
        }
    }
    public void Heal(int amount)
    {
        if (currentHP <= 0)
        {
            return; // 이미 죽은 적은 회복 불가
        }
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        Debug.Log($"{gameObject.name} 회복. 현재 HP : {currentHP}/{maxHP}");
    }
}