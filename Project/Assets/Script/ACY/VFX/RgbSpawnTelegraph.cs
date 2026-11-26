using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RGB 보스의 회오리(Hurricane) 및 얼음 망치(IceHammer) 생성 직전,
/// 속성 색상에 맞는 픽셀 파티클들이 실제 공격 형태(회오리 깔때기 / 대형 망치 실루엣)로
/// 뭉쳐지는 경고 이펙트를 재생합니다.
/// </summary>
public class RgbSpawnTelegraph : MonoBehaviour
{
    public enum TelegraphShape
    {
        Hurricane,
        IceHammer
    }

    private struct GatherParticleData
    {
        public Vector2 startOffset;
        public Vector2 targetOffset;
        public float phaseOffset;
        public float spinSpeed;
        public float swirlRadius;
        public float targetRadius;
        public float startY;
        public float targetY;
        public float baseSize;
        public float staggerDelay;
        public bool useCoreColor;
        public bool isOutline;
    }

    private TelegraphShape shapeType;
    private EnemyStatus.EnemyElement element;
    private float duration;
    private float elapsed;
    private bool isCompleted;
    private bool isCancelled;

    private Color coreColor;
    private Color midColor;
    private Color deepColor;

    private ParticleSystem corePixelPS;
    private ParticleSystem diamondOutlinePS;
    private ParticleSystem materializeBurstPS;
    private ParticleSystem materializeRingPS;

    private ParticleSystem.Particle[] coreBuffer;
    private ParticleSystem.Particle[] diamondBuffer;
    private GatherParticleData[] coreData;
    private GatherParticleData[] diamondData;

    private Vector3 burstLocalCenter = Vector3.zero;
    private Action<RgbSpawnTelegraph> onFinished;

    private static Texture2D sharedPixelTex;
    private static Texture2D sharedDiamondTex;
    private static Texture2D sharedRingTex;
    private static Material sharedPixelMat;
    private static Material sharedDiamondMat;
    private static Material sharedRingMat;

    public static RgbSpawnTelegraph SpawnHurricaneTelegraph(
        Vector3 worldPos,
        EnemyStatus.EnemyElement element,
        float duration,
        Action<RgbSpawnTelegraph> onFinished = null)
    {
        GameObject go = new GameObject("RgbTelegraph_Hurricane");
        go.transform.position = worldPos;
        RgbSpawnTelegraph comp = go.AddComponent<RgbSpawnTelegraph>();
        comp.Initialize(TelegraphShape.Hurricane, element, duration, onFinished);
        return comp;
    }

    public static RgbSpawnTelegraph SpawnIceHammerTelegraph(
        Vector3 worldPos,
        float duration,
        Action<RgbSpawnTelegraph> onFinished = null)
    {
        GameObject go = new GameObject("RgbTelegraph_IceHammer");
        go.transform.position = worldPos;
        RgbSpawnTelegraph comp = go.AddComponent<RgbSpawnTelegraph>();
        comp.Initialize(TelegraphShape.IceHammer, EnemyStatus.EnemyElement.Blue, duration, onFinished);
        return comp;
    }

    public void CancelAndDestroy()
    {
        if (isCancelled) return;
        isCancelled = true;
        onFinished?.Invoke(this);
        Destroy(gameObject);
    }

    private void Initialize(
        TelegraphShape shape,
        EnemyStatus.EnemyElement targetElement,
        float warningDuration,
        Action<RgbSpawnTelegraph> finishedCallback)
    {
        shapeType = shape;
        element = targetElement;
        duration = Mathf.Max(0.15f, warningDuration);
        elapsed = 0f;
        onFinished = finishedCallback;

        ResolvePalette(targetElement);
        EnsureSharedAssets();
        BuildParticleSystems();
        PopulateGatherData();
        UpdateSimulation(0f);
    }

