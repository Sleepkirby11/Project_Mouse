using System.Collections;
using UnityEngine;

public class BlueBossWarningLine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float blinkSpeed = 0.1f; // 깜빡임 속도

    private Color originalColor;
    private Coroutine currentEffectRoutine;
    private string myPoolKey; 

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        originalColor = spriteRenderer.color;
    }

    public void ActivateWarning(float duration, string poolKey)
    {
        myPoolKey = poolKey; // 풀 키 저장

        // 기존에 돌던 연출이 있다면 정지
        if (currentEffectRoutine != null) StopCoroutine(currentEffectRoutine);

        spriteRenderer.enabled = true;
        spriteRenderer.color = originalColor;

        // 깜빡임 및 페이드아웃 효과 시작
        currentEffectRoutine = StartCoroutine(WarningEffectRoutine(duration));
    }

    private IEnumerator WarningEffectRoutine(float duration)
    {
        float timer = 0f;
        // 전체 유지 시간의 70%는 깜빡이고, 나머지 30% 동안 페이드아웃
        float blinkDuration = duration * 0.7f;
        float fadeDuration = duration * 0.3f;

        // 깜빡임
        while (timer < blinkDuration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(blinkSpeed);
            timer += blinkSpeed;
        }

        spriteRenderer.enabled = true; // 페이드아웃 전에는 확실히 켜주기
        timer = 0f;

        // 페이드 아웃
        Color startColor = spriteRenderer.color;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            Color newColor = startColor;
            newColor.a = Mathf.Lerp(startColor.a, 0f, timer / fadeDuration);
            spriteRenderer.color = newColor;
            yield return null;
        }

        if (PoolingManager.Instance != null && !string.IsNullOrEmpty(myPoolKey))
        {
            PoolingManager.Instance.Return(myPoolKey, gameObject);
        }
        else
        {
            // 예외 처리
            gameObject.SetActive(false);
        }
    }
}