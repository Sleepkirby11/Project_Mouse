using UnityEngine;

public class Meteor : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float fallSpeed = 8f;

    [Header("데미지")]
    [SerializeField] private int damage = 1;

    private Vector3 targetPos;
    private bool isFalling;

    private const string METEOR_KEY = "RedBossMeteor";

    public void Init(Vector3 targetPos)
    {
        this.targetPos = targetPos;
        isFalling = true;
    }

    private void Update()
    {
        if (!isFalling) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            fallSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPos) <= 0.05f)
        {
            Explode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isFalling) return;

        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            Explode();
        }
    }

    private void Explode()
    {
        isFalling = false;

        // 나중에 여기서 장판 생성
        PoolingManager.Instance.Return(METEOR_KEY, gameObject);
    }
}