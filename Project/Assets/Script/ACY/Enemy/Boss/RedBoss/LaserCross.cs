using System.Collections;
using UnityEngine;

public class LaserCross : MonoBehaviour
{
    [Header("경고선")]
    [SerializeField] private SpriteRenderer[] warningLines;

    [Header("레이저")]
    [SerializeField] private Transform[] laserLines;

    [Header("레이저 스프라이트")]
    [SerializeField] private SpriteRenderer[] laserRenderers;

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip laserSFX;

    [Header("회전")]
    [SerializeField] private float startRotateSpeed = 8f;
    [SerializeField] private float maxRotateSpeed = 45f;
    [SerializeField] private float acceleration = 12f;

    [Header("레이저 굵기")]
    [SerializeField] private float laserThickness = 1.5f;

    private const string LASER_KEY = "RedBossLaser";
    private float duration;

    private float rotateSpeed;
    private float rotateDirection; // 1(시계) -1(반시계) 방향
    private Coroutine laserRoutine;
    private void OnDisable()
    {
        if (laserRoutine != null)
        {
            StopCoroutine(laserRoutine);
            laserRoutine = null;
        }
    }
    public void Init(float warningTime, float laserDuration)
    {
        duration = laserDuration;

        // ---------- 상태 초기화 ----------

        transform.rotation = Quaternion.identity;

        rotateSpeed = startRotateSpeed;
        rotateDirection = Random.value < 0.5f ? 1f : -1f;

        SetWarning(true);
        SetLaser(false);

        SetWarningAlpha(0.15f);
        SetLaserAlpha(1f);

        // 레이저 두께 초기화
        for (int i = 0; i < laserLines.Length; i++)
        {
            Vector3 scale = laserLines[i].localScale;

            scale.y = 0f;

            laserLines[i].localScale = scale;
        }

        // 기존 코루틴 제거
        if (laserRoutine != null)
        {
            StopCoroutine(laserRoutine);
        }

        laserRoutine = StartCoroutine(LaserRoutine(warningTime));
    }

    private IEnumerator LaserRoutine(float warningTime)
    {
        SetLaser(false);

        // ---------------- 경고선 ----------------
        SetWarning(true);

        SetWarningAlpha(0.15f);

        yield return new WaitForSeconds(warningTime * 0.5f);

        // 한번 강하게 점멸
        SetWarningAlpha(0.6f);

        yield return new WaitForSeconds(warningTime * 0.5f);

        SetWarning(false);

        // ---------------- 레이저 등장 ----------------
        SetLaser(true);

        // 시작 투명도
        SetLaserAlpha(1f);

        // 시작 굵기 0
        for (int i = 0; i < laserLines.Length; i++)
        {
            Vector3 scale = laserLines[i].localScale;

            scale.y = 0f;

            laserLines[i].localScale = scale;
        }

        // 사운드
        if (audioSource != null && laserSFX != null)
        {
            audioSource.PlayOneShot(laserSFX);
        }

        // 카메라 흔들림
        /*
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.25f, 0.15f);
        }
        */

        // ---------------- 굵어지는 연출 ----------------
        float growTime = 0.2f;
        float growTimer = 0f;

        while (growTimer < growTime)
        {
            growTimer += Time.deltaTime;

            float value = Mathf.Lerp(0f, laserThickness, growTimer / growTime);

            for (int i = 0; i < laserLines.Length; i++)
            {
                Vector3 scale = laserLines[i].localScale;

                scale.y = value;

                laserLines[i].localScale = scale;
            }

            yield return null;
        }

        // ---------------- 회전 ----------------
        float rotateTimer = 0f;

        while (rotateTimer < duration)
        {
            rotateSpeed += acceleration * Time.deltaTime;

            rotateSpeed = Mathf.Clamp(rotateSpeed, startRotateSpeed, maxRotateSpeed);

            transform.Rotate(0f, 0f, rotateDirection * rotateSpeed * Time.deltaTime);

            rotateTimer += Time.deltaTime;

            yield return null;
        }

        // ---------------- 서서히 사라짐 ----------------
        float fadeTimer = 0f;
        float fadeDuration = 0.5f;

        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;

            float alpha = Mathf.Lerp(1f, 0f, fadeTimer / fadeDuration);

            SetLaserAlpha(alpha);

            yield return null;
        }

        PoolingManager.Instance.Return(LASER_KEY, gameObject);
    }

    private void SetWarning(bool value)
    {
        for (int i = 0; i < warningLines.Length; i++)
        {
            warningLines[i].gameObject.SetActive(value);
        }
    }

    private void SetWarningAlpha(float alpha)
    {
        for (int i = 0; i < warningLines.Length; i++)
        {
            Color color = warningLines[i].color;

            color.a = alpha;

            warningLines[i].color = color;
        }
    }

    private void SetLaser(bool value)
    {
        for (int i = 0; i < laserLines.Length; i++)
        {
            laserLines[i].gameObject.SetActive(value);
        }
    }

    private void SetLaserAlpha(float alpha)
    {
        for (int i = 0; i < laserRenderers.Length; i++)
        {
            Color color = laserRenderers[i].color;

            color.a = alpha;

            laserRenderers[i].color = color;
        }
    }
}