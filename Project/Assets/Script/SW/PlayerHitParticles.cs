using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 피격 시 과도하게 큰 기존 프리팹 파티클을 대체해
/// 카메라 스케일에 맞춰 선명하게 보이는 순백색 픽셀 섬광·충격 링·스파크·도트 이펙트를 재생합니다.
/// </summary>
public class PlayerHitParticles : MonoBehaviour
{
    private const float CleanupDelay = 0.55f;

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
    private static readonly Queue<PlayerHitParticles> pool = new Queue<PlayerHitParticles>();
    private static readonly List<PlayerHitParticles> activeInstances = new List<PlayerHitParticles>();

    private static Texture2D sharedStarTex;
    private static Texture2D sharedDiamondTex;
    private static Texture2D sharedRingTex;
    private static Texture2D sharedDropletTex;

    private static Material sharedStarMat;
    private static Material sharedDiamondMat;
    private static Material sharedRingMat;
    private static Material sharedDropletMat;

    private ParticleSystem flashPS;
    private ParticleSystem sparkPS;
    private ParticleSystem ringPS;
    private ParticleSystem dropletPS;
    private Transform followTarget;
    private Collider2D followCollider;
    private Coroutine returnRoutine;
    private bool built;

    public static void Play(Transform target, SpriteRenderer targetSprite = null)
    {
        if (target == null)
        {
            return;
        }

        Collider2D col = target.GetComponent<Collider2D>();
        Vector3 center = ResolveTargetCenter(target, col);

        int layerId = targetSprite != null ? targetSprite.sortingLayerID : 0;
        int baseOrder = targetSprite != null ? targetSprite.sortingOrder : 300;
        PlayInternalStatic(center, target, col, layerId, baseOrder);
    }

    public static void PlayAt(Vector3 worldCenter, int sortingLayerId = 0, int baseSortingOrder = 300)
    {
        PlayInternalStatic(worldCenter, null, null, sortingLayerId, baseSortingOrder);
    }

    private static void PlayInternalStatic(
        Vector3 worldCenter,
        Transform target,
        Collider2D targetCollider,
        int sortingLayerId,
        int baseSortingOrder)
    {
        EnsureHostAndAssets();
        PlayerHitParticles inst = AcquireInstance();
        if (inst == null)
        {
            return;
        }

        inst.PlayInternal(worldCenter, target, targetCollider, sortingLayerId, baseSortingOrder);
    }

    private static Vector3 ResolveTargetCenter(Transform target, Collider2D col)
    {
        if (col != null && col.enabled)
        {
            return col.bounds.center;
        }
        return target.position;
    }

    private void LateUpdate()
    {
        if (followTarget != null && followTarget.gameObject.activeInHierarchy)
        {
            transform.position = ResolveTargetCenter(followTarget, followCollider);
        }
    }

    private void PlayInternal(
        Vector3 worldCenter,
        Transform target,
        Collider2D targetCollider,
        int sortingLayerId,
        int baseSortingOrder)
    {
        EnsureBuilt();
        StopAllSystems();

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        followTarget = target;
        followCollider = targetCollider;
        transform.position = worldCenter;
        ApplySorting(dropletPS, sortingLayerId, baseSortingOrder + 2);
        ApplySorting(ringPS, sortingLayerId, baseSortingOrder + 3);
        ApplySorting(sparkPS, sortingLayerId, baseSortingOrder + 4);
        ApplySorting(flashPS, sortingLayerId, baseSortingOrder + 5);

        if (!activeInstances.Contains(this))
        {
            activeInstances.Add(this);
        }

        if (flashPS != null)
        {
            flashPS.Play(false);
            flashPS.Emit(3);
        }
        if (ringPS != null)
        {
            ringPS.Play(false);
            ringPS.Emit(1);
        }
        if (sparkPS != null)
        {
            sparkPS.Play(false);
            sparkPS.Emit(18);
        }
        if (dropletPS != null)
        {
            dropletPS.Play(false);
            dropletPS.Emit(10);
        }

        returnRoutine = StartCoroutine(ReturnAfterDelay());
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(CleanupDelay);
        returnRoutine = null;
        ForceReturnNow();
    }