    private void ResolvePalette(EnemyStatus.EnemyElement targetElement)
    {
        switch (targetElement)
        {
            case EnemyStatus.EnemyElement.Red:
                coreColor = new Color(1f, 0.90f, 0.80f, 1f);
                midColor = new Color(1f, 0.24f, 0.22f, 1f);
                deepColor = new Color(0.68f, 0.06f, 0.14f, 1f);
                break;

            case EnemyStatus.EnemyElement.Green:
                coreColor = new Color(0.86f, 1f, 0.88f, 1f);
                midColor = new Color(0.24f, 0.95f, 0.42f, 1f);
                deepColor = new Color(0.06f, 0.54f, 0.24f, 1f);
                break;

            case EnemyStatus.EnemyElement.Blue:
            default:
                coreColor = new Color(0.92f, 0.99f, 1f, 1f);
                midColor = new Color(0.28f, 0.82f, 1f, 1f);
                deepColor = new Color(0.10f, 0.38f, 0.94f, 1f);
                break;
        }
    }

    private void BuildParticleSystems()
    {
        corePixelPS = CreateManagedSystem("TelegraphCorePixels", sharedPixelMat, 102);
        diamondOutlinePS = CreateManagedSystem("TelegraphDiamondOutline", sharedDiamondMat, 103);

        materializeBurstPS = CreateBurstSystem("TelegraphMaterializeBurst", sharedDiamondMat, 104);
        materializeRingPS = CreateRingSystem("TelegraphMaterializeRing", sharedRingMat, 101);
    }

    private void PopulateGatherData()
    {
        if (shapeType == TelegraphShape.Hurricane)
        {
            burstLocalCenter = Vector3.zero;
            BuildHurricaneGatherData(pixelCount: 64, diamondCount: 52);
        }
        else
        {
            // IceHammer.prefab 첫 프레임(sprite-sheet_0: 256x128 @ 16 PPU, pivot 0.5,0.5)의
            // 실제 얼음 망치 헤드(-5.25, +2.15) 위치에 맞춰 응집 및 폭발 포커스 정렬
            burstLocalCenter = new Vector3(-5.15f, 1.95f, 0f);
            BuildIceHammerGatherData(pixelCount: 72, diamondCount: 74);
        }
    }

    private void BuildHurricaneGatherData(int pixelCount, int diamondCount)
    {
        coreData = new GatherParticleData[pixelCount];
        coreBuffer = new ParticleSystem.Particle[pixelCount];
        diamondData = new GatherParticleData[diamondCount];
        diamondBuffer = new ParticleSystem.Particle[diamondCount];

        for (int i = 0; i < pixelCount; i++)
        {
            coreData[i] = CreateHurricaneParticle(i, pixelCount, isDiamondLayer: false);
        }
        for (int i = 0; i < diamondCount; i++)
        {
            diamondData[i] = CreateHurricaneParticle(i, diamondCount, isDiamondLayer: true);
        }
    }

    private GatherParticleData CreateHurricaneParticle(int index, int total, bool isDiamondLayer)
    {
        float heightNorm = (index + UnityEngine.Random.Range(0.1f, 0.9f)) / Mathf.Max(1, total);
        heightNorm = Mathf.Clamp01(heightNorm);

        float targetY = Mathf.Lerp(-2.65f, 3.05f, heightNorm);
        float funnelRadius = EvaluateHurricaneRadius(heightNorm);
        if (!isDiamondLayer)
        {
            funnelRadius *= UnityEngine.Random.Range(0.35f, 0.95f);
        }
        else
        {
            funnelRadius *= UnityEngine.Random.Range(0.85f, 1.12f);
        }

        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float scatterExtra = UnityEngine.Random.Range(2.4f, 4.9f);
        return new GatherParticleData
        {
            phaseOffset = angle,
            spinSpeed = UnityEngine.Random.Range(6.5f, 10.5f) * (1f + (1f - heightNorm) * 0.45f),
            swirlRadius = funnelRadius + scatterExtra,
            targetRadius = funnelRadius,
            startY = targetY + UnityEngine.Random.Range(-1.2f, 1.2f),
            targetY = targetY,
            baseSize = isDiamondLayer ? UnityEngine.Random.Range(0.24f, 0.42f) : UnityEngine.Random.Range(0.18f, 0.34f),
            staggerDelay = UnityEngine.Random.Range(0f, 0.14f),
            useCoreColor = isDiamondLayer || UnityEngine.Random.value < 0.35f,
            isOutline = isDiamondLayer
        };
    }

