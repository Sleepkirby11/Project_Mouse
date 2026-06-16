using System.Collections;
using UnityEngine;

public class Frog : MonoBehaviour
{
    [Header("����� ����")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float hitboxActivateDelay = 0.5f;  // ħ ���� Ÿ�̹�
    [SerializeField] private float hitboxDuration = 0.3f;       // ���� ���� �ð�
    [SerializeField] private float animDuration = 2.0f;


    private Animator anim;
    private const string POOL_KEY = "GreenBossFrog";
    private BoxCollider2D hitbox;

    private void Awake()
    {
        hitbox = GetComponent<BoxCollider2D>();
        anim = GetComponent<Animator>();
    }

    private void OnDisable()
    {
        if (anim != null)
        {
            anim.Play("Frog", 0, 0f); // ��ȯ �� �ִϸ��̼� ó������ ����
            anim.Update(0f);
            anim.enabled = false;     // �ִϸ����� ��Ȱ��ȭ
        }
    }

    private void OnEnable()
    {
        if (anim != null)
        {
            anim.enabled = true;
        }

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
        StartCoroutine(FrogRoutine());
    }

    private IEnumerator FrogRoutine()
    {
        // ���� Ÿ�ֿ̹� ���� ON
        yield return new WaitForSeconds(hitboxActivateDelay);
        if (hitbox != null)
        {
            hitbox.enabled = true;
        }
        // ���� ����
        yield return new WaitForSeconds(hitboxDuration);
        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
        // �ִϸ��̼� ������ ���
        yield return new WaitForSeconds(animDuration - hitboxActivateDelay - hitboxDuration);

        PoolingManager.Instance.Return(POOL_KEY, gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);
        }
    }
}