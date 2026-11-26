using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트리거 등으로 적이 등장할 때 응집 텔레그래프(Stage 1)와 실체화 폭발 버스트(Stage 2)
/// 픽셀 아트 파티클 연출을 독립적으로 생성·재생하는 풀링 VFX 컴포넌트입니다.
/// </summary>
public class EnemySpawnParticles : MonoBehaviour
{
    private const float BurstCleanupDelay = 0.65f;

    private struct GatherParticleData
    {
        public Vector2 startOffset;
        public Vector2 targetOffset;
        public float phaseOffset;
        public float spinDir;
        public float swirlStrength;
        public float baseSize;
        public float staggerDelay;
        public bool useCoreColor;
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
    private static readonly Queue<EnemySpawnParticles> pool = new Queue<EnemySpawnParticles>();
    private static readonly List<EnemySpawnParticles> activeInstances = new List<EnemySpawnParticles>();

    private static Texture2D sharedPixelTex;
    private static Texture2D sharedDiamondTex;
    private static Texture2D sharedRingTex;
    private static Texture2D sharedStarTex;
    private static Texture2D sharedPuffTex;

    private static Material sharedPixelMat;
    private static Material sharedDiamondMat;
    private static Material sharedRingMat;
    private static Material sharedStarMat;
    private static Material sharedPuffMat;

    private ParticleSystem gatherPixelPS;
    private ParticleSystem gatherDiamondPS;
    private ParticleSystem riftCorePS;
    private ParticleSystem flashStarPS;
    private ParticleSystem burstRingPS;
    private ParticleSystem burstSparkPS;
    private ParticleSystem burstSmokePS;
    private bool built;

    private ParticleSystem.Particle[] gatherPixelBuffer;
    private ParticleSystem.Particle[] gatherDiamondBuffer;
    private ParticleSystem.Particle[] riftCoreBuffer;
    private GatherParticleData[] gatherPixelData;
    private GatherParticleData[] gatherDiamondData;

    private GameObject targetEnemy;
    private bool trackEnemyObject;
    private float telegraphDuration;
    private float elapsed;
    private bool materialized;
    private bool isPlaying;
    private Action onMaterialize;

    private Vector3 spawnCenter;
    private Vector2 spawnSize;
    private float effectScale = 1f;
    private int targetSortingLayerId;
    private int targetBaseOrder = 252;

    private Color coreColor;
    private Color midColor;
    private Color deepColor;

    public static EnemySpawnParticles PlaySpawnSequence(
        GameObject enemy,
        float telegraphDuration = 0.42f,
        Action onMaterialize = null)
    {
        if (enemy == null)
        {
            return null;
        }

        ResolveEnemyVisualContext(
            enemy,
            out Vector3 center,
            out Vector2 size,
            out int sortingLayerId,
            out int baseOrder);

        return PlayInternal(
            enemy,
            trackEnemy: true,
            center,
            size,
            telegraphDuration,
            onMaterialize,
            sortingLayerId,
            baseOrder);
    }

    public static EnemySpawnParticles PlayMaterializeBurst(GameObject enemy)
    {
        return PlaySpawnSequence(enemy, 0f, null);
    }

    public static EnemySpawnParticles PlayAt(
        Vector3 worldCenter,
        Vector2 boundsSize,
        float telegraphDuration = 0.42f,
        Action onMaterialize = null,
        int sortingLayerId = 0,
        int sortingOrder = 252)
    {
        return PlayInternal(
            null,
            trackEnemy: false,
            worldCenter,
            boundsSize,
            telegraphDuration,
            onMaterialize,
            sortingLayerId,
            sortingOrder);
    }

    private static EnemySpawnParticles PlayInternal(
        GameObject enemy,
        bool trackEnemy,
        Vector3 center,
        Vector2 size,
        float duration,
        Action materializeCallback,
        int sortingLayerId,
        int baseOrder)
    {
        EnsureHostAndAssets();
        EnemySpawnParticles instance = AcquireInstance();
        if (instance == null)
        {
            materializeCallback?.Invoke();
            return null;
        }

        instance.Begin(
            enemy,
            trackEnemy,
            center,
            size,
            duration,
            materializeCallback,
            sortingLayerId,
            baseOrder);
        return instance;
    }

