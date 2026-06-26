using System.Collections;
using UnityEngine;

public class Hurricane : MonoBehaviour
{
    [Header("소용돌이 기본 설정")]
    [SerializeField] private float duration = 3f; // 생존 시간
    [SerializeField] private float moveSpeed = 5f; // 이동 속도
    [SerializeField] private float fadeDuration = 0.5f; // 페이드아웃 지속 시간

    [Header("소용돌이 원소별 대미지 설정")]
    [Tooltip("기본 스프라이트(파랑) 상태일 때 대미지")]
    [SerializeField] private int blueDamage = 10;

    [Tooltip("그린 보스 상태일 때 대미지")]
    [SerializeField] private int greenDamage = 15;

    [Tooltip("레드 보스 상태일 때 대미지")]
    [SerializeField] private int redDamage = 20;

    private int finalDamage;
    private SpriteRenderer spriteRenderer;
    private Coroutine returnRoutine;
    private Vector3 moveDirection;

    // 중복 충돌 및 페이드 중 충돌 방지용 플래그
    private bool isDisposing = false;

    // ★ 추가: 오브젝트 풀 재사용 시 속도 복구를 위한 원본 속도 저장 변수
    private float originMoveSpeed;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // ★ 처음에 기입한 이동 속도를 미리 기억해둡니다.
        originMoveSpeed = moveSpeed;
    }

    private void OnEnable()
    {
        isDisposing = false;

        // ★ 오브젝트 풀에서 다시 켜질 때 이동 속도를 원상태로 복구합니다.
        moveSpeed = originMoveSpeed;

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f; // 알파 값을 다시 최대로 복원
            spriteRenderer.color = color;
        }

        if (returnRoutine != null) StopCoroutine(returnRoutine);
        // 시간 지나면 자동으로 페이드아웃 시작
        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private void Update()
    {
        // ★ 핵심 수정: 사라지는 중(isDisposing == true)이 아닐 때만 이동합니다.
        if (!isDisposing)
        {
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
        }
    }

    public void Initialize(EnemyStatus.EnemyElement bossElement, Vector3 dir)
    {
        moveDirection = dir.normalized;

        if (spriteRenderer == null) return;

        // 원소 상태에 따라 색상과 대미지 결정
        switch (bossElement)
        {
            case EnemyStatus.EnemyElement.Red:
                spriteRenderer.color = Color.red;
                finalDamage = redDamage;
                break;

            case EnemyStatus.EnemyElement.Green:
                spriteRenderer.color = Color.green;
                finalDamage = greenDamage;
                break;

            case EnemyStatus.EnemyElement.Blue:
            default:
                // 원래 이미지 색상을 유지하기 위해 white로 설정
                spriteRenderer.color = Color.white;
                finalDamage = blueDamage;
                break;
        }
    }

    // 플레이어 충돌 처리
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 이미 사라지는 중이라면 충돌 무시
        if (isDisposing) return;

        if (collision.CompareTag("Player"))
        {
            IDamageable damageable = collision.GetComponent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage);
                Debug.Log($"[Hurricane] 플레이어 충돌! 대미지 {finalDamage} 부여.");
            }

            // 충돌 직후 대미지를 준 후 즉시 페이드아웃 코루틴 시작
            StartCoroutine(FadeOutAndReturnRoutine());
        }
    }

    // 시간 경과 후 자동 페이드아웃 시작 루틴
    private IEnumerator ReturnRoutine()
    {
        yield return new WaitForSeconds(duration);

        // 이미 충돌해서 사라지는 중이 아니라면 페이드아웃 시작
        if (!isDisposing)
        {
            StartCoroutine(FadeOutAndReturnRoutine());
        }
    }

    // 서서히 투명해진 후 풀에 반납하는 코루틴
    private IEnumerator FadeOutAndReturnRoutine()
    {
        // 중복 실행 방지
        if (isDisposing) yield break;
        isDisposing = true;

        moveSpeed = 0f;

        if (spriteRenderer == null)
        {
            // 혹시 렌더러가 없으면 즉시 반납
            PoolingManager.Instance.Return("Hurricane", gameObject);
            yield break;
        }

        float elapsed = 0f;
        Color originalColor = spriteRenderer.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            // 알파 값을 1에서 0으로 선형 보간 (Lerp)
            float newAlpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, newAlpha);

            yield return null;
        }

        // 완전히 투명해진 후 풀에 반납
        PoolingManager.Instance.Return("Hurricane", gameObject);
    }
}