using System.Collections;
using UnityEngine;

public class Bird : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float speed = 15f;      // 새의 비행 속도
    [SerializeField] private float despawnX = -25f;  // 화면 왼쪽 끝 좌표 (넘어가면 풀링 반환)

    [Header("경고선 설정")]
    [SerializeField] private SpriteRenderer warningLine;
    [SerializeField] private float warningTime = 1f;

    [Header("공격 설정")]
    [SerializeField] private int damage = 10;     //  대미지

    private const string BIRD_KEY = "GreenBossBird";

    private bool isFlying = false; // 경고가 끝나고 날아가고 있는지 여부
    private Coroutine attackRoutine;

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    public void Init(float customWarningTime, float customSpeed = -1f)
    {
        if (customSpeed > 0)
        {
            speed = customSpeed;
        }

        warningTime = customWarningTime;

        isFlying = false;

        if (warningLine != null)
        {
            warningLine.gameObject.SetActive(false);
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        attackRoutine = StartCoroutine(AttackRoutine(warningTime));
    }
    private IEnumerator AttackRoutine(float warningTime)
    {
        if (warningLine != null)
        {
            warningLine.gameObject.SetActive(true);
            SetWarningAlpha(0.15f);

            yield return new WaitForSeconds(warningTime * 0.5f);

            SetWarningAlpha(0f);

            yield return new WaitForSeconds(warningTime * 0.5f);

            warningLine.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(warningTime);
        }

        isFlying = true;
    }

    private void SetWarningAlpha(float alpha)
    {
        Color color = warningLine.color;
        color.a = alpha;
        warningLine.color = color;
    }
    private void Update()
    {
        if(!isFlying)
        {
            return;
        }
        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.World);

        if (transform.position.x < despawnX)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            collision.GetComponent<IDamageable>()?.TakeDamage(damage);
        }
    }

    private void ReturnToPool()
    {
        PoolingManager.Instance.Return(BIRD_KEY, gameObject);
    }
}