    private static float EvaluateHurricaneRadius(float heightNorm)
    {
        // WaterHurricane.prefab 콜라이더 형태: 넓은 상단 깔때기 + 잘록한 허리 + 바닥 소용돌이 받침
        if (heightNorm > 0.56f)
        {
            float topT = (heightNorm - 0.56f) / 0.44f;
            return Mathf.Lerp(0.65f, 2.35f, Mathf.Pow(topT, 1.25f));
        }
        if (heightNorm >= 0.20f)
        {
            float waistT = (heightNorm - 0.20f) / 0.36f;
            return Mathf.Lerp(0.85f, 0.58f, Mathf.Sin(waistT * Mathf.PI));
        }

        float baseT = 1f - (heightNorm / 0.20f);
        return Mathf.Lerp(0.85f, 1.65f, baseT);
    }

    private void BuildIceHammerGatherData(int pixelCount, int diamondCount)
    {
        coreData = new GatherParticleData[pixelCount];
        coreBuffer = new ParticleSystem.Particle[pixelCount];
        diamondData = new GatherParticleData[diamondCount];
        diamondBuffer = new ParticleSystem.Particle[diamondCount];

        for (int i = 0; i < pixelCount; i++)
        {
            coreData[i] = CreateHammerParticle(outlineBias: 0.25f, isDiamondLayer: false);
        }
        for (int i = 0; i < diamondCount; i++)
        {
            diamondData[i] = CreateHammerParticle(outlineBias: 0.85f, isDiamondLayer: true);
        }
    }

    private GatherParticleData CreateHammerParticle(float outlineBias, bool isDiamondLayer)
    {
        bool isOutline = UnityEngine.Random.value < outlineBias;
        Vector2 target = SampleIceHammerPoint(isOutline);

        Vector2 hammerAnchor = new Vector2(-4.85f, 0.45f);
        Vector2 radialDir = target - hammerAnchor;
        if (radialDir.sqrMagnitude < 0.001f)
        {
            radialDir = UnityEngine.Random.insideUnitCircle.normalized;
        }
        radialDir.Normalize();
        Vector2 tangent = new Vector2(-radialDir.y, radialDir.x);

        float outerDist = UnityEngine.Random.Range(2.2f, 4.8f);
        float tangentShift = UnityEngine.Random.Range(-1.6f, 1.6f);
        Vector2 startPos = target + radialDir * outerDist + tangent * tangentShift;

        return new GatherParticleData
        {
            startOffset = startPos,
            targetOffset = target,
            phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            spinSpeed = UnityEngine.Random.Range(-1f, 1f) >= 0f ? 1f : -1f,
            swirlRadius = UnityEngine.Random.Range(0.35f, 1.1f),
            baseSize = isDiamondLayer ? UnityEngine.Random.Range(0.20f, 0.36f) : UnityEngine.Random.Range(0.16f, 0.30f),
            staggerDelay = UnityEngine.Random.Range(0f, 0.16f),
            useCoreColor = isOutline || UnityEngine.Random.value < 0.38f,
            isOutline = isOutline
        };
    }

