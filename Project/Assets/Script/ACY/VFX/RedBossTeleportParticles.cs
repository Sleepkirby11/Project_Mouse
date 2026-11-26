using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RedBoss 및 분신이 순간이동·소환·소멸할 때 몸체 중심을 기준으로
/// 암흑-진홍빛(네크로맨서 테마) 픽셀 아트 텔레포트 파티클 연출을 풀링 재생합니다.
/// </summary>
public class RedBossTeleportParticles : MonoBehaviour
{
    private const float GatherDuration = 0.20f;
    private const float DisappearCleanupDelay = 0.56f;
    private const float AppearCleanupDelay = 0.52f;
    private const int DefaultBaseOrder = 250;

    private struct GatherParticleData
    {
        public Vector2 startOffset;
        public Vector2 targetOffset;
        public float spinDir;
        public float swirlStrength;
        public float baseSize;
        public float staggerDelay;
        public bool useBrightAccent;
    }

    private sealed class PoolHost : MonoBehaviour
    {
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ReturnAllActive();
        }
    }

    private static PoolHost host;
    private static readonly Queue<RedBossTeleportParticles> pool = new Queue<RedBossTeleportParticles>();
    private static readonly List<RedBossTeleportParticles> activeInstances = new List<RedBossTeleportParticles>();

    private static Texture2D sharedDiamondTex;
    private static Texture2D sharedSquareTex;
    private static Texture2D sharedPuffTex;
    private static Texture2D sharedRingTex;
    private static Texture2D sharedCrossStarTex;

    private static Material sharedDiamondMat;
    private static Material sharedSquareMat;
    private static Material sharedPuffMat;
    private static Material sharedRingMat;
    private static Material sharedCrossStarMat;

    // Necromancer_creativekind-Sheet 팔레트 & RedBoss 화염/진홍 계열 통일 색상
    private static readonly Color CoreEmberWhite = new Color(1.00f, 0.92f, 0.84f, 1.00f); // #FFECD6
    private static readonly Color CrimsonBright = new Color(1.00f, 0.15f, 0.18f, 1.00f);  // #FF262E
    private static readonly Color BloodRed = new Color(0.75f, 0.05f, 0.07f, 1.00f);       // #C00D12
    private static readonly Color DeepCrimson = new Color(0.45f, 0.04f, 0.07f, 1.00f);    // #730A12
    private static readonly Color ShadowViolet = new Color(0.10f, 0.07f, 0.16f, 0.92f);   // #1A1229
    private static readonly Color ShadowAbyss = new Color(0.05f, 0.04f, 0.09f, 0.92f);    // #0D0A17

    private ParticleSystem gatherVeilPS;
    private ParticleSystem gatherRunePS;
    private ParticleSystem implodeRingPS;
    private ParticleSystem shadowBurstPS;
    private ParticleSystem sparkBurstPS;
    private ParticleSystem emberRisePS;
    private ParticleSystem appearRingPS;
    private ParticleSystem flashStarPS;

    private ParticleSystem.Particle[] gatherVeilBuffer;
    private ParticleSystem.Particle[] gatherRuneBuffer;
    private GatherParticleData[] gatherVeilData;
    private GatherParticleData[] gatherRuneData;

    private bool built;
    private bool isPlaying;
    private bool isDisappearing;
    private bool collapseCompleted;
    private float elapsed;
    private float totalLifetime;
    private Vector2 bodyExtent = new Vector2(0.85f, 1.35f);

    public static void PlayDisappear(Component owner)
    {
        if (owner == null) return;

        ResolveOwnerContext(owner, out Vector3 center, out Vector2 extent, out int layerId, out int baseOrder);
        PlayInternal(isAppear: false, center, extent, layerId, baseOrder);
    }

    public static void PlayAppear(Component owner)
    {
        if (owner == null) return;

        ResolveOwnerContext(owner, out Vector3 center, out Vector2 extent, out int layerId, out int baseOrder);
        PlayInternal(isAppear: true, center, extent, layerId, baseOrder);
    }

    public static void PlayDisappearAt(Vector3 worldCenter, int sortingLayerId = 0, int baseSortingOrder = DefaultBaseOrder)
    {
        PlayInternal(isAppear: false, worldCenter, new Vector2(0.85f, 1.35f), sortingLayerId, baseSortingOrder);
    }

    public static void PlayAppearAt(Vector3 worldCenter, int sortingLayerId = 0, int baseSortingOrder = DefaultBaseOrder)
    {
        PlayInternal(isAppear: true, worldCenter, new Vector2(0.85f, 1.35f), sortingLayerId, baseSortingOrder);
    }

    public static Vector3 ResolveBodyCenter(Component owner)
    {
        ResolveOwnerContext(owner, out Vector3 center, out _, out _, out _);
        return center;
    }

    private static void ResolveOwnerContext(
        Component owner,
        out Vector3 center,
        out Vector2 extent,
        out int sortingLayerId,
        out int baseSortingOrder)
    {
        extent = new Vector2(0.85f, 1.35f);
        sortingLayerId = 0;
        baseSortingOrder = DefaultBaseOrder;

        if (owner == null)
        {
            center = Vector3.zero;
            return;
        }

        SpriteRenderer sr = owner.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sortingLayerId = sr.sortingLayerID;
            baseSortingOrder = sr.sortingOrder;
        }

        Collider2D col = owner.GetComponent<Collider2D>();
        if (col != null)
        {
            // Physics2D autoSyncTransforms=false 환경에서 transform.position 변경 직후 col.bounds는 이전 프레임 좌표를 반환하므로
            // Transform.TransformPoint(col.offset)로 즉시 갱신된 몸체 중심을 계산합니다.
            Transform colTf = col.transform;
            Vector3 lossy = colTf.lossyScale;
            float sx = Mathf.Abs(lossy.x);
            float sy = Mathf.Abs(lossy.y);

            center = colTf.TransformPoint(col.offset);

            if (col is CapsuleCollider2D cap)
            {
                extent = new Vector2(
                    Mathf.Clamp(cap.size.x * 0.5f * sx, 0.55f, 1.5f),
                    Mathf.Clamp(cap.size.y * 0.5f * sy, 0.85f, 2.0f));
            }
            else if (col is BoxCollider2D box)
            {
                extent = new Vector2(
                    Mathf.Clamp(box.size.x * 0.5f * sx, 0.55f, 1.5f),
                    Mathf.Clamp(box.size.y * 0.5f * sy, 0.85f, 2.0f));
            }
            else if (col is CircleCollider2D circle)
            {
                float r = Mathf.Clamp(circle.radius * Mathf.Max(sx, sy), 0.55f, 1.5f);
                extent = new Vector2(r, r);
            }
            return;
        }

        Transform pivot = owner.transform.Find("PivotPoint");
        if (pivot != null)
        {
            center = pivot.position;
            return;
        }

        // RedBoss/RedBossClone 스프라이트 셀의 실제 네크로맨서 몸체 중심 오프셋(-1.75) 보정
        center = owner.transform.TransformPoint(new Vector3(0f, -1.75f, 0f));
    }

    private static void PlayInternal(
        bool isAppear,
        Vector3 worldCenter,
        Vector2 extent,
        int sortingLayerId,
        int baseSortingOrder)
    {
        EnsureHostAndAssets();
        RedBossTeleportParticles inst = AcquireInstance();
        if (inst == null) return;

        if (isAppear)
        {
            inst.BeginAppear(worldCenter, extent, sortingLayerId, baseSortingOrder);
        }
        else
        {
            inst.BeginDisappear(worldCenter, extent, sortingLayerId, baseSortingOrder);
        }
    }

    private void BeginDisappear(Vector3 center, Vector2 extent, int layerId, int baseOrder)
    {
        EnsureBuilt();
        StopAllSystems();

        bodyExtent = extent;
        transform.position = center;
        ApplySortingAll(layerId, baseOrder);

        isPlaying = true;
        isDisappearing = true;
        collapseCompleted = false;
        elapsed = 0f;
        totalLifetime = DisappearCleanupDelay;

        if (!activeInstances.Contains(this))
        {
            activeInstances.Add(this);
        }

        PrepareGatherData();
        UpdateGatherSimulation(0f);

        if (implodeRingPS != null)
        {
            implodeRingPS.Play(false);
            implodeRingPS.Emit(1);
        }
    }

    private void BeginAppear(Vector3 center, Vector2 extent, int layerId, int baseOrder)
    {
        EnsureBuilt();
        StopAllSystems();

        bodyExtent = extent;
        transform.position = center;
        ApplySortingAll(layerId, baseOrder);

        isPlaying = true;
        isDisappearing = false;
        collapseCompleted = true;
        elapsed = 0f;
        totalLifetime = AppearCleanupDelay;

        if (!activeInstances.Contains(this))
        {
            activeInstances.Add(this);
        }

        EmitAppearBurst();
    }

    private void Update()
    {
        if (!isPlaying) return;

        elapsed += Time.deltaTime;

        if (isDisappearing && !collapseCompleted)
        {
            float t = Mathf.Clamp01(elapsed / GatherDuration);
            UpdateGatherSimulation(t);

            if (t >= 1f)
            {
                TriggerCollapseSnap();
            }
        }

        if (elapsed >= totalLifetime)
        {
            ForceReturnNow();
        }
    }

    private void TriggerCollapseSnap()
    {
        collapseCompleted = true;
        ClearManagedSystems();

        if (flashStarPS != null)
        {
            var ep = new ParticleSystem.EmitParams
            {
                startSize = 1.45f,
                startLifetime = 0.16f,
                startColor = CoreEmberWhite
            };
            flashStarPS.Play(false);
            flashStarPS.Emit(ep, 1);
        }

        if (shadowBurstPS != null)
        {
            shadowBurstPS.Play(false);
            shadowBurstPS.Emit(10);
        }

        if (emberRisePS != null)
        {
            emberRisePS.Play(false);
            emberRisePS.Emit(10);
        }
    }

    private void EmitAppearBurst()
    {
        if (flashStarPS != null)
        {
            flashStarPS.Play(false);
            flashStarPS.Emit(2);
        }

        if (appearRingPS != null)
        {
            appearRingPS.Play(false);
            var innerRing = new ParticleSystem.EmitParams
            {
                startSize = 1.75f,
                startLifetime = 0.22f,
                startColor = CrimsonBright
            };
            var outerRing = new ParticleSystem.EmitParams
            {
                startSize = 2.40f,
                startLifetime = 0.28f,
                startColor = BloodRed
            };
            appearRingPS.Emit(innerRing, 1);
            appearRingPS.Emit(outerRing, 1);
        }

        if (shadowBurstPS != null)
        {
            shadowBurstPS.Play(false);
            shadowBurstPS.Emit(16);
        }

        if (sparkBurstPS != null)
        {
            sparkBurstPS.Play(false);
            sparkBurstPS.Emit(20);
        }

        if (emberRisePS != null)
        {
            emberRisePS.Play(false);
            emberRisePS.Emit(14);
        }
    }

    private void PrepareGatherData()
    {
        const int veilCount = 18;
        const int runeCount = 16;

        if (gatherVeilData == null || gatherVeilData.Length != veilCount)
        {
            gatherVeilData = new GatherParticleData[veilCount];
            gatherVeilBuffer = new ParticleSystem.Particle[veilCount];
        }

        if (gatherRuneData == null || gatherRuneData.Length != runeCount)
        {
            gatherRuneData = new GatherParticleData[runeCount];
            gatherRuneBuffer = new ParticleSystem.Particle[runeCount];
        }

        for (int i = 0; i < veilCount; i++)
        {
            gatherVeilData[i] = CreateGatherEntry(isRune: false);
        }

        for (int i = 0; i < runeCount; i++)
        {
            gatherRuneData[i] = CreateGatherEntry(isRune: true);
        }

        ActivateManagedSystem(gatherVeilPS);
        ActivateManagedSystem(gatherRunePS);
    }

    private GatherParticleData CreateGatherEntry(bool isRune)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float rx = bodyExtent.x * Random.Range(1.05f, 1.95f);
        float ry = bodyExtent.y * Random.Range(0.95f, 1.55f);

        Vector2 start = new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        Vector2 target = new Vector2(
            Random.Range(-0.18f, 0.18f),
            Random.Range(-0.25f, 0.25f));

        return new GatherParticleData
        {
            startOffset = start,
            targetOffset = target,
            spinDir = Random.value < 0.5f ? 1f : -1f,
            swirlStrength = Random.Range(0.28f, 0.72f),
            baseSize = isRune ? Random.Range(0.18f, 0.34f) : Random.Range(0.32f, 0.52f),
            staggerDelay = Random.Range(0f, 0.18f),
            useBrightAccent = isRune && Random.value < 0.45f
        };
    }

    private void UpdateGatherSimulation(float globalT)
    {
        SimulateGatherLayer(gatherVeilPS, gatherVeilBuffer, gatherVeilData, globalT, isRune: false);
        SimulateGatherLayer(gatherRunePS, gatherRuneBuffer, gatherRuneData, globalT, isRune: true);
    }

    private static void SimulateGatherLayer(
        ParticleSystem ps,
        ParticleSystem.Particle[] buffer,
        GatherParticleData[] data,
        float globalT,
        bool isRune)
    {
        if (ps == null || buffer == null || data == null) return;

        for (int i = 0; i < data.Length; i++)
        {
            GatherParticleData d = data[i];
            float localT = Mathf.Clamp01((globalT - d.staggerDelay) / Mathf.Max(0.12f, 1f - d.staggerDelay));
            // 가속 흡입 커브 (끝으로 갈수록 빠르게 중심으로 빨려듦)
            float ease = localT * localT;

            Vector2 basePos = Vector2.Lerp(d.startOffset, d.targetOffset, ease);
            Vector2 delta = d.targetOffset - d.startOffset;
            Vector2 perp = delta.sqrMagnitude > 0.001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;

            float arc = Mathf.Sin(localT * Mathf.PI) * d.swirlStrength * d.spinDir * (1f - ease * 0.6f);
            Vector2 pos = basePos + perp * arc;

            Color c;
            if (isRune)
            {
                c = Color.Lerp(
                    d.useBrightAccent ? CrimsonBright : BloodRed,
                    CoreEmberWhite,
                    ease * 0.75f);
                c.a = Mathf.Clamp01(Mathf.Sin(localT * Mathf.PI * 0.85f) + 0.3f) * (1f - ease * 0.35f);
            }
            else
            {
                c = Color.Lerp(ShadowViolet, DeepCrimson, ease * 0.65f);
                c.a = Mathf.Lerp(0.85f, 0.15f, ease);
            }

            float sizeMult = Mathf.Lerp(1.1f, 0.35f, ease);
            buffer[i] = new ParticleSystem.Particle
            {
                position = new Vector3(pos.x, pos.y, 0f),
                velocity = Vector3.zero,
                remainingLifetime = 1f,
                startLifetime = 1f,
                startSize = d.baseSize * sizeMult,
                rotation = (localT * 180f * d.spinDir),
                startColor = (Color32)c
            };
        }

        ps.SetParticles(buffer, buffer.Length);
    }

    private void ClearManagedSystems()
    {
        if (gatherVeilPS != null)
        {
            gatherVeilPS.Clear();
            gatherVeilPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (gatherRunePS != null)
        {
            gatherRunePS.Clear();
            gatherRunePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ForceReturnNow()
    {
        isPlaying = false;
        isDisappearing = false;
        StopAllSystems();
        ReleaseInstance(this);
    }

    private void StopAllSystems()
    {
        ClearManagedSystems();
        StopPs(implodeRingPS);
        StopPs(shadowBurstPS);
        StopPs(sparkBurstPS);
        StopPs(emberRisePS);
        StopPs(appearRingPS);
        StopPs(flashStarPS);
    }

    private static void StopPs(ParticleSystem ps)
    {
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void ActivateManagedSystem(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play(false);
    }

    private void EnsureBuilt()
    {
        if (built) return;

        EnsureHostAndAssets();

        gatherVeilPS = CreateManagedSystem("Teleport_GatherVeil", sharedPuffMat, DefaultBaseOrder - 1);
        gatherRunePS = CreateManagedSystem("Teleport_GatherRunes", sharedDiamondMat, DefaultBaseOrder + 3);
        implodeRingPS = CreateImplodeRingSystem();
        shadowBurstPS = CreateShadowBurstSystem();
        appearRingPS = CreateAppearRingSystem();
        emberRisePS = CreateEmberRiseSystem();
        sparkBurstPS = CreateSparkBurstSystem();
        flashStarPS = CreateFlashStarSystem();

        built = true;
    }

    private ParticleSystem CreateManagedSystem(string childName, Material mat, int sortingOrder)
    {
        ParticleSystem ps = CreateBaseSystem(childName, mat, sortingOrder);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 32;
        return ps;
    }

    private ParticleSystem CreateImplodeRingSystem()
    {
        ParticleSystem ps = CreateBaseSystem("Teleport_ImplodeRing", sharedRingMat, DefaultBaseOrder + 1);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = GatherDuration + 0.02f;
        main.startSpeed = 0f;
        main.startSize = 2.45f;
        main.startColor = CrimsonBright;
        main.maxParticles = 4;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = 160f * Mathf.Deg2Rad;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(0.65f, 0.05f, 0.08f, 0.80f),
            CrimsonBright,
            CoreEmberWhite);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.0f),
            new Keyframe(0.70f, 0.42f),
            new Keyframe(1f, 0.05f)));

        return ps;
    }

    private ParticleSystem CreateAppearRingSystem()
    {
        ParticleSystem ps = CreateBaseSystem("Teleport_AppearRing", sharedRingMat, DefaultBaseOrder + 1);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = 2.15f;
        main.startColor = CrimsonBright;
        main.maxParticles = 4;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = -120f * Mathf.Deg2Rad;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            CoreEmberWhite,
            CrimsonBright,
            new Color(DeepCrimson.r, DeepCrimson.g, DeepCrimson.b, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.22f),
            new Keyframe(0.35f, 0.85f),
            new Keyframe(1f, 1.25f)));

        return ps;
    }

    private ParticleSystem CreateFlashStarSystem()
    {
        ParticleSystem ps = CreateBaseSystem("Teleport_FlashStar", sharedCrossStarMat, DefaultBaseOrder + 4);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.22f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.35f, 1.75f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(CoreEmberWhite, CrimsonBright);
        main.maxParticles = 6;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            CoreEmberWhite,
            new Color(CrimsonBright.r, CrimsonBright.g, CrimsonBright.b, 0.90f),
            new Color(BloodRed.r, BloodRed.g, BloodRed.b, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.50f),
            new Keyframe(0.20f, 1.15f),
            new Keyframe(1f, 0.05f)));

        return ps;
    }

    private ParticleSystem CreateShadowBurstSystem()
    {
        ParticleSystem ps = CreateBaseSystem("Teleport_ShadowBurst", sharedPuffMat, DefaultBaseOrder - 1);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.36f, 0.62f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(ShadowViolet, ShadowAbyss);
        main.maxParticles = 28;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.55f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(0.75f, 1.35f, 1f);

        ConfigureUpwardVelocity(ps, 0.4f, 1.4f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            ShadowViolet,
            new Color(DeepCrimson.r, DeepCrimson.g, DeepCrimson.b, 0.72f),
            new Color(ShadowAbyss.r, ShadowAbyss.g, ShadowAbyss.b, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.75f),
            new Keyframe(0.25f, 1.10f),
            new Keyframe(1f, 0.15f)));

        return ps;
    }

    private ParticleSystem CreateSparkBurstSystem()
    {
        ParticleSystem ps = CreateBaseSystem("Teleport_CrimsonSparks", sharedDiamondMat, DefaultBaseOrder + 3);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.4f, 7.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(CoreEmberWhite, CrimsonBright);
        main.maxParticles = 28;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.38f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(0.80f, 1.25f, 1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            CoreEmberWhite,
            CrimsonBright,
            new Color(DeepCrimson.r, DeepCrimson.g, DeepCrimson.b, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.0f),
            new Keyframe(0.3f, 1.08f),
            new Keyframe(1f, 0f)));

        return ps;
    }

    private ParticleSystem CreateEmberRiseSystem()
    {
        ParticleSystem ps = CreateBaseSystem("Teleport_EmberRise", sharedSquareMat, DefaultBaseOrder + 2);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.40f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
        main.startColor = new ParticleSystem.MinMaxGradient(CrimsonBright, BloodRed);
        main.maxParticles = 24;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.62f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(0.75f, 1.40f, 1f);

        ConfigureUpwardVelocity(ps, 1.8f, 3.4f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            CoreEmberWhite,
            CrimsonBright,
            new Color(DeepCrimson.r, DeepCrimson.g, DeepCrimson.b, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.9f),
            new Keyframe(1f, 0f)));

        return ps;
    }

    private static void ConfigureUpwardVelocity(ParticleSystem ps, float minY, float maxY)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // VelocityOverLifetime의 x, y, z 커브 모드를 모두 TwoConstants로 통일
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(minY, maxY);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    private ParticleSystem CreateBaseSystem(string childName, Material mat, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.SetActive(false);
        child.transform.SetParent(transform, false);

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = sortingOrder;
        if (mat != null)
        {
            r.sharedMaterial = mat;
        }

        child.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private void ApplySortingAll(int layerId, int baseOrder)
    {
        ApplySorting(gatherVeilPS, layerId, baseOrder - 1);
        ApplySorting(shadowBurstPS, layerId, baseOrder - 1);
        ApplySorting(implodeRingPS, layerId, baseOrder + 1);
        ApplySorting(appearRingPS, layerId, baseOrder + 1);
        ApplySorting(emberRisePS, layerId, baseOrder + 2);
        ApplySorting(gatherRunePS, layerId, baseOrder + 3);
        ApplySorting(sparkBurstPS, layerId, baseOrder + 3);
        ApplySorting(flashStarPS, layerId, baseOrder + 4);
    }

    private static void ApplySorting(ParticleSystem ps, int layerId, int order)
    {
        if (ps == null) return;
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.sortingLayerID = layerId;
        r.sortingOrder = order;
    }

    private static ParticleSystem.MinMaxGradient BuildGradient(Color start, Color mid, Color end)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(mid, 0.42f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a, 0.45f),
                new GradientAlphaKey(end.a, 1f)
            });
        return new ParticleSystem.MinMaxGradient(grad);
    }

    private static RedBossTeleportParticles AcquireInstance()
    {
        while (pool.Count > 0)
        {
            RedBossTeleportParticles pooled = pool.Dequeue();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        if (host == null) return null;

        GameObject go = new GameObject("RedBossTeleportVfxInstance");
        go.transform.SetParent(host.transform, false);
        RedBossTeleportParticles inst = go.AddComponent<RedBossTeleportParticles>();
        inst.EnsureBuilt();
        return inst;
    }

    private static void ReleaseInstance(RedBossTeleportParticles inst)
    {
        if (inst == null) return;

        activeInstances.Remove(inst);
        if (host != null && inst.transform.parent != host.transform)
        {
            inst.transform.SetParent(host.transform, false);
        }

        inst.gameObject.SetActive(false);
        if (!pool.Contains(inst))
        {
            pool.Enqueue(inst);
        }
    }

    private static void ReturnAllActive()
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            RedBossTeleportParticles inst = activeInstances[i];
            if (inst != null)
            {
                inst.ForceReturnNow();
            }
        }
        activeInstances.Clear();
    }

    private static void EnsureHostAndAssets()
    {
        if (host == null)
        {
            GameObject hostGo = new GameObject("RedBossTeleportVfxPoolHost");
            DontDestroyOnLoad(hostGo);
            host = hostGo.AddComponent<PoolHost>();
        }

        if (sharedDiamondTex == null) sharedDiamondTex = CreateDiamondTexture();
        if (sharedSquareTex == null) sharedSquareTex = CreateSquareTexture();
        if (sharedPuffTex == null) sharedPuffTex = CreatePuffTexture();
        if (sharedRingTex == null) sharedRingTex = CreateRuneRingTexture();
        if (sharedCrossStarTex == null) sharedCrossStarTex = CreateCrossStarTexture();

        if (sharedDiamondMat == null) sharedDiamondMat = CreateMat("RedBossTP_DiamondMat", sharedDiamondTex);
        if (sharedSquareMat == null) sharedSquareMat = CreateMat("RedBossTP_SquareMat", sharedSquareTex);
        if (sharedPuffMat == null) sharedPuffMat = CreateMat("RedBossTP_PuffMat", sharedPuffTex);
        if (sharedRingMat == null) sharedRingMat = CreateMat("RedBossTP_RingMat", sharedRingTex);
        if (sharedCrossStarMat == null) sharedCrossStarMat = CreateMat("RedBossTP_CrossStarMat", sharedCrossStarTex);
    }

    private static Texture2D CreateDiamondTexture()
    {
        const int size = 9;
        Texture2D tex = NewPointTexture(size, "RedBossTP_DiamondTex");
        Color32[] px = new Color32[size * size];
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                byte a = d <= 1 ? (byte)255 : (d <= c ? (byte)195 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateSquareTexture()
    {
        const int size = 5;
        Texture2D tex = NewPointTexture(size, "RedBossTP_SquareTex");
        Color32[] px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = new Color32(255, 255, 255, 255);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreatePuffTexture()
    {
        const int size = 10;
        Texture2D tex = NewPointTexture(size, "RedBossTP_PuffTex");
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = r <= 2.6f ? (byte)255 : (r <= 4.3f ? (byte)165 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateRuneRingTexture()
    {
        const int size = 24;
        Texture2D tex = NewPointTexture(size, "RedBossTP_RuneRingTex");
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float adx = Mathf.Abs(dx);
                float ady = Mathf.Abs(dy);

                bool mainRing = r >= 7.6f && r <= 9.6f;
                bool outerTrim = r >= 6.8f && r <= 10.3f;
                // 네 방위 룬 포인트 강조
                bool cardinalTick = (adx <= 1.2f || ady <= 1.2f) && r >= 6.4f && r <= 11.4f;
                byte a = (mainRing || cardinalTick) ? (byte)255 : (outerTrim ? (byte)130 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateCrossStarTexture()
    {
        const int size = 15;
        Texture2D tex = NewPointTexture(size, "RedBossTP_CrossStarTex");
        Color32[] px = new Color32[size * size];
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int ax = Mathf.Abs(x - c);
                int ay = Mathf.Abs(y - c);
                bool core = ax + ay <= 2;
                bool diag = ax == ay && ax <= 4;
                bool cross = (ax == 0 && ay <= c) || (ay == 0 && ax <= c);
                byte a = (core || diag) ? (byte)255 : (cross ? (byte)215 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D NewPointTexture(int size, string texName)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = texName
        };
    }

    private static Material CreateMat(string matName, Texture2D tex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                        ?? Shader.Find("Sprites/Default")
                        ?? Shader.Find("Particles/Standard Unlit");
        if (shader == null) return null;

        return new Material(shader)
        {
            name = matName,
            mainTexture = tex,
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