    public void CancelAndReturn()
    {
        if (!isPlaying)
        {
            return;
        }

        isPlaying = false;
        onMaterialize = null;
        targetEnemy = null;
        trackEnemyObject = false;
        StopAndClearAll();
        ReleaseInstance(this);
    }

    private void Begin(
        GameObject enemy,
        bool trackEnemy,
        Vector3 center,
        Vector2 size,
        float duration,
        Action materializeCallback,
        int sortingLayerId,
        int baseOrder)
    {
        EnsureBuilt();
        StopAndClearAll();

        targetEnemy = enemy;
        trackEnemyObject = trackEnemy;
        spawnCenter = center;
        spawnSize = new Vector2(Mathf.Max(0.7f, size.x), Mathf.Max(0.9f, size.y));
        effectScale = Mathf.Clamp(Mathf.Max(spawnSize.x, spawnSize.y) * 0.72f, 0.85f, 3.5f);
        targetSortingLayerId = sortingLayerId;
        targetBaseOrder = baseOrder;

        telegraphDuration = Mathf.Max(0f, duration);
        elapsed = 0f;
        materialized = false;
        isPlaying = true;
        onMaterialize = materializeCallback;

        transform.position = spawnCenter;
        ResolvePalette();
        ApplySortingAll();

        if (!activeInstances.Contains(this))
        {
            activeInstances.Add(this);
        }

        if (telegraphDuration <= 0.001f)
        {
            TriggerMaterialize();
            return;
        }

        PrepareGatherData();
        UpdateGatherSimulation(0f);
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        if (trackEnemyObject && targetEnemy == null)
        {
            CancelAndReturn();
            return;
        }

        elapsed += Time.deltaTime;

        if (!materialized)
        {
            float t = Mathf.Clamp01(elapsed / telegraphDuration);
            UpdateGatherSimulation(t);

            if (t >= 1f)
            {
                TriggerMaterialize();
            }
        }
        else if (elapsed >= telegraphDuration + BurstCleanupDelay)
        {
            isPlaying = false;
            targetEnemy = null;
            trackEnemyObject = false;
            StopAndClearAll();
            ReleaseInstance(this);
        }
    }

    private void TriggerMaterialize()
    {
        materialized = true;

        Action callback = onMaterialize;
        onMaterialize = null;
        callback?.Invoke();

        if (trackEnemyObject && targetEnemy == null)
        {
            CancelAndReturn();
            return;
        }

        if (targetEnemy != null)
        {
            ResolveEnemyVisualContext(
                targetEnemy,
                out spawnCenter,
                out Vector2 refreshedSize,
                out targetSortingLayerId,
                out targetBaseOrder);

            spawnSize = new Vector2(Mathf.Max(0.7f, refreshedSize.x), Mathf.Max(0.9f, refreshedSize.y));
            effectScale = Mathf.Clamp(Mathf.Max(spawnSize.x, spawnSize.y) * 0.72f, 0.85f, 3.5f);
            transform.position = spawnCenter;
            ApplySortingAll();
        }

        ClearManagedGatherSystems();
        EmitMaterializeBurst();
    }

    private void PrepareGatherData()
    {
        const int pixelCount = 20;
        const int diamondCount = 16;

        if (gatherPixelData == null || gatherPixelData.Length != pixelCount)
        {
            gatherPixelData = new GatherParticleData[pixelCount];
            gatherPixelBuffer = new ParticleSystem.Particle[pixelCount];
        }

        if (gatherDiamondData == null || gatherDiamondData.Length != diamondCount)
        {
            gatherDiamondData = new GatherParticleData[diamondCount];
            gatherDiamondBuffer = new ParticleSystem.Particle[diamondCount];
        }

        if (riftCoreBuffer == null || riftCoreBuffer.Length != 2)
        {
            riftCoreBuffer = new ParticleSystem.Particle[2];
        }

        float halfW = spawnSize.x * 0.5f;
        float halfH = spawnSize.y * 0.5f;

        for (int i = 0; i < pixelCount; i++)
        {
            gatherPixelData[i] = CreateGatherParticle(halfW, halfH, isDiamond: false);
        }

        for (int i = 0; i < diamondCount; i++)
        {
            gatherDiamondData[i] = CreateGatherParticle(halfW, halfH, isDiamond: true);
        }

        ActivateManagedSystem(gatherPixelPS);
        ActivateManagedSystem(gatherDiamondPS);
        ActivateManagedSystem(riftCorePS);
    }

