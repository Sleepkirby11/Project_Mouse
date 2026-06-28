using System.Collections;
using UnityEngine;

public class Hurricane : MonoBehaviour
{
    #region Inspector Fields
    [Header("소용돌이 기본 설정")]
    [SerializeField] private float duration = 3f; // 지속 시간
    [SerializeField] private float moveSpeed = 5f; // 이동 속도
    [SerializeField] private float fadeDuration = 0.5f; // 페이드아웃 지속 시간

    [Header("소용돌이 속성별 수치 설정")]
    [Tooltip("블루 속성 피해량 및 수직 밀어내는 힘")]
    [SerializeField] private int blueDamage = 10;
    [SerializeField] private float launchForceY = 15f;

    [Tooltip("그린 속성 피해량")]
    [SerializeField] private int greenDamage = 15;

    [Tooltip("레드 속성 피해량")]
    [SerializeField] private int redDamage = 20;

    [Header("Pool Settings")]
    [SerializeField] private string poolKey = "Hurricane";
    #endregion

    #region Private Fields
    private EnemyStatus.EnemyElement currentElement;
    private int finalDamage;
    private SpriteRenderer spriteRenderer;
    private Coroutine returnRoutine;
    private Vector3 moveDirection;

    // 중복 충돌 및 이중 반환 방지 플래그
    private bool isDisposing = false;

    // 원본 이동 속도 저장 (오브젝트 풀 반환 시 리셋용)
    private float originMoveSpeed;
    #endregion

    #region Public Fields & Actions
    public System.Action onHitPlayer;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // 최초 설정된 기본 이동 속도를 기억해 둡니다.
        originMoveSpeed = moveSpeed;
    }

    private void OnEnable()
    {
        isDisposing = false;
        onHitPlayer = null;
        // 오브젝트 풀에서 꺼낼 때 이동 속도를 초기 속도로 리셋합니다.
        moveSpeed = originMoveSpeed;

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f; // 알파값을 다시 최대로 복구
            spriteRenderer.color = color;
        }

        if (returnRoutine != null) StopCoroutine(returnRoutine);
        // 지속 시간 만료 시 자동 페이드아웃 및 반환 루틴 시작
        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private void Update()
    {
        // 소용돌이가 반환 처리 중(isDisposing == true)이 아닐 때만 이동합니다.
        if (!isDisposing)
        {
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
        }
    }
    #endregion

    #region Public Initialization
    public void Initialize(EnemyStatus.EnemyElement bossElement, Vector3 dir)
    {
        currentElement = bossElement;
        moveDirection = dir.normalized;

        if (spriteRenderer == null) return;

        // 보스의 현재 속성에 따라 색상 및 데미지 차등 적용
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
                // 블루 이미지는 기본 화이트 컬러로 스프라이트 본연의 색을 표현합니다.
                spriteRenderer.color = Color.white;
                finalDamage = blueDamage;
                break;
        }
    }
    #endregion

    #region Collision & Return Routines
    // 플레이어 충돌 처리
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDisposing) return;
        if (!collision.CompareTag("Player")) return;

        IDamageable damageable = collision.GetComponent<IDamageable>();
        damageable?.TakeDamage(finalDamage);

        onHitPlayer?.Invoke();

        if (currentElement == EnemyStatus.EnemyElement.Blue)
        {
            if (collision.TryGetComponent(out PlayerStatus playerStatus))
                playerStatus.LaunchByWater(launchForceY);
        }

        StartCoroutine(FadeOutAndReturnRoutine());
    }

    // 지정된 시간이 지난 후 자동 풀 반환을 위한 대기 루틴
    private IEnumerator ReturnRoutine()
    {
        yield return new WaitForSeconds(duration);

        // 이미 플레이어에 닿아 소멸 처리 중인 경우가 아니라면 소멸 루틴 실행
        if (!isDisposing)
        {
            StartCoroutine(FadeOutAndReturnRoutine());
        }
    }

    // 서서히 투명해지며 풀에 반환되는 코루틴
    private IEnumerator FadeOutAndReturnRoutine()
    {
        // 중복 진입 방지
        if (isDisposing) yield break;
        isDisposing = true;

        moveSpeed = 0f;

        if (spriteRenderer == null)
        {
            PoolingManager.Instance.Return(poolKey, gameObject);
            yield break;
        }

        float elapsed = 0f;
        Color originalColor = spriteRenderer.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            // 알파값을 1에서 0으로 서서히 보간 (Lerp)
            float newAlpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, newAlpha);

            yield return null;
        }

        // 완전히 투명해지면 오브젝트 풀로 반환
        PoolingManager.Instance.Return(poolKey, gameObject);
    }
    #endregion
}