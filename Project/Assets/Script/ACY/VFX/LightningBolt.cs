using System.Collections;
using UnityEngine;

public class LightningBolt : MonoBehaviour
{
    [SerializeField] private int damage = 10;

    private string poolName;
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Initialize(string pool, Vector2 dir)
    {
        poolName = pool;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        StartCoroutine(PlayAndReturn());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);
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