    private GatherParticleData CreateGatherParticle(float halfW, float halfH, bool isDiamond)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radialX = halfW + UnityEngine.Random.Range(1.1f, 2.3f) * effectScale;
        float radialY = halfH + UnityEngine.Random.Range(0.95f, 2.05f) * effectScale;

        Vector2 start = new Vector2(Mathf.Cos(angle) * radialX, Mathf.Sin(angle) * radialY);
        Vector2 target = new Vector2(
            UnityEngine.Random.Range(-halfW * 0.55f, halfW * 0.55f),
            UnityEngine.Random.Range(-halfH * 0.55f, halfH * 0.55f));

        return new GatherParticleData
        {
            startOffset = start,
            targetOffset = target,
            phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            spinDir = UnityEngine.Random.value < 0.5f ? 1f : -1f,
            swirlStrength = UnityEngine.Random.Range(0.4f, 1.05f) * effectScale,
            baseSize = (isDiamond ? UnityEngine.Random.Range(0.20f, 0.36f) : UnityEngine.Random.Range(0.15f, 0.28f))
                       * Mathf.Lerp(1f, effectScale, 0.35f),
            staggerDelay = UnityEngine.Random.Range(0f, 0.16f),
            useCoreColor = isDiamond || UnityEngine.Random.value < 0.35f
        };
    }

    private void UpdateGatherSimulation(float globalT)
    {
        SimulateGatherLayer(gatherPixelPS, gatherPixelBuffer, gatherPixelData, globalT);
        SimulateGatherLayer(gatherDiamondPS, gatherDiamondBuffer, gatherDiamondData, globalT);

        if (riftCorePS != null && riftCoreBuffer != null)
        {
            float ease = 1f - Mathf.Pow(1f - globalT, 2f);
            float pulse = 1f + 0.14f * Mathf.Sin(globalT * Mathf.PI * 6f);
            float alpha = Mathf.Lerp(0.28f, 0.96f, ease);

            Color starCol = Color.Lerp(midColor, coreColor, ease * 0.85f);
            starCol.a = alpha;

            Color diamondCol = Color.Lerp(deepColor, coreColor, ease);
            diamondCol.a = alpha * 0.78f;

            riftCoreBuffer[0] = BuildManagedParticle(
                Vector3.zero,
                Mathf.Lerp(0.35f, 1.35f, ease) * effectScale * pulse,
                globalT * 160f,
                starCol);

            riftCoreBuffer[1] = BuildManagedParticle(
                Vector3.zero,
                Mathf.Lerp(0.45f, 0.95f, ease) * effectScale,
                -globalT * 110f,
                diamondCol);

            riftCorePS.SetParticles(riftCoreBuffer, riftCoreBuffer.Length);
        }
    }

