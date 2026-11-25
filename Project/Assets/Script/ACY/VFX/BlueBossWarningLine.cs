using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 블루보스 돌진 경로 경고를 픽셀 감성의 파티클 코리더로 렌더링합니다.
/// 실제 히트박스 두께(thickness)와 길이(length)에 맞춰 경계 레일과 방향성 흐름 파티클을 생성합니다.
/// </summary>
public class BlueBossWarningLine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private const float EdgeHalfWidth = 0.07f;
    private const float FlowTravelMargin = 1.2f;
    private const float DismissDrainDelay = 0.36f;

    private Coroutine currentEffectRoutine;
    private string myPoolKey;
    private bool systemsInitialized;
    private float currentLength = 10f;
    private float currentThickness = 1f;

    private ParticleSystem corridorFlowPs;
    private ParticleSystem edgeTopPs;
    private ParticleSystem edgeBottomPs;
    private ParticleSystem sparkPs;
    private ParticleSystem startAnchorPs;
    private readonly List<ParticleSystem> allParticles = new List<ParticleSystem>();

    private static Texture2D sharedSquareTex;
    private static Texture2D sharedDiamondTex;
    private static Material sharedSquareMat;
    private static Material sharedDiamondMat;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        DisableLegacySprite();
    }

    private void OnDisable()
    {
        if (currentEffectRoutine != null)
        {
            StopCoroutine(currentEffectRoutine);
            currentEffectRoutine = null;
        }
        StopAndClearParticles();
    }

    public void ActivateWarning(float duration, string poolKey)
    {
        float fallbackLength = Mathf.Max(1f, Mathf.Abs(transform.localScale.x));
        float fallbackThickness = Mathf.Max(0.2f, Mathf.Abs(transform.localScale.y));
        ActivateWarning(fallbackLength, fallbackThickness, duration, poolKey);
    }

    public void ActivateWarning(float length, float thickness, float maxDuration, string poolKey)
    {
        myPoolKey = poolKey;
        currentLength = Mathf.Max(0.5f, length);
        currentThickness = Mathf.Max(0.2f, thickness);

        // 비균등 스케일로 인한 파티클 왜곡을 막기 위해 루트 스케일은 항상 1로 유지
        transform.localScale = Vector3.one;
        DisableLegacySprite();
        EnsureParticleSystems();

        if (currentEffectRoutine != null)
        {
            StopCoroutine(currentEffectRoutine);
            currentEffectRoutine = null;
        }

        StopAndClearParticles();
        ConfigureGeometryAndRates(currentLength, currentThickness);
        PlayWithInitialFill();

        if (maxDuration > 0f)
        {
            currentEffectRoutine = StartCoroutine(AutoDismissAfterDelay(maxDuration));
        }
    }

    /// <summary>
    /// 돌진 직전 해당 경로가 곧 발동됨을 알리는 짧은 강조 버스트를 재생합니다.
    /// </summary>
    public void TriggerLockOnPulse()
    {
        if (!systemsInitialized || !gameObject.activeInHierarchy)
        {
            return;
        }

        int edgeBurst = Mathf.Clamp(Mathf.RoundToInt(currentLength * 1.1f), 20, 90);
        if (edgeTopPs != null) edgeTopPs.Emit(edgeBurst);
        if (edgeBottomPs != null) edgeBottomPs.Emit(edgeBurst);
        if (startAnchorPs != null) startAnchorPs.Emit(22);
    }

    /// <summary>
    /// 경고를 종료하고 풀에 반환합니다.
    /// </summary>
    public void DismissWarning(bool immediate = false)
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        if (currentEffectRoutine != null)
        {
            StopCoroutine(currentEffectRoutine);
            currentEffectRoutine = null;
        }

        if (immediate)
        {
            StopAndClearParticles();
            ReturnToPool();
            return;
        }

        StopEmittingParticles();
        currentEffectRoutine = StartCoroutine(ReturnAfterDrainRoutine());
    }

    private IEnumerator AutoDismissAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentEffectRoutine = null;
        StopEmittingParticles();
        yield return new WaitForSeconds(DismissDrainDelay);
        ReturnToPool();
    }

    private IEnumerator ReturnAfterDrainRoutine()
    {
        yield return new WaitForSeconds(DismissDrainDelay);
        currentEffectRoutine = null;
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (PoolingManager.Instance != null && !string.IsNullOrEmpty(myPoolKey))
        {
            PoolingManager.Instance.Return(myPoolKey, gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void DisableLegacySprite()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    private void EnsureParticleSystems()
    {
        if (systemsInitialized)
        {
            return;
        }

        EnsureSharedAssets();

        corridorFlowPs = CreateCorridorFlowParticles();
        edgeTopPs = CreateEdgeRailParticles("WarningEdgeTop");
        edgeBottomPs = CreateEdgeRailParticles("WarningEdgeBottom");
        sparkPs = CreateCorridorSparkParticles();
        startAnchorPs = CreateStartAnchorParticles();

        systemsInitialized = true;
    }

    private void ConfigureGeometryAndRates(float length, float thickness)
    {
        float spawnLength = Mathf.Max(0.5f, length - FlowTravelMargin);
        float spawnCenterX = (length > FlowTravelMargin) ? -FlowTravelMargin * 0.5f : 0f;
        float innerThickness = Mathf.Max(0.12f, thickness - 0.28f);
        float edgeOffsetY = Mathf.Max(0.02f, thickness * 0.5f - EdgeHalfWidth);

        if (corridorFlowPs != null)
        {
            corridorFlowPs.transform.localPosition = new Vector3(spawnCenterX, 0f, 0f);
            var shape = corridorFlowPs.shape;
            shape.scale = new Vector3(spawnLength, innerThickness, 0.1f);

            var main = corridorFlowPs.main;
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(length * thickness * 1.15f), 100, 480);

            var emission = corridorFlowPs.emission;
            emission.rateOverTime = Mathf.Clamp(length * thickness * 0.95f, 70f, 360f);
        }

        ConfigureEdgeSystem(edgeTopPs, spawnCenterX, edgeOffsetY, spawnLength, length);
        ConfigureEdgeSystem(edgeBottomPs, spawnCenterX, -edgeOffsetY, spawnLength, length);

        if (sparkPs != null)
        {
            sparkPs.transform.localPosition = new Vector3(spawnCenterX * 0.5f, 0f, 0f);
            var shape = sparkPs.shape;
            shape.scale = new Vector3(Mathf.Max(0.5f, length - 0.6f), innerThickness, 0.1f);

            var main = sparkPs.main;
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(length * thickness * 0.6f), 60, 260);

            var emission = sparkPs.emission;
            emission.rateOverTime = Mathf.Clamp(length * thickness * 0.45f, 35f, 180f);
        }

        if (startAnchorPs != null)
        {
            startAnchorPs.transform.localPosition = new Vector3(-length * 0.5f, 0f, 0f);
            var shape = startAnchorPs.shape;
            shape.scale = new Vector3(0.4f, Mathf.Max(0.3f, thickness * 0.85f), 0.1f);
        }
    }

    private static void ConfigureEdgeSystem(ParticleSystem ps, float localX, float localY, float spawnLength, float fullLength)
    {
        if (ps == null)
        {
            return;
        }

        ps.transform.localPosition = new Vector3(localX, localY, 0f);
        var shape = ps.shape;
        shape.scale = new Vector3(spawnLength, 0.02f, 0.1f);

        var main = ps.main;
        main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(fullLength * 3.5f), 80, 320);

        var emission = ps.emission;
        emission.rateOverTime = Mathf.Clamp(fullLength * 3.2f, 55f, 260f);
    }

    private void PlayWithInitialFill()
    {
        for (int i = 0; i < allParticles.Count; i++)
        {
            if (allParticles[i] != null)
            {
                allParticles[i].Play(false);
            }
        }

        // 생성 첫 프레임부터 경로 전체가 즉시 보이도록 초기 버스트 배치
        int corridorInitial = Mathf.Clamp(Mathf.RoundToInt(currentLength * currentThickness * 0.45f), 45, 220);
        int edgeInitial = Mathf.Clamp(Mathf.RoundToInt(currentLength * 1.4f), 30, 140);
        int sparkInitial = Mathf.Clamp(Mathf.RoundToInt(currentLength * currentThickness * 0.2f), 20, 100);

        if (corridorFlowPs != null) corridorFlowPs.Emit(corridorInitial);
        if (edgeTopPs != null) edgeTopPs.Emit(edgeInitial);
        if (edgeBottomPs != null) edgeBottomPs.Emit(edgeInitial);
        if (sparkPs != null) sparkPs.Emit(sparkInitial);
        if (startAnchorPs != null) startAnchorPs.Emit(18);
    }

    private void StopEmittingParticles()
    {
        for (int i = 0; i < allParticles.Count; i++)
        {
            if (allParticles[i] != null)
            {
                allParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void StopAndClearParticles()
    {
        for (int i = 0; i < allParticles.Count; i++)
        {
            if (allParticles[i] != null)
            {
                allParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private ParticleSystem CreateCorridorFlowParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("WarningCorridorFlow", out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.34f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.94f, 1f, 0.55f),
            new Color(0.18f, 0.68f, 1f, 0.45f)
        );
        main.maxParticles = 320;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 160f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(10f, 1f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(3.8f, 6.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(0.80f, 0.98f, 1f, 0f),
            new Color(0.26f, 0.80f, 1f, 0.52f),
            new Color(0.08f, 0.34f, 0.90f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0.15f)
        ));

        ConfigureStretchRenderer(ps, sharedSquareMat, 99, lengthScale: 2.2f, velocityScale: 0.06f);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateEdgeRailParticles(string objName)
    {
        ParticleSystem ps = CreateStoppedSystem(objName, out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(EdgeHalfWidth * 1.8f, EdgeHalfWidth * 2.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.92f, 0.99f, 1f, 1f),
            new Color(0.42f, 0.90f, 1f, 1f)
        );
        main.maxParticles = 240;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 140f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(10f, 0.02f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(3.6f, 5.8f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(0.95f, 1f, 1f, 0f),
            new Color(0.58f, 0.94f, 1f, 0.95f),
            new Color(0.14f, 0.56f, 1f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1f, 0.35f)
        ));

        ConfigureStretchRenderer(ps, sharedSquareMat, 101, lengthScale: 3.0f, velocityScale: 0.05f);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateCorridorSparkParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("WarningCorridorSparks", out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.94f, 0.99f, 1f, 0.95f),
            new Color(0.34f, 0.86f, 1f, 0.9f)
        );
        main.maxParticles = 180;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 80f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(10f, 1f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(0.92f, 0.99f, 1f, 0f),
            new Color(0.45f, 0.90f, 1f, 0.88f),
            new Color(0.10f, 0.42f, 0.95f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f)
        ));

        ConfigureBillboardRenderer(ps, sharedDiamondMat, 100);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateStartAnchorParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("WarningStartAnchor", out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.38f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.95f, 1f, 1f, 1f),
            new Color(0.38f, 0.88f, 1f, 1f)
        );
        main.maxParticles = 70;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.4f, 1f, 0.1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildGradient(
            new Color(1f, 1f, 1f, 0.95f),
            new Color(0.42f, 0.90f, 1f, 0.9f),
            new Color(0.10f, 0.44f, 0.96f, 0f)
        );

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.4f, 1f),
            new Keyframe(1f, 0f)
        ));

        ConfigureBillboardRenderer(ps, sharedDiamondMat, 102);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateStoppedSystem(string objName, out GameObject go)
    {
        go = new GameObject(objName);
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.playOnAwake = false;
        main.duration = 1f;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private static void ConfigureBillboardRenderer(ParticleSystem ps, Material mat, int sortingOrder)
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

    private static void ConfigureStretchRenderer(ParticleSystem ps, Material mat, int sortingOrder, float lengthScale, float velocityScale)
    {
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        if (pr == null) return;

        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.lengthScale = lengthScale;
        pr.velocityScale = velocityScale;
        pr.sortingOrder = sortingOrder;
        if (mat != null)
        {
            pr.sharedMaterial = mat;
        }
    }

    private static Gradient BuildGradient(Color start, Color mid, Color end)
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

    private static void EnsureSharedAssets()
    {
        if (sharedSquareTex == null)
        {
            sharedSquareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
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
            sharedSquareTex.SetPixels(pixels);
            sharedSquareTex.Apply(false, true);
        }

        if (sharedDiamondTex == null)
        {
            const int size = 7;
            sharedDiamondTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
                    float a = dist <= center ? (dist <= 1 ? 1f : 0.78f) : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sharedDiamondTex.SetPixels(pixels);
            sharedDiamondTex.Apply(false, true);
        }

        if (sharedSquareMat == null)
        {
            sharedSquareMat = CreateParticleMaterial("BlueBossWarning_SquareMat", sharedSquareTex);
        }

        if (sharedDiamondMat == null)
        {
            sharedDiamondMat = CreateParticleMaterial("BlueBossWarning_DiamondMat", sharedDiamondTex);
        }
    }

    private static Material CreateParticleMaterial(string matName, Texture2D tex)
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
}