    private static Vector2 SampleIceHammerPoint(bool onOutline)
    {
        // sprite-sheet_0(256x128 @ 16 PPU, 중심 피벗) 기준 실제 형태:
        // 1) 좌상단 가시형 얼음 결정 헤드: 중심 (-5.25, +2.10), 반경 ~1.55 (비대칭 뾰족 스파이크)
        // 2) 대각선 자루: (-5.10, +1.25) -> (-4.12, -2.35)
        // 3) 하단 4각 별 폼멜: (-4.22, -1.85)
        float roll = UnityEngine.Random.value;

        if (roll < 0.72f)
        {
            Vector2 headCenter = new Vector2(-5.25f, 2.10f);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            // 위쪽/좌우로 뾰족하게 뻗는 10개 얼음 가시 실루엣
            float spikeWave = Mathf.Abs(Mathf.Cos(angle * 5f + 0.35f));
            float outerR = Mathf.Lerp(0.95f, 1.68f, Mathf.Pow(spikeWave, 1.35f));
            float r = onOutline
                ? outerR * UnityEngine.Random.Range(0.86f, 1.04f)
                : outerR * Mathf.Sqrt(UnityEngine.Random.Range(0.04f, 0.78f));

            // 뾰족한 결정 헤드가 약간 위아래로 눌리고 -16도 기울어진 비율 반영
            Vector2 local = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.84f);
            float tiltRad = 16f * Mathf.Deg2Rad;
            float cosT = Mathf.Cos(tiltRad);
            float sinT = Mathf.Sin(tiltRad);
            Vector2 tilted = new Vector2(
                local.x * cosT - local.y * sinT,
                local.x * sinT + local.y * cosT);

            return headCenter + tilted;
        }

        if (roll < 0.91f)
        {
            Vector2 shaftTop = new Vector2(-5.08f, 1.20f);
            Vector2 shaftBottom = new Vector2(-4.12f, -2.35f);
            float t = UnityEngine.Random.value;
            Vector2 dir = (shaftBottom - shaftTop).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float halfW = onOutline ? 0.11f : 0.06f;
            float offset = onOutline
                ? (UnityEngine.Random.value < 0.5f ? -halfW : halfW)
                : UnityEngine.Random.Range(-halfW, halfW);

            return Vector2.Lerp(shaftTop, shaftBottom, t) + perp * offset;
        }