    private void SimulateGatherLayer(
        ParticleSystem ps,
        ParticleSystem.Particle[] buffer,
        GatherParticleData[] data,
        float globalT)
    {
        if (ps == null || buffer == null || data == null)
        {
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            GatherParticleData d = data[i];
            float localT = Mathf.Clamp01((globalT - d.staggerDelay) / Mathf.Max(0.1f, 1f - d.staggerDelay));
            float ease = localT * localT * (3f - 2f * localT);

            Vector2 basePos = Vector2.Lerp(d.startOffset, d.targetOffset, ease);
            Vector2 delta = d.targetOffset - d.startOffset;
            Vector2 perp = delta.sqrMagnitude > 0.001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;

            float arc = Mathf.Sin(localT * Mathf.PI) * d.swirlStrength * d.spinDir * (1f - ease * 0.75f);
            Vector2 pos = basePos + perp * arc;

            float alpha = localT < 0.18f ? localT / 0.18f : 1f;
            float size = d.baseSize * Mathf.Lerp(0.65f, 1.22f, localT);
            Color c = ResolveGatherColor(localT, d.useCoreColor, alpha * 0.95f);

            buffer[i] = BuildManagedParticle(
                new Vector3(pos.x, pos.y, 0f),
                size,
                (d.phaseOffset + localT * 3.5f * d.spinDir) * Mathf.Rad2Deg,
                c);
        }

        ps.SetParticles(buffer, data.Length);
    }

    private Color ResolveGatherColor(float localT, bool useCore, float alpha)
    {
        Color c;
        if (localT < 0.7f)
        {
            Color targetMid = useCore ? Color.Lerp(midColor, coreColor, 0.4f) : midColor;
            c = Color.Lerp(deepColor, targetMid, localT / 0.7f);
        }
        else
        {
            float flashT = (localT - 0.7f) / 0.3f;
            c = Color.Lerp(midColor, coreColor, flashT * (useCore ? 0.95f : 0.7f));
        }

        c.a = alpha;
        return c;
    }

    private void EmitMaterializeBurst()
    {
        float sparkRadius = Mathf.Clamp(Mathf.Min(spawnSize.x, spawnSize.y) * 0.28f, 0.18f, 0.95f);

        if (flashStarPS != null)
        {
            var main = flashStarPS.main;
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f * effectScale, 1.55f * effectScale);
            main.startColor = new ParticleSystem.MinMaxGradient(coreColor, midColor);

            var col = flashStarPS.colorOverLifetime;
            col.color = BuildBurstGradient(coreColor, midColor, deepColor, 0.95f);

            flashStarPS.Play(false);
            flashStarPS.Emit(2);
        }

        if (burstRingPS != null)
        {
            var main = burstRingPS.main;
            main.startSize = new ParticleSystem.MinMaxCurve(1.35f * effectScale, 1.65f * effectScale);
            main.startColor = Color.Lerp(midColor, coreColor, 0.55f);

            var col = burstRingPS.colorOverLifetime;
            col.color = BuildBurstGradient(coreColor, midColor, deepColor, 0.85f);

            burstRingPS.Play(false);
            burstRingPS.Emit(1);
        }

        if (burstSparkPS != null)
        {
            var main = burstSparkPS.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f * effectScale, 6.2f * effectScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.16f * Mathf.Lerp(1f, effectScale, 0.35f),
                0.34f * Mathf.Lerp(1f, effectScale, 0.35f));
            main.startColor = new ParticleSystem.MinMaxGradient(coreColor, midColor);

            var shape = burstSparkPS.shape;
            shape.radius = sparkRadius;

            var col = burstSparkPS.colorOverLifetime;
            col.color = BuildBurstGradient(coreColor, midColor, deepColor, 1f);

