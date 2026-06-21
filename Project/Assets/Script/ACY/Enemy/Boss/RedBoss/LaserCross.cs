using System.Collections;
using UnityEngine;

public class LaserCross : MonoBehaviour
{
    #region Settings & Variables

    [Header("경고선")]
    [SerializeField] private SpriteRenderer[] warningLines;

    [Header("레이저")]
    [SerializeField] private Transform[] laserLines;

    [Header("레이저 스프라이트")]
    [SerializeField] private SpriteRenderer[] laserRenderers;

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip laserSFX;

    [Header("레이저 굵기")]
    [SerializeField] private float laserThickness = 1.5f;

    private const string LASER_KEY = "RedBossLaser";
    private float duration;
    private bool isEnraged = false;

    private float rotateDirection; // 1(시계) -1(반시계) 방향
    private Coroutine laserRoutine;

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        if (laserRoutine != null)
        {
            StopCoroutine(laserRoutine);
            laserRoutine = null;
        }
    }

    #endregion

    #region Laser Initialization

    public void Init(float warningTime, float laserDuration, bool enraged = false)
    {
        duration = laserDuration;
        isEnraged = enraged;

        // ---------- 상태 초기화 ----------
        transform.rotation = Quaternion.identity;
        rotateDirection = Random.value < 0.5f ? 1f : -1f;

        SetWarning(true);
        SetLaser(false);

        SetWarningAlpha(0.15f);
        SetLaserAlpha(1f);

        // 레이저 두께 초기화
        if (laserLines != null)
        {
            for (int i = 0; i < laserLines.Length; i++)
            {
                if (laserLines[i] != null)
                {
                    Vector3 scale = laserLines[i].localScale;
                    scale.y = 0f;
                    laserLines[i].localScale = scale;
                }
            }
        }

        // 기존 코루틴 제거
        if (laserRoutine != null)
        {
            StopCoroutine(laserRoutine);
        }

        laserRoutine = StartCoroutine(LaserRoutine(warningTime));
    }

    #endregion

    #region Laser Sequence Routine

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
        if (laserLines != null)
        {
            for (int i = 0; i < laserLines.Length; i++)
            {
                if (laserLines[i] != null)
                {
                    Vector3 scale = laserLines[i].localScale;
                    scale.y = 0f;
                    laserLines[i].localScale = scale;
                }
            }
        }

        // 사운드
        if (audioSource != null && laserSFX != null)
        {
            audioSource.PlayOneShot(laserSFX);
        }

        // ---------------- 굵어지는 연출 ----------------
        float growTime = 0.2f;
        float growTimer = 0f;

        while (growTimer < growTime)
        {
            growTimer += Time.deltaTime;

            float value = Mathf.Lerp(0f, laserThickness, growTimer / growTime);

            if (laserLines != null)
            {
                for (int i = 0; i < laserLines.Length; i++)
                {
                    if (laserLines[i] != null)
                    {
                        Vector3 scale = laserLines[i].localScale;
                        scale.y = value;
                        laserLines[i].localScale = scale;
                    }
                }
            }

            yield return null;
        }

        // ---------------- 회전 ----------------
        float rotateTimer = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0f, 0f, 90f * rotateDirection);

        while (rotateTimer < duration)
        {
            rotateTimer += Time.deltaTime;
            float t = rotateTimer / duration;
            t = t * t * (3f - 2f * t);

            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            yield return null;
        }
        transform.rotation = targetRotation; 

        // ---------------- 분노 시 역회전 추가 ----------------
        if (isEnraged)
        {
            yield return new WaitForSeconds(0.5f); // 0.5초 정지

            rotateTimer = 0f;
            Quaternion reverseStartRot = transform.rotation;
            Quaternion reverseTargetRot = startRotation; // 기존 각도로 복귀

            while (rotateTimer < duration)
            {
                rotateTimer += Time.deltaTime;
                float t = rotateTimer / duration;
                t = t * t * (3f - 2f * t); 

                transform.rotation = Quaternion.Lerp(reverseStartRot, reverseTargetRot, t);
                yield return null;
            }
            transform.rotation = reverseTargetRot;
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

        if (PoolingManager.Instance != null)
        {
            PoolingManager.Instance.Return(LASER_KEY, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    #endregion

    #region Rendering Helpers

    private void SetWarning(bool value)
    {
        if (warningLines == null) return;
        for (int i = 0; i < warningLines.Length; i++)
        {
            if (warningLines[i] != null)
            {
                warningLines[i].gameObject.SetActive(value);
            }
        }
    }

    private void SetWarningAlpha(float alpha)
    {
        if (warningLines == null) return;
        for (int i = 0; i < warningLines.Length; i++)
        {
            if (warningLines[i] != null)
            {
                Color color = warningLines[i].color;
                color.a = alpha;
                warningLines[i].color = color;
            }
        }
    }

    private void SetLaser(bool value)
    {
        if (laserLines == null) return;
        for (int i = 0; i < laserLines.Length; i++)
        {
            if (laserLines[i] != null)
            {
                laserLines[i].gameObject.SetActive(value);
            }
        }
    }

    private void SetLaserAlpha(float alpha)
    {
        if (laserRenderers == null) return;
        for (int i = 0; i < laserRenderers.Length; i++)
        {
            if (laserRenderers[i] != null)
            {
                Color color = laserRenderers[i].color;
                color.a = alpha;
                laserRenderers[i].color = color;
            }
        }
    }

    #endregion
}