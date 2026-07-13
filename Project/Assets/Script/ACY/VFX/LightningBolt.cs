using System.Collections;
using UnityEngine;

public class LightningBolt : MonoBehaviour
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float stunDuration = 1f;

    public System.Action onHitPlayer;
    private string poolName;
    private EnemyStatus.EnemyElement currentElement;
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Initialize(string pool, Vector2 dir, EnemyStatus.EnemyElement element)
    {
        poolName = pool;
        currentElement = element;
        onHitPlayer = null;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.SFX.RGB_Lightning);
        }

        StartCoroutine(PlayAndReturn());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);

            onHitPlayer?.Invoke();

            if (currentElement == EnemyStatus.EnemyElement.Blue)
            {
                if (other.TryGetComponent(out IStunnable stunnable))
                    stunnable.ApplyStun(stunDuration);
            }
        }
    }

    private IEnumerator PlayAndReturn()
    {
        animator.Play(0);
        yield return null;
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        PoolingManager.Instance.Return(poolName, gameObject);
    }
}