            burstSparkPS.Play(false);
            burstSparkPS.Emit(18);
        }

        if (burstSmokePS != null)
        {
            var main = burstSmokePS.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * effectScale, 2.4f * effectScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.28f * Mathf.Lerp(1f, effectScale, 0.45f),
                0.56f * Mathf.Lerp(1f, effectScale, 0.45f));
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(midColor, coreColor, 0.25f),
                deepColor);

            var shape = burstSmokePS.shape;
            shape.radius = sparkRadius * 1.15f;

            var col = burstSmokePS.colorOverLifetime;
            col.color = BuildBurstGradient(
                Color.Lerp(midColor, coreColor, 0.2f),
                midColor,
                deepColor,
                0.72f);

            burstSmokePS.Play(false);
            burstSmokePS.Emit(12);
        }
    }

    private void ResolvePalette()
    {
        coreColor = new Color(0.76f, 0.78f, 0.80f, 1f);
        midColor = new Color(0.32f, 0.34f, 0.36f, 1f);
        deepColor = new Color(0.06f, 0.06f, 0.08f, 1f);
    }

    private static void ResolveEnemyVisualContext(
        GameObject enemy,
        out Vector3 center,
        out Vector2 size,
        out int sortingLayerId,
        out int baseOrder)
    {
        center = enemy.transform.position;
        size = new Vector2(1.1f, 1.4f);
        sortingLayerId = 0;
        baseOrder = 252;

        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null)
        {
            sortingLayerId = sr.sortingLayerID;
            baseOrder = Mathf.Max(250, sr.sortingOrder);
        }

        // 비활성 오브젝트에서도 정확한 등장 중심과 크기를 얻기 위해 콜라이더/스프라이트 로컬 데이터를 월드 좌표로 직접 변환
        Collider2D col = enemy.GetComponent<Collider2D>() ?? enemy.GetComponentInChildren<Collider2D>(true);
        if (TryComputeColliderBounds(col, out Vector3 colCenter, out Vector2 colSize))
        {
            center = colCenter;
            size = colSize;
            return;
        }

        if (sr != null && sr.sprite != null)
        {
            Bounds spriteBounds = sr.sprite.bounds;
            Vector3 lossy = sr.transform.lossyScale;
            center = sr.transform.TransformPoint(spriteBounds.center);
            size = new Vector2(
                spriteBounds.size.x * Mathf.Abs(lossy.x),
                spriteBounds.size.y * Mathf.Abs(lossy.y));
        }
    }

    private static bool TryComputeColliderBounds(Collider2D col, out Vector3 center, out Vector2 size)
    {
        center = Vector3.zero;
        size = Vector2.zero;
        if (col == null)
        {
            return false;
        }

        Transform tf = col.transform;
        Vector3 lossy = tf.lossyScale;
        float sx = Mathf.Abs(lossy.x);
        float sy = Mathf.Abs(lossy.y);

        if (col is BoxCollider2D box)
        {
            center = tf.TransformPoint(box.offset);
            size = new Vector2(box.size.x * sx, box.size.y * sy);
            return true;
        }

        if (col is CircleCollider2D circle)
        {
            center = tf.TransformPoint(circle.offset);
            float diameter = circle.radius * 2f * Mathf.Max(sx, sy);
            size = new Vector2(diameter, diameter);
            return true;
        }

        if (col is CapsuleCollider2D capsule)
        {
            center = tf.TransformPoint(capsule.offset);
            size = new Vector2(capsule.size.x * sx, capsule.size.y * sy);
            return true;
        }

        if (col.enabled && col.gameObject.activeInHierarchy)
        {
            Bounds b = col.bounds;
            center = b.center;
            size = new Vector2(b.size.x, b.size.y);
            return true;
        }

        return false;
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        EnsureHostAndAssets();

        gatherPixelPS = CreateManagedSystem("SpawnGatherPixels", sharedPixelMat, 258);
        gatherDiamondPS = CreateManagedSystem("SpawnGatherDiamonds", sharedDiamondMat, 259);
        riftCorePS = CreateManagedSystem("SpawnRiftCore", sharedStarMat, 260);

        flashStarPS = CreateFlashStarSystem("SpawnFlashStar", sharedStarMat, 261);
        burstRingPS = CreateBurstRingSystem("SpawnBurstRing", sharedRingMat, 259);
        burstSparkPS = CreateBurstSparkSystem("SpawnBurstSparks", sharedDiamondMat, 260);
        burstSmokePS = CreateBurstSmokeSystem("SpawnBurstSmoke", sharedPuffMat, 258);

        built = true;
    }

    private void ApplySortingAll()
    {
        ApplySorting(burstSmokePS, targetSortingLayerId, targetBaseOrder + 4);
        ApplySorting(burstRingPS, targetSortingLayerId, targetBaseOrder + 5);
        ApplySorting(gatherPixelPS, targetSortingLayerId, targetBaseOrder + 6);
        ApplySorting(burstSparkPS, targetSortingLayerId, targetBaseOrder + 6);
        ApplySorting(gatherDiamondPS, targetSortingLayerId, targetBaseOrder + 7);
        ApplySorting(riftCorePS, targetSortingLayerId, targetBaseOrder + 8);
        ApplySorting(flashStarPS, targetSortingLayerId, targetBaseOrder + 9);
    }

    private void ClearManagedGatherSystems()
    {
        if (gatherPixelPS != null) gatherPixelPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (gatherDiamondPS != null) gatherDiamondPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (riftCorePS != null) riftCorePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StopAndClearAll()
    {
        ClearManagedGatherSystems();
        if (flashStarPS != null) flashStarPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (burstRingPS != null) burstRingPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (burstSparkPS != null) burstSparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (burstSmokePS != null) burstSmokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void ActivateManagedSystem(ParticleSystem ps)
    {
        if (ps != null && !ps.isPlaying)
        {
            ps.Play(false);
        }
    }

    private ParticleSystem CreateBaseChildSystem(string childName, Material mat, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.SetActive(false);
        child.transform.SetParent(transform, false);

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

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

    private ParticleSystem CreateManagedSystem(string childName, Material mat, int sortingOrder)
    {
        ParticleSystem ps = CreateBaseChildSystem(childName, mat, sortingOrder);
        var main = ps.main;
        main.loop = true;
        main.maxParticles = 64;
        return ps;
    }

    private ParticleSystem CreateFlashStarSystem(string childName, Material mat, int sortingOrder)
    {
        ParticleSystem ps = CreateBaseChildSystem(childName, mat, sortingOrder);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
        main.maxParticles = 4;

        var col = ps.colorOverLifetime;
        col.enabled = true;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.22f, 1.25f),
            new Keyframe(1f, 0f)
        ));

        return ps;
    }

    private ParticleSystem CreateBurstRingSystem(string childName, Material mat, int sortingOrder)
    {
        ParticleSystem ps = CreateBaseChildSystem(childName, mat, sortingOrder);
        var main = ps.main;
        main.startLifetime = 0.26f;
        main.startSpeed = 0f;
        main.maxParticles = 4;

        var col = ps.colorOverLifetime;
        col.enabled = true;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(1f, 1.55f)
        ));

        return ps;
    }

    private ParticleSystem CreateBurstSparkSystem(string childName, Material mat, int sortingOrder)
    {
        ParticleSystem ps = CreateBaseChildSystem(childName, mat, sortingOrder);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.44f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.25f;
        main.maxParticles = 32;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;
        shape.radiusThickness = 1f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 2.0f;
        noise.scrollSpeed = 1.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.25f, 1.1f),
            new Keyframe(1f, 0f)
        ));

        return ps;
    }

    private ParticleSystem CreateBurstSmokeSystem(string childName, Material mat, int sortingOrder)
    {
        ParticleSystem ps = CreateBaseChildSystem(childName, mat, sortingOrder);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.54f);
        main.gravityModifier = -0.35f;
        main.maxParticles = 24;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.radiusThickness = 1f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 1.8f;
        noise.scrollSpeed = 1.0f;

        var col = ps.colorOverLifetime;
        col.enabled = true;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.75f),
            new Keyframe(0.3f, 1.1f),
            new Keyframe(1f, 0.1f)
        ));

        return ps;
    }

    private static void ApplySorting(ParticleSystem ps, int layerId, int order)
    {
        if (ps == null) return;
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.sortingLayerID = layerId;
        r.sortingOrder = order;
    }

    private static ParticleSystem.Particle BuildManagedParticle(Vector3 localPos, float size, float rotDeg, Color color)
    {
        return new ParticleSystem.Particle
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
    }

    private static ParticleSystem.MinMaxGradient BuildBurstGradient(Color start, Color mid, Color end, float peakAlpha)
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
                new GradientAlphaKey(peakAlpha, 0f),
                new GradientAlphaKey(peakAlpha * 0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return new ParticleSystem.MinMaxGradient(grad);
    }

    private static EnemySpawnParticles AcquireInstance()
    {
        while (pool.Count > 0)
        {
            EnemySpawnParticles pooled = pool.Dequeue();
            if (pooled != null)
            {
                pooled.gameObject.SetActive(true);
                return pooled;
            }
        }

        if (host == null)
        {
            return null;
        }

        GameObject go = new GameObject("EnemySpawnVfxInstance");
        go.transform.SetParent(host.transform, false);
        EnemySpawnParticles comp = go.AddComponent<EnemySpawnParticles>();
        comp.EnsureBuilt();
        return comp;
    }

    private static void ReleaseInstance(EnemySpawnParticles instance)
    {
        if (instance == null) return;

        activeInstances.Remove(instance);
        if (host != null && instance.transform.parent != host.transform)
        {
            instance.transform.SetParent(host.transform, false);
        }

        instance.gameObject.SetActive(false);
        if (!pool.Contains(instance))
        {
            pool.Enqueue(instance);
        }
    }

    private static void ReturnAllActive()
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            EnemySpawnParticles inst = activeInstances[i];
            if (inst != null)
            {
                inst.CancelAndReturn();
            }
        }
        activeInstances.Clear();
    }

    private static void EnsureHostAndAssets()
    {
        if (host == null)
        {
            GameObject hostGo = new GameObject("EnemySpawnVfxPoolHost");
            DontDestroyOnLoad(hostGo);
            host = hostGo.AddComponent<PoolHost>();
        }

        if (sharedPixelTex == null) sharedPixelTex = CreatePixelTexture();
        if (sharedDiamondTex == null) sharedDiamondTex = CreateDiamondTexture();
        if (sharedRingTex == null) sharedRingTex = CreateRingTexture();
        if (sharedStarTex == null) sharedStarTex = CreateStarTexture();
        if (sharedPuffTex == null) sharedPuffTex = CreatePuffTexture();

        if (sharedPixelMat == null) sharedPixelMat = CreateMat("EnemySpawn_PixelMat", sharedPixelTex);
        if (sharedDiamondMat == null) sharedDiamondMat = CreateMat("EnemySpawn_DiamondMat", sharedDiamondTex);
        if (sharedRingMat == null) sharedRingMat = CreateMat("EnemySpawn_RingMat", sharedRingTex);
        if (sharedStarMat == null) sharedStarMat = CreateMat("EnemySpawn_StarMat", sharedStarTex);
        if (sharedPuffMat == null) sharedPuffMat = CreateMat("EnemySpawn_PuffMat", sharedPuffTex);
    }

    private static Texture2D CreatePixelTexture()
    {
        const int size = 6;
        Texture2D tex = NewPointTexture(size, "EnemySpawn_PixelTex");
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                px[y * size + x] = new Color32(255, 255, 255, corner ? (byte)0 : (byte)255);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateDiamondTexture()
    {
        const int size = 9;
        Texture2D tex = NewPointTexture(size, "EnemySpawn_DiamondTex");
        Color32[] px = new Color32[size * size];
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dist = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                byte a = dist <= 1 ? (byte)255 : (dist <= c ? (byte)195 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateRingTexture()
    {
        const int size = 24;
        Texture2D tex = NewPointTexture(size, "EnemySpawn_RingTex");
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (r >= 7.8f && r <= 10.6f) ? (byte)245 : ((r >= 6.8f && r <= 11.4f) ? (byte)110 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateStarTexture()
    {
        const int size = 13;
        Texture2D tex = NewPointTexture(size, "EnemySpawn_StarTex");
        Color32[] px = new Color32[size * size];
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int ax = Mathf.Abs(x - c);
                int ay = Mathf.Abs(y - c);
                bool core = ax + ay <= 2;
                bool arm = (ax == 0 && ay <= c) || (ay == 0 && ax <= c);
                bool inner = ax <= 1 && ay <= 1;
                byte a = (core || inner) ? (byte)255 : (arm ? (byte)215 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreatePuffTexture()
    {
        const int size = 10;
        Texture2D tex = NewPointTexture(size, "EnemySpawn_PuffTex");
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = d <= 2.8f ? (byte)255 : (d <= 4.3f ? (byte)140 : (byte)0);
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
}