    private void ForceReturnNow()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
        followTarget = null;
        followCollider = null;
        StopAllSystems();
        ReleaseInstance(this);
    }

    private void StopAllSystems()
    {
        if (flashPS != null) flashPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (sparkPS != null) sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (ringPS != null) ringPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (dropletPS != null) dropletPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        EnsureHostAndAssets();

        flashPS = CreateFlashSystem();
        ringPS = CreateRingSystem();
        sparkPS = CreateSparkSystem();
        dropletPS = CreateDropletSystem();

        built = true;
    }

    private ParticleSystem CreateFlashSystem()
    {
        ParticleSystem ps = CreateBaseSystem("HitFlash", sharedStarMat, 305);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.20f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.10f, 1.45f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.white,
            new Color(0.92f, 0.96f, 1f, 1f));
        main.maxParticles = 6;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(0.94f, 0.97f, 1f, 0.92f),
            new Color(0.82f, 0.88f, 0.96f, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.50f),
            new Keyframe(0.22f, 1.15f),
            new Keyframe(1f, 0f)));

        return ps;
    }

    private ParticleSystem CreateRingSystem()
    {
        ParticleSystem ps = CreateBaseSystem("HitRing", sharedRingMat, 303);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.20f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.25f, 1.55f);
        main.startColor = new Color(1f, 1f, 1f, 0.95f);
        main.maxParticles = 4;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(1f, 1f, 1f, 0.95f),
            new Color(0.90f, 0.94f, 1f, 0.78f),
            new Color(0.75f, 0.82f, 0.92f, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(1f, 1.55f)));

        return ps;
    }

    private ParticleSystem CreateSparkSystem()
    {
        ParticleSystem ps = CreateBaseSystem("HitSparks", sharedDiamondMat, 304);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.40f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.6f, 7.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.36f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.55f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.white,
            new Color(0.88f, 0.92f, 0.98f, 1f));
        main.maxParticles = 28;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(0.92f, 0.95f, 1f, 0.92f),
            new Color(0.76f, 0.82f, 0.90f, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.3f, 1.08f),
            new Keyframe(1f, 0f)));

        return ps;
    }

    private ParticleSystem CreateDropletSystem()
    {
        ParticleSystem ps = CreateBaseSystem("HitDroplets", sharedDropletMat, 302);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.26f, 0.44f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
        main.gravityModifier = 0.80f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.98f, 0.99f, 1f, 0.95f),
            new Color(0.78f, 0.82f, 0.88f, 0.95f));
        main.maxParticles = 16;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(1f, 1f, 1f, 0.95f),
            new Color(0.84f, 0.87f, 0.92f, 0.82f),
            new Color(0.65f, 0.70f, 0.78f, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(1f, 0.12f)));

        return ps;
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
                new GradientColorKey(mid, 0.4f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a, 0.4f),
                new GradientAlphaKey(end.a, 1f)
            });
        return new ParticleSystem.MinMaxGradient(grad);
    }

    private static PlayerHitParticles AcquireInstance()
    {
        while (pool.Count > 0)
        {
            PlayerHitParticles pooled = pool.Dequeue();
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

        GameObject go = new GameObject("PlayerHitVfxInstance");
        go.transform.SetParent(host.transform, false);
        PlayerHitParticles inst = go.AddComponent<PlayerHitParticles>();
        inst.EnsureBuilt();
        return inst;
    }

    private static void ReleaseInstance(PlayerHitParticles inst)
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
            PlayerHitParticles inst = activeInstances[i];
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
            GameObject hostGo = new GameObject("PlayerHitVfxPoolHost");
            DontDestroyOnLoad(hostGo);
            host = hostGo.AddComponent<PoolHost>();
        }

        if (sharedStarTex == null) sharedStarTex = CreateStarTexture();
        if (sharedDiamondTex == null) sharedDiamondTex = CreateDiamondTexture();
        if (sharedRingTex == null) sharedRingTex = CreateRingTexture();
        if (sharedDropletTex == null) sharedDropletTex = CreateDropletTexture();

        if (sharedStarMat == null) sharedStarMat = CreateMat("PlayerHit_StarMat", sharedStarTex);
        if (sharedDiamondMat == null) sharedDiamondMat = CreateMat("PlayerHit_DiamondMat", sharedDiamondTex);
        if (sharedRingMat == null) sharedRingMat = CreateMat("PlayerHit_RingMat", sharedRingTex);
        if (sharedDropletMat == null) sharedDropletMat = CreateMat("PlayerHit_DropletMat", sharedDropletTex);
    }

    private static Texture2D CreateStarTexture()
    {
        const int size = 13;
        Texture2D tex = NewPointTexture(size, "PlayerHit_StarTex");
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
                byte a = (core || diag) ? (byte)255 : (cross ? (byte)205 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateDiamondTexture()
    {
        const int size = 7;
        Texture2D tex = NewPointTexture(size, "PlayerHit_DiamondTex");
        Color32[] px = new Color32[size * size];
        int c = size / 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dist = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                byte a = dist <= 1 ? (byte)255 : (dist <= c ? (byte)190 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateRingTexture()
    {
        const int size = 20;
        Texture2D tex = NewPointTexture(size, "PlayerHit_RingTex");
        Color32[] px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (r >= 5.6f && r <= 8.8f) ? (byte)255 : ((r >= 4.8f && r <= 9.5f) ? (byte)135 : (byte)0);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateDropletTexture()
    {
        const int size = 6;
        Texture2D tex = NewPointTexture(size, "PlayerHit_DropletTex");
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