        // 하단 4각 십자/다이아 별 폼멜
        Vector2 pommelCenter = new Vector2(-4.22f, -1.85f);
        bool horizontalArm = UnityEngine.Random.value < 0.5f;
        float arm = UnityEngine.Random.Range(-0.46f, 0.46f);
        float thickness = UnityEngine.Random.Range(-0.07f, 0.07f);
        return horizontalArm
            ? pommelCenter + new Vector2(arm, thickness)
            : pommelCenter + new Vector2(thickness, arm);
    }

    private void Update()
    {
        if (isCancelled) return;

        elapsed += Time.deltaTime;

        if (!isCompleted)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            UpdateSimulation(t);

            if (t >= 1f)
            {
                TriggerMaterializeBurst();
            }
        }
        else if (elapsed >= duration + 0.45f)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateSimulation(float globalT)
    {
        if (shapeType == TelegraphShape.Hurricane)
        {
            SimulateHurricaneLayer(corePixelPS, coreBuffer, coreData, globalT);
            SimulateHurricaneLayer(diamondOutlinePS, diamondBuffer, diamondData, globalT);
        }
        else
        {
            SimulateHammerLayer(corePixelPS, coreBuffer, coreData, globalT);
            SimulateHammerLayer(diamondOutlinePS, diamondBuffer, diamondData, globalT);
        }
    }

    private void SimulateHurricaneLayer(
        ParticleSystem ps,
        ParticleSystem.Particle[] buffer,
        GatherParticleData[] data,
        float globalT)
    {
        if (ps == null || data == null) return;

        for (int i = 0; i < data.Length; i++)
        {
            GatherParticleData d = data[i];
            float localT = Mathf.Clamp01((globalT - d.staggerDelay) / Mathf.Max(0.1f, 1f - d.staggerDelay));
            float pull = localT * localT * (3f - 2f * localT);
            float spinAngle = d.phaseOffset + (localT * localT * d.spinSpeed * 2.15f);

            float radius = Mathf.Lerp(d.swirlRadius, d.targetRadius * Mathf.Lerp(1.12f, 0.88f, localT), pull);
            float x = Mathf.Cos(spinAngle) * radius;
            float y = Mathf.Lerp(d.startY, d.targetY, pull) + Mathf.Sin(spinAngle) * radius * 0.16f;

            float alpha = ComputeAlpha(localT, d.isOutline ? 0.96f : 0.86f);
            float size = d.baseSize * Mathf.Lerp(0.65f, 1.15f, localT);
            Color c = ResolveParticleColor(localT, d.useCoreColor, alpha);

            buffer[i] = BuildParticle(new Vector3(x, y, 0f), size, spinAngle * Mathf.Rad2Deg, c);
        }

        ps.SetParticles(buffer, data.Length);
    }

    private void SimulateHammerLayer(
        ParticleSystem ps,
        ParticleSystem.Particle[] buffer,
        GatherParticleData[] data,
        float globalT)
    {
        if (ps == null || data == null) return;

        for (int i = 0; i < data.Length; i++)
        {
            GatherParticleData d = data[i];
            float localT = Mathf.Clamp01((globalT - d.staggerDelay) / Mathf.Max(0.1f, 1f - d.staggerDelay));
            float ease = 1f - Mathf.Pow(1f - localT, 2.3f);

            Vector2 basePos = Vector2.Lerp(d.startOffset, d.targetOffset, ease);

            // 모이는 동안 곡선 호를 그리며 응집되고, 마지막 구간엔 미세한 결정 진동
            Vector2 delta = d.targetOffset - d.startOffset;
            Vector2 perp = delta.sqrMagnitude > 0.001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            float arcWave = Mathf.Sin(localT * Mathf.PI) * d.swirlRadius * d.spinSpeed * (1f - ease);
            Vector2 pos = basePos + perp * arcWave;

            if (localT > 0.82f)
            {
                float jitterAmp = 0.06f * ((localT - 0.82f) / 0.18f);
                pos.x += Mathf.Sin(Time.time * 48f + d.phaseOffset) * jitterAmp;
                pos.y += Mathf.Cos(Time.time * 44f + d.phaseOffset) * jitterAmp;
            }

            float alpha = ComputeAlpha(localT, d.isOutline ? 0.98f : 0.84f);
            float size = d.baseSize * Mathf.Lerp(0.7f, 1.18f, localT);
            Color c = ResolveParticleColor(localT, d.useCoreColor, alpha);

            buffer[i] = BuildParticle(new Vector3(pos.x, pos.y, 0f), size, (d.phaseOffset + localT * 3f) * Mathf.Rad2Deg, c);
        }

        ps.SetParticles(buffer, data.Length);
    }

    private Color ResolveParticleColor(float localT, bool useCoreColor, float alpha)
    {
        Color c;
        if (localT < 0.72f)
        {
            Color targetMid = useCoreColor ? Color.Lerp(midColor, coreColor, 0.45f) : Color.Lerp(deepColor, midColor, 0.65f);
            c = Color.Lerp(deepColor, targetMid, localT / 0.72f);
        }
        else
        {
            float flashT = (localT - 0.72f) / 0.28f;
            c = Color.Lerp(midColor, coreColor, flashT * (useCoreColor ? 0.95f : 0.7f));
        }
        c.a = alpha;
        return c;
    }

    private static float ComputeAlpha(float localT, float peakAlpha)
    {
        if (localT < 0.16f)
        {
            return peakAlpha * (localT / 0.16f);
        }
        return peakAlpha;
    }

    private static ParticleSystem.Particle BuildParticle(Vector3 localPos, float size, float rotDeg, Color color)
    {
        ParticleSystem.Particle p = new ParticleSystem.Particle
        {
            position = localPos,
            velocity = Vector3.zero,
            startSize = size,
            startColor = (Color32)color,
            rotation = rotDeg,
            angularVelocity = 0f,
            startLifetime = 10f,
            remainingLifetime = 10f
        };
        return p;
    }

    private void TriggerMaterializeBurst()
    {
        isCompleted = true;

        if (corePixelPS != null)
        {
            corePixelPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (diamondOutlinePS != null)
        {
            diamondOutlinePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (materializeBurstPS != null)
        {
            materializeBurstPS.transform.localPosition = burstLocalCenter;
            materializeBurstPS.Play(false);
            materializeBurstPS.Emit(shapeType == TelegraphShape.IceHammer ? 24 : 20);
        }
        if (materializeRingPS != null)
        {
            materializeRingPS.transform.localPosition = burstLocalCenter;
            materializeRingPS.Play(false);
            materializeRingPS.Emit(1);
        }

        onFinished?.Invoke(this);
    }

    private ParticleSystem CreateManagedSystem(string childName, Material mat, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.SetActive(false);
        child.transform.SetParent(transform, false);

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 128;

        var em = ps.emission;
        em.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = sortingOrder;
        r.sharedMaterial = mat;

        child.SetActive(true);
        ps.Play(false);
        return ps;
    }

    private ParticleSystem CreateBurstSystem(string childName, Material mat, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.SetActive(false);
        child.transform.SetParent(transform, false);

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 6.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.44f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(coreColor, midColor);
        main.maxParticles = 40;

        var em = ps.emission;
        em.enabled = false;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = shapeType == TelegraphShape.IceHammer ? 1.6f : 1.0f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildFadeOutGradient(0.95f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.05f),
            new Keyframe(1f, 0f)
        ));

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = sortingOrder;
        r.sharedMaterial = mat;

        child.SetActive(true);
        return ps;
    }

    private ParticleSystem CreateRingSystem(string childName, Material mat, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.SetActive(false);
        child.transform.SetParent(transform, false);

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.28f;
        main.startSpeed = 0f;
        main.startSize = shapeType == TelegraphShape.IceHammer ? 4.4f : 3.4f;
        main.startColor = coreColor;
        main.maxParticles = 4;

        var em = ps.emission;
        em.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildFadeOutGradient(0.8f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(1f, 1.35f)
        ));

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = sortingOrder;
        r.sharedMaterial = mat;

        child.SetActive(true);
        return ps;
    }

    private ParticleSystem.MinMaxGradient BuildFadeOutGradient(float startAlpha)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(coreColor, 0f),
                new GradientColorKey(midColor, 0.45f),
                new GradientColorKey(deepColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startAlpha, 0f),
                new GradientAlphaKey(startAlpha * 0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static void EnsureSharedAssets()
    {
        if (sharedPixelTex == null)
        {
            const int size = 6;
            sharedPixelTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RgbTelegraph_PixelTex"
            };
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                    px[y * size + x] = new Color32(255, 255, 255, corner ? (byte)0 : (byte)255);
                }
            }
            sharedPixelTex.SetPixels32(px);
            sharedPixelTex.Apply(false, true);
        }

        if (sharedDiamondTex == null)
        {
            const int size = 10;
            sharedDiamondTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RgbTelegraph_DiamondTex"
            };
            Color32[] px = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    byte a = dist <= 2.2f ? (byte)255 : (dist <= 4.0f ? (byte)185 : (byte)0);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            sharedDiamondTex.SetPixels32(px);
            sharedDiamondTex.Apply(false, true);
        }

        if (sharedRingTex == null)
        {
            const int size = 24;
            sharedRingTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RgbTelegraph_RingTex"
            };
            Color32[] px = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (r >= 7.8f && r <= 10.6f) ? (byte)245 : ((r >= 6.8f && r <= 11.4f) ? (byte)110 : (byte)0);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            sharedRingTex.SetPixels32(px);
            sharedRingTex.Apply(false, true);
        }

        if (sharedPixelMat == null) sharedPixelMat = CreateMat("RgbTelegraph_PixelMat", sharedPixelTex);
        if (sharedDiamondMat == null) sharedDiamondMat = CreateMat("RgbTelegraph_DiamondMat", sharedDiamondTex);
        if (sharedRingMat == null) sharedRingMat = CreateMat("RgbTelegraph_RingMat", sharedRingTex);
    }

    private static Material CreateMat(string matName, Texture2D tex)
    {
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit");
        Material mat = new Material(shader) { name = matName };
        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", tex);
        }
        mat.renderQueue = 3000;
        return mat;
    }
}
