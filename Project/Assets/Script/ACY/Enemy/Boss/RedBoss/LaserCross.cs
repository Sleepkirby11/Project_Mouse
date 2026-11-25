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

    [Header("파티클 / 연출 설정")]
    [SerializeField] private float particleBeamSpan = 36f;

    private const string LASER_KEY = "RedBossLaser";
    private float duration;
    private bool isEnraged = false;

    private float rotateDirection; // 1(시계) -1(반시계) 방향
    private Coroutine laserRoutine;
    private LaserCrossParticles visuals;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureVisualsInitialized();
    }

    private void OnDisable()
    {
        if (laserRoutine != null)
        {
            StopCoroutine(laserRoutine);
            laserRoutine = null;
        }

        if (visuals != null)
        {
            visuals.StopAndClearAllParticles();
        }
    }

    private void EnsureVisualsInitialized()
    {
        if (visuals == null)
        {
            visuals = GetComponent<LaserCrossParticles>();
            if (visuals == null)
            {
                visuals = gameObject.AddComponent<LaserCrossParticles>();
            }
        }
        visuals.Initialize(warningLines, laserLines, laserRenderers, laserThickness, particleBeamSpan);
    }

    #endregion

    #region Laser Initialization

    public void Init(float warningTime, float laserDuration, bool enraged = false)
    {
        EnsureVisualsInitialized();

        duration = laserDuration;
        isEnraged = enraged;

        // ---------- 상태 초기화 ----------
        transform.rotation = Quaternion.identity;
        rotateDirection = Random.value < 0.5f ? 1f : -1f;

        visuals.StopAndClearAllParticles();
        SetWarning(true);
        SetLaser(false);

        visuals.UpdateWarningVisuals(0f);
        visuals.SetLaserAlpha(1f);

        // 레이저 두께 초기화
        SetBeamThickness(0f);

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

        // ---------------- 경고선 + 차징 파티클 ----------------
        SetWarning(true);
        visuals.PlayWarningParticles(true);

        float warnTimer = 0f;
        bool lockOnBurstTriggered = false;
        while (warnTimer < warningTime)
        {
            warnTimer += Time.deltaTime;
            float progress = warningTime > 0f ? Mathf.Clamp01(warnTimer / warningTime) : 1f;
            visuals.UpdateWarningVisuals(progress);

            if (!lockOnBurstTriggered && progress >= 0.82f)
            {
                lockOnBurstTriggered = true;
                visuals.EmitWarningLockOnBurst();
            }

            yield return null;
        }

        SetWarning(false);
        visuals.PlayWarningParticles(false);

        // ---------------- 레이저 등장 ----------------
        SetLaser(true);
        visuals.SetLaserAlpha(1f);
        SetBeamThickness(0f);
        visuals.PlayBeamParticles(true, burstOnStart: true);

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

            float t = Mathf.Clamp01(growTimer / growTime);
            float eased = 1f - (1f - t) * (1f - t);
            float value = Mathf.Lerp(0f, laserThickness, eased);

            SetBeamThickness(value);
            visuals.UpdateCorePulse(Time.time);

            yield return null;
        }
        SetBeamThickness(laserThickness);

        // ---------------- 회전 ----------------
        float rotateTimer = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0f, 0f, 90f * rotateDirection);

        while (rotateTimer < duration)
        {
            rotateTimer += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(rotateTimer / duration) : 1f;
            t = t * t * (3f - 2f * t);

            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            visuals.UpdateCorePulse(Time.time);
            yield return null;
        }
        transform.rotation = targetRotation;

        // ---------------- 분노 시 역회전 추가 ----------------
        if (isEnraged)
        {
            float pauseTimer = 0f;
            while (pauseTimer < 0.5f)
            {
                pauseTimer += Time.deltaTime;
                visuals.UpdateCorePulse(Time.time);
                yield return null;
            }

            rotateTimer = 0f;
            Quaternion reverseStartRot = transform.rotation;
            Quaternion reverseTargetRot = startRotation; // 기존 각도로 복귀

            while (rotateTimer < duration)
            {
                rotateTimer += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(rotateTimer / duration) : 1f;
                t = t * t * (3f - 2f * t);

                transform.rotation = Quaternion.Lerp(reverseStartRot, reverseTargetRot, t);
                visuals.UpdateCorePulse(Time.time);
                yield return null;
            }
            transform.rotation = reverseTargetRot;
        }

        // ---------------- 서서히 사라짐 ----------------
        visuals.PlayBeamParticles(false);

        float fadeTimer = 0f;
        float fadeDuration = 0.5f;

        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(fadeTimer / fadeDuration);
            float alpha = Mathf.Lerp(1f, 0f, progress);

            visuals.SetLaserAlpha(alpha);
            SetBeamThickness(Mathf.Lerp(laserThickness, laserThickness * 0.25f, progress));

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

    #region Transform Helpers

    private void SetBeamThickness(float thickness)
    {
        if (laserLines == null) return;
        for (int i = 0; i < laserLines.Length; i++)
        {
            if (laserLines[i] != null)
            {
                Vector3 scale = laserLines[i].localScale;
                scale.y = thickness;
                laserLines[i].localScale = scale;
            }
        }
    }

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

    #endregion
}
