using System.Collections;
using System.Collections.Generic;
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

    // 런타임 생성 비주얼/파티클 캐시
    private bool visualsInitialized = false;
    private readonly List<SpriteRenderer> midGlowRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> coreRenderers = new List<SpriteRenderer>();
    private readonly List<Transform> midGlowTransforms = new List<Transform>();
    private readonly List<Transform> coreTransforms = new List<Transform>();

    private readonly List<SpriteRenderer> warningEdgeRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> warningGuideRenderers = new List<SpriteRenderer>();
    private readonly List<Transform> warningGuideTransforms = new List<Transform>();

    private ParticleSystem warningChargePs;
    private readonly List<ParticleSystem> warningAxisPs = new List<ParticleSystem>();
    private ParticleSystem coreVortexPs;
    private readonly List<ParticleSystem> beamEmberPs = new List<ParticleSystem>();
    private readonly List<ParticleSystem> allParticles = new List<ParticleSystem>();

    private static Material sharedUnlitMat;
    private static Material sharedSquareParticleMat;
    private static Material sharedDiamondParticleMat;
    private static Texture2D sharedPixelSquareTex;
    private static Texture2D sharedPixelDiamondTex;

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

        StopAndClearAllParticles();
    }

    #endregion

    #region Visual & Particle Setup

    private static void EnsureSharedResources()
    {
        if (sharedUnlitMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                sharedUnlitMat = new Material(shader)
                {
                    name = "LaserCross_UnlitMat",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        if (sharedPixelSquareTex == null)
        {
            sharedPixelSquareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            sharedPixelSquareTex.SetPixels(pixels);
            sharedPixelSquareTex.Apply();
        }

        if (sharedPixelDiamondTex == null)
        {
            const int size = 7;
            sharedPixelDiamondTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            int center = size / 2;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dist = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    float a = dist <= center ? (dist <= 1 ? 1f : 0.75f) : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sharedPixelDiamondTex.SetPixels(pixels);
            sharedPixelDiamondTex.Apply();
        }

        if (sharedSquareParticleMat == null)
        {
            sharedSquareParticleMat = CreateSharedParticleMaterial("LaserCross_SquareParticleMat", sharedPixelSquareTex);
        }

        if (sharedDiamondParticleMat == null)
        {
            sharedDiamondParticleMat = CreateSharedParticleMaterial("LaserCross_DiamondParticleMat", sharedPixelDiamondTex);
        }
    }

    private static Material CreateSharedParticleMaterial(string matName, Texture2D tex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        return new Material(shader)
        {
            name = matName,
            mainTexture = tex,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void EnsureVisualsInitialized()
    {
        if (visualsInitialized)
            return;

        visualsInitialized = true;
        EnsureSharedResources();

        // 경고선 색상 + 상하 경계 레일 + 수렴 가이드 라인 구성
        if (warningLines != null)
        {
            for (int i = 0; i < warningLines.Length; i++)
            {
                SpriteRenderer warnSr = warningLines[i];
                if (warnSr == null) continue;

                warnSr.color = new Color(0.85f, 0.08f, 0.14f, 0.14f);
                warnSr.sortingOrder = 97;
                if (sharedUnlitMat != null)
                {
                    warnSr.sharedMaterial = sharedUnlitMat;
                }

                Transform warnTf = warnSr.transform;
                SpriteRenderer topEdge = CreateBeamLayer(warnTf, "WarningEdgeTop", 0.045f, 98, warnSr.sprite, new Color(1f, 0.25f, 0.2f, 0.75f));
                topEdge.transform.localPosition = new Vector3(0f, 0.48f, 0f);
                warningEdgeRenderers.Add(topEdge);

                SpriteRenderer bottomEdge = CreateBeamLayer(warnTf, "WarningEdgeBottom", 0.045f, 98, warnSr.sprite, new Color(1f, 0.25f, 0.2f, 0.75f));
                bottomEdge.transform.localPosition = new Vector3(0f, -0.48f, 0f);
                warningEdgeRenderers.Add(bottomEdge);

                SpriteRenderer guideSr = CreateBeamLayer(warnTf, "WarningConvergeGuide", 1f, 99, warnSr.sprite, new Color(1f, 0.5f, 0.22f, 0.55f));
                warningGuideRenderers.Add(guideSr);
                warningGuideTransforms.Add(guideSr.transform);
            }
        }

        // 외곽 빔 + 중간 화염 오라 + 중심 고온 코어 구성
        if (laserLines != null)
        {
            for (int i = 0; i < laserLines.Length; i++)
            {
                Transform beamTf = laserLines[i];
                if (beamTf == null) continue;

                SpriteRenderer outerSr = beamTf.GetComponent<SpriteRenderer>();
                if (outerSr != null)
                {
                    outerSr.color = new Color(0.9f, 0.08f, 0.14f, 0.8f);
                    outerSr.sortingOrder = 99;
                    if (sharedUnlitMat != null)
                    {
                        outerSr.sharedMaterial = sharedUnlitMat;
                    }

                    SpriteRenderer midSr = CreateBeamLayer(beamTf, "BeamMidGlow", 0.58f, 100, outerSr.sprite, new Color(1f, 0.36f, 0.12f, 0.92f));
                    SpriteRenderer coreSr = CreateBeamLayer(beamTf, "BeamInnerCore", 0.24f, 101, outerSr.sprite, new Color(1f, 0.93f, 0.78f, 1f));

                    midGlowRenderers.Add(midSr);
                    coreRenderers.Add(coreSr);
                    midGlowTransforms.Add(midSr.transform);
                    coreTransforms.Add(coreSr.transform);
                }
            }
        }

        // 루트(Scale 1,1,1) 기준 파티클 생성 — 비균등 스케일 왜곡 방지
        warningChargePs = CreateWarningChargeParticles();
        warningAxisPs.Add(CreateWarningAxisParticles("WarningParticles_H", Quaternion.identity));
        warningAxisPs.Add(CreateWarningAxisParticles("WarningParticles_V", Quaternion.Euler(0f, 0f, 90f)));

        coreVortexPs = CreateCoreVortexParticles();
        beamEmberPs.Add(CreateBeamEmberParticles("BeamEmbers_H", Quaternion.identity));
        beamEmberPs.Add(CreateBeamEmberParticles("BeamEmbers_V", Quaternion.Euler(0f, 0f, 90f)));
    }

    private SpriteRenderer CreateBeamLayer(Transform parent, string layerName, float yScale, int sortingOrder, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(layerName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(1f, yScale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        if (sharedUnlitMat != null)
        {
            sr.sharedMaterial = sharedUnlitMat;
        }
        return sr;
    }

    private static ParticleSystem CreateStoppedParticleSystem(string objName, Transform parent, Quaternion localRot, out GameObject go)
    {
        go = new GameObject(objName);
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = localRot;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 1f;
        return ps;
    }

    private ParticleSystem CreateWarningChargeParticles()
    {
        ParticleSystem ps = CreateStoppedParticleSystem("WarningChargeParticles", transform, Quaternion.identity, out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-7.5f, -4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.2f, 0.18f, 1f),
            new Color(1f, 0.85f, 0.45f, 1f)
        );
        main.maxParticles = 140;

        var emission = ps.emission;
        emission.rateOverTime = 85f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 3.4f;
        shape.radiusThickness = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildFadeGradient(
            new Color(1f, 0.22f, 0.18f, 0f),
            new Color(1f, 0.55f, 0.22f, 1f),
            new Color(1f, 0.96f, 0.78f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

        ConfigureParticleRenderer(ps, sharedDiamondParticleMat, 102);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateWarningAxisParticles(string objName, Quaternion localRot)
    {
        ParticleSystem ps = CreateStoppedParticleSystem(objName, transform, localRot, out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.18f, 0.18f, 0.95f),
            new Color(1f, 0.7f, 0.3f, 0.95f)
        );
        main.maxParticles = 140;

        var emission = ps.emission;
        emission.rateOverTime = 90f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(particleBeamSpan, Mathf.Max(0.8f, laserThickness * 0.9f), 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildFadeGradient(
            new Color(1f, 0.2f, 0.18f, 0f),
            new Color(1f, 0.45f, 0.2f, 0.95f),
            new Color(1f, 0.92f, 0.65f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ConfigureParticleRenderer(ps, sharedSquareParticleMat, 101);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateCoreVortexParticles()
    {
        ParticleSystem ps = CreateStoppedParticleSystem("CoreVortexParticles", transform, Quaternion.identity, out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 6.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.7f, 1f),
            new Color(1f, 0.28f, 0.16f, 1f)
        );
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.rateOverTime = 50f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.55f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildFadeGradient(
            new Color(1f, 0.96f, 0.8f, 1f),
            new Color(1f, 0.42f, 0.16f, 0.95f),
            new Color(0.75f, 0.06f, 0.12f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ConfigureParticleRenderer(ps, sharedDiamondParticleMat, 103);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateBeamEmberParticles(string objName, Quaternion localRot)
    {
        ParticleSystem ps = CreateStoppedParticleSystem(objName, transform, localRot, out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
        main.gravityModifier = -0.22f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.62f, 1f),
            new Color(1f, 0.25f, 0.14f, 1f)
        );
        main.maxParticles = 180;

        var emission = ps.emission;
        emission.rateOverTime = 70f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(particleBeamSpan, Mathf.Max(0.4f, laserThickness * 0.55f), 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildFadeGradient(
            new Color(1f, 0.95f, 0.78f, 1f),
            new Color(1f, 0.38f, 0.14f, 0.95f),
            new Color(0.65f, 0.05f, 0.1f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        ConfigureParticleRenderer(ps, sharedSquareParticleMat, 102);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private void ConfigureParticleRenderer(ParticleSystem ps, Material mat, int sortingOrder)
    {
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        if (pr == null) return;

        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sortingOrder = sortingOrder;
        if (mat != null)
        {
            pr.sharedMaterial = mat;
        }
    }

    private static Gradient BuildFadeGradient(Color start, Color mid, Color end)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(mid, 0.45f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a, 0.45f),
                new GradientAlphaKey(end.a, 1f)
            }
        );
        return grad;
    }

    private void StopAndClearAllParticles()
    {
        for (int i = 0; i < allParticles.Count; i++)
        {
            if (allParticles[i] != null)
            {
                allParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void PlayWarningParticles(bool play)
    {
        if (warningChargePs != null)
        {
            if (play) warningChargePs.Play();
            else warningChargePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        for (int i = 0; i < warningAxisPs.Count; i++)
        {
            if (warningAxisPs[i] == null) continue;
            if (play) warningAxisPs[i].Play();
            else warningAxisPs[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void PlayBeamParticles(bool play, bool burstOnStart = false)
    {
        if (coreVortexPs != null)
        {
            if (play)
            {
                coreVortexPs.Play();
                if (burstOnStart) coreVortexPs.Emit(28);
            }
            else
            {
                coreVortexPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        for (int i = 0; i < beamEmberPs.Count; i++)
        {
            if (beamEmberPs[i] == null) continue;
            if (play)
            {
                beamEmberPs[i].Play();
                if (burstOnStart) beamEmberPs[i].Emit(35);
            }
            else
            {
                beamEmberPs[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
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

        StopAndClearAllParticles();
        SetWarning(true);
        SetLaser(false);

        UpdateWarningVisuals(0f);
        SetLaserAlpha(1f);

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
        PlayWarningParticles(true);

        float warnTimer = 0f;
        bool lockOnBurstTriggered = false;
        while (warnTimer < warningTime)
        {
            warnTimer += Time.deltaTime;
            float progress = warningTime > 0f ? Mathf.Clamp01(warnTimer / warningTime) : 1f;
            UpdateWarningVisuals(progress);

            if (!lockOnBurstTriggered && progress >= 0.82f)
            {
                lockOnBurstTriggered = true;
                if (warningChargePs != null)
                {
                    warningChargePs.Emit(24);
                }
            }

            yield return null;
        }

        SetWarning(false);
        PlayWarningParticles(false);

        // ---------------- 레이저 등장 ----------------
        SetLaser(true);
        SetLaserAlpha(1f);
        SetBeamThickness(0f);
        PlayBeamParticles(true, burstOnStart: true);

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
            UpdateCorePulse(Time.time);

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
            UpdateCorePulse(Time.time);
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
                UpdateCorePulse(Time.time);
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
                UpdateCorePulse(Time.time);
                yield return null;
            }
            transform.rotation = reverseTargetRot;
        }

        // ---------------- 서서히 사라짐 ----------------
        PlayBeamParticles(false);

        float fadeTimer = 0f;
        float fadeDuration = 0.5f;

        while (fadeTimer < fadeDuration)
        {
            fadeTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(fadeTimer / fadeDuration);
            float alpha = Mathf.Lerp(1f, 0f, progress);

            SetLaserAlpha(alpha);
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

    #region Rendering Helpers

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

    private void UpdateCorePulse(float time)
    {
        float midPulse = 0.58f + 0.06f * Mathf.Sin(time * 28f);
        float corePulse = 0.24f + 0.045f * Mathf.Sin(time * 42f);

        for (int i = 0; i < midGlowTransforms.Count; i++)
        {
            if (midGlowTransforms[i] != null)
            {
                midGlowTransforms[i].localScale = new Vector3(1f, midPulse, 1f);
            }
        }

        for (int i = 0; i < coreTransforms.Count; i++)
        {
            if (coreTransforms[i] != null)
            {
                coreTransforms[i].localScale = new Vector3(1f, corePulse, 1f);
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

    private void UpdateWarningVisuals(float progress)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(progress * Mathf.PI * 6f);
        float fillAlpha = Mathf.Lerp(0.12f, 0.36f, progress) + pulse * 0.08f;
        SetWarningAlpha(Mathf.Clamp01(fillAlpha));

        // 경계 레일은 점차 선명해지며 직전에 밝게 점멸
        float edgeAlpha = Mathf.Lerp(0.45f, 0.95f, progress);
        Color edgeColor = Color.Lerp(
            new Color(1f, 0.22f, 0.18f, edgeAlpha),
            new Color(1f, 0.78f, 0.42f, edgeAlpha),
            Mathf.Clamp01((progress - 0.7f) / 0.3f)
        );
        for (int i = 0; i < warningEdgeRenderers.Count; i++)
        {
            if (warningEdgeRenderers[i] != null)
            {
                warningEdgeRenderers[i].color = edgeColor;
            }
        }

        // 중앙 수렴 가이드는 경계에서 중심으로 조여들며 발사 타이밍 예고
        float guideScaleY = Mathf.Lerp(1f, 0.06f, progress * progress);
        for (int i = 0; i < warningGuideTransforms.Count; i++)
        {
            if (warningGuideTransforms[i] != null)
            {
                warningGuideTransforms[i].localScale = new Vector3(1f, guideScaleY, 1f);
            }
        }

        float guideAlpha = Mathf.Lerp(0.25f, 0.95f, progress);
        Color guideColor = Color.Lerp(
            new Color(1f, 0.35f, 0.18f, guideAlpha),
            new Color(1f, 0.95f, 0.75f, guideAlpha),
            progress
        );
        for (int i = 0; i < warningGuideRenderers.Count; i++)
        {
            if (warningGuideRenderers[i] != null)
            {
                warningGuideRenderers[i].color = guideColor;
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
        ApplyAlphaToRenderers(laserRenderers, 0.8f * alpha);
        ApplyAlphaToRenderers(midGlowRenderers, 0.92f * alpha);
        ApplyAlphaToRenderers(coreRenderers, alpha);
    }

    private static void ApplyAlphaToRenderers(IReadOnlyList<SpriteRenderer> renderers, float alpha)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }
        }
    }

    #endregion
}