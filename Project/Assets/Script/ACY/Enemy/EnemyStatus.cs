using System;
using System.Collections;
using UnityEngine;

public class EnemyStatus : MonoBehaviour, IDamageable, IStunnable
{
    #region Settings & Variables
    public enum EnemyElement { Red, Green, Blue, None }

    [Header("적 체력 설정")]
    [SerializeField] private int maxHP = 10; // 기본 체력, 적마다 인스펙터창에서 설정
    private int currentHP;

    [Header("사망 연출 설정")]
    [SerializeField] private float dieAnimationLength = 1f; // 사망 애니메이션 길이에 맞춰 설정 
    private Animator anim;

    [Header("상태 이상")]
    public bool isStunned { get; private set; }

    [Header("속성 설정")]
    [SerializeField] private EnemyElement element = EnemyElement.None;
    [SerializeField] private float weaknessMultiplier = 1.5f; // 약점 배율
    [SerializeField] private float resistMultiplier = 0.5f; // 감소 배율

    public event Action OnEnemyDeath; // 사망 시 보고 받을 리스너

    [Header("보스 설정")]
    [SerializeField] private bool isBoss = false; // 보스인지 판별

    public bool IsDead { get; private set; } // 사망 여부 플래그

    private GameObject cachedPortalVisual;
    private PlayerStatus cachedPlayer;
    public EnemyElement CurrentElement => element;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        cachedPlayer = GameObject.FindWithTag("Player")?.GetComponent<PlayerStatus>();
        currentHP = maxHP;
    }
    private void Start()
    {
        if (isBoss)
        {
            CachePortal();
        }
    }
    public void SetElement(EnemyElement newElement)
    {
        element = newElement;
        Debug.Log($"{gameObject.name} 속성 변경 : {element}");
    }
    #endregion

    #region HP Getters & Utility

    public float GetHPRatio() // 보스 페이즈 판별용 체력 비율 반환
    {
        if (maxHP <= 0)
        {
            return 0f;
        }
        return (float)currentHP / maxHP;
    }

    // 애니메이터에 특정 파라미터가 등록되어 있는지 체크하는 안전 장치 함수
    private bool HasParameter(string paramName)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
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

    #endregion

    #region Damage & Healing Logic

    public void TakeDamage(int damage) // IDamageable 인터페이스 구현
    {
        if (currentHP <= 0) // 이미 사망한 적은 추가 피해 없음
        {
            return;
        }

        IHitReaction hitReaction = GetComponent<IHitReaction>();

        if (hitReaction != null)
        {
            // 피격 직전 데미지 경감/무효 조건 체크 (예: 방패병 패링 등)
            bool blocked = hitReaction.OnBeforeTakeDamage(this, damage);

            if (blocked)
            {
                return;
            }
        }
            damage = ApplyElementalDamage(damage);
        CameraShake.instance.Impulse();
        if (anim != null)
        {
            if (HasParameter("Hurt"))
            {
                anim.SetTrigger("Hurt");
            }
            else
            {
                StartCoroutine(FlashRoutine()); // Hurt 애니메이션이 없으면 메테리얼 깜빡임으로 대체
            }
        }

        currentHP -= damage;
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
    private int ApplyElementalDamage(int damage)
    {
        if (cachedPlayer == null) return damage;
        PlayerStatus.Stance stance = cachedPlayer.currentStance;

        bool isWeakness =
            (element == EnemyElement.Green && stance == PlayerStatus.Stance.Red) ||
            (element == EnemyElement.Blue && stance == PlayerStatus.Stance.Green) ||
            (element == EnemyElement.Red && stance == PlayerStatus.Stance.Blue);

        bool isResist =
         (element == EnemyElement.Red && stance == PlayerStatus.Stance.Green) ||
         (element == EnemyElement.Green && stance == PlayerStatus.Stance.Blue) ||
         (element == EnemyElement.Blue && stance == PlayerStatus.Stance.Red);

        if (isWeakness)
        {
            Debug.Log($"[EnemyStatus] 약점! {damage} → {Mathf.RoundToInt(damage * weaknessMultiplier)}");
            return Mathf.RoundToInt(damage * weaknessMultiplier);
        }
        if (isResist)
        {
            Debug.Log($"[EnemyStatus] 저항! {damage} → {Mathf.RoundToInt(damage * resistMultiplier)}");
            return Mathf.RoundToInt(damage * resistMultiplier);
        }

        return damage;
    }
    public void Heal(int amount)
    {
        if (currentHP <= 0)
        {
            return; // 이미 죽은 적은 회복 불가
        }
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    #endregion

    #region Stun Logic

    public void ApplyStun(float duration)
    {
        if (isBoss || currentHP <= 0 || isStunned)
        {
            return;
        }

        StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;

        yield return new WaitForSeconds(duration);

        isStunned = false;
    }

    #endregion

    #region Death Sequence

    private void Die()
    {
        // 정령(BossSpirit) 등 예외 오브젝트는 자폭 처리되므로 여기서 일반 사망 로직 진행 안 함
        if (GetComponent<BossSpirit>() != null)
        {
            return;
        }

        IsDead = true; // 사망 상태 설정

        OnEnemyDeath?.Invoke(); // 사망 이벤트 발행

        if (isBoss && cachedPortalVisual != null) // 포탈 활성화
        {
            cachedPortalVisual.SetActive(true);
        }

        if (anim != null)
        {
            if (HasParameter("Death"))
            {
                anim.SetTrigger("Death");
            }
            else
            {
                StartCoroutine(FadeRoutine()); // Death 애니메이션이 없으면 페이드 아웃으로 대체
            }
        }

        if (!isBoss)
        {
            if (TryGetComponent(out Collider2D col))
            {
                col.enabled = false;
            }

            if (TryGetComponent(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            // 본인을 제외한 모든 MonoBehaviour 스크립트 비활성화
            MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null && script != this)
                {
                    script.enabled = false;
                }
            }
        }

        Destroy(gameObject, dieAnimationLength); // 애니메이션 대기 후 파괴
    }
    private void CachePortal()
    {
        GameObject portalParent = GameObject.FindWithTag("Portal");

        if (portalParent != null)
        {
            if (portalParent.transform.childCount > 0)
            {
                cachedPortalVisual = portalParent.transform.GetChild(0).gameObject;
            }
        }
        else
        {
            Debug.LogWarning("Portal 태그 없음");
        }
    }

    #endregion

    #region VFX Routines

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

    #endregion
}