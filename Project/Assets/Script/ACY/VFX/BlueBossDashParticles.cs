using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 블루보스 돌진(일반 돌진·벽 튕김·3페이즈 발악 돌진)에 기존 TrailRenderer 리본 대신
/// 픽셀 아트 감성의 기류 잔상, 빙결/물보라 파편, 도트 미스트, 충격파 링 파티클을 생성·제어합니다.
/// </summary>
public class BlueBossDashParticles : MonoBehaviour
{
    private const string VfxRootName = "BlueBossDashVFX";

    private Transform vfxRoot;
    private Transform emitterPivot;
    private Collider2D bossCollider;
    private SpriteRenderer bossSprite;

    private ParticleSystem streakPS;
    private ParticleSystem shardPS;
    private ParticleSystem mistPS;
    private ParticleSystem ringPS;
    private readonly List<ParticleSystem> loopingSystems = new List<ParticleSystem>();

    private bool initialized;
    private bool isDashing;
    private Vector2 currentDashDir = Vector2.right;

    private static Texture2D sharedStreakTex;
    private static Texture2D sharedDiamondTex;
    private static Texture2D sharedPixelTex;
    private static Texture2D sharedRingTex;

    private static Material sharedStreakMat;
    private static Material sharedDiamondMat;
    private static Material sharedPixelMat;
    private static Material sharedRingMat;

    private static readonly Color IceWhite = new Color(0.94f, 0.99f, 1f, 1f);
    private static readonly Color BrightCyan = new Color(0.32f, 0.88f, 1f, 1f);
    private static readonly Color DeepCobalt = new Color(0.08f, 0.36f, 0.92f, 1f);

    public static BlueBossDashParticles Attach(GameObject target)
    {
        if (target == null) return null;

        RemoveLegacyTrail(target);

        BlueBossDashParticles comp = target.GetComponent<BlueBossDashParticles>();
        if (comp == null)
        {
            comp = target.AddComponent<BlueBossDashParticles>();
        }
        comp.EnsureCreated();
        return comp;
    }

    private static void RemoveLegacyTrail(GameObject target)
    {
        BossProjectileTrail legacyComp = target.GetComponent<BossProjectileTrail>();
        if (legacyComp != null)
        {
            Destroy(legacyComp);
        }

        Transform legacyChild = target.transform.Find("BossAttackTrailVFX");
        if (legacyChild != null)
        {
            Destroy(legacyChild.gameObject);
        }
    }

    public void StartDash(Vector2 dashDirection, bool isEnrage = false)
    {
        EnsureCreated();

        UpdateDirection(dashDirection);
        SyncTransforms();

        float emissionMult = isEnrage ? 1.4f : 1f;
        ApplyEmissionRates(emissionMult);

        for (int i = 0; i < loopingSystems.Count; i++)
        {
            ParticleSystem ps = loopingSystems[i];
            if (ps != null && !ps.isEmitting)
            {
                ps.Play(false);
            }
        }

        if (ringPS != null && !ringPS.isPlaying)
        {
            ringPS.Play(false);
        }

        // 돌진 시작 시 폭발적인 출발 버스트 + 충격 링
        if (streakPS != null) streakPS.Emit(isEnrage ? 22 : 14);
        if (shardPS != null) shardPS.Emit(isEnrage ? 22 : 14);
        if (mistPS != null) mistPS.Emit(isEnrage ? 28 : 18);
        if (ringPS != null) ringPS.Emit(isEnrage ? 2 : 1);

        isDashing = true;
    }

    /// <summary>
    /// 루프 방출을 켜지 않고 현재 보스/분신 위치에 1회성 빙결 파편·미스트·충격 링 버스트만 재생합니다.
    /// </summary>
    public void EmitBurstPulse(int shardCount = 16, int mistCount = 18, int ringCount = 1)
    {
        EnsureCreated();
        SyncTransforms();

        if (shardPS != null && shardCount > 0) shardPS.Emit(shardCount);
        if (mistPS != null && mistCount > 0) mistPS.Emit(mistCount);
        if (ringPS != null && ringCount > 0) ringPS.Emit(ringCount);
    }

    public void UpdateDirection(Vector2 dashDirection)
    {
        if (dashDirection.sqrMagnitude > 0.0001f)
        {
            currentDashDir = dashDirection.normalized;
        }

        if (emitterPivot != null)
        {
            float angleDeg = Mathf.Atan2(currentDashDir.y, currentDashDir.x) * Mathf.Rad2Deg;
            emitterPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }
    }

    public void OnWallBounce(Vector2 hitPoint, Vector2 wallNormal, Vector2 newDashDirection)
    {
        EnsureCreated();
        UpdateDirection(newDashDirection);
        SyncTransforms();

        Vector2 safeNormal = wallNormal.sqrMagnitude > 0.0001f ? wallNormal.normalized : -currentDashDir;
        Vector2 tangent = new Vector2(-safeNormal.y, safeNormal.x);

        if (ringPS != null)
        {
            var ringParams = new ParticleSystem.EmitParams
            {
                position = new Vector3(hitPoint.x, hitPoint.y, 0f),
                startSize = 2.6f,
                startLifetime = 0.28f
            };
            ringPS.Emit(ringParams, 1);
        }

        if (shardPS != null)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 burstVel = safeNormal * Random.Range(2.8f, 6.5f) + tangent * Random.Range(-4.2f, 4.2f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(hitPoint.x, hitPoint.y, 0f),
                    velocity = new Vector3(burstVel.x, burstVel.y, 0f),
                    applyShapeToPosition = false
                };
                shardPS.Emit(ep, 1);
            }
        }

        if (mistPS != null)
        {
            for (int i = 0; i < 14; i++)
            {
                Vector2 burstVel = safeNormal * Random.Range(1.5f, 4.4f) + tangent * Random.Range(-3.2f, 3.2f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = new Vector3(hitPoint.x, hitPoint.y, 0f),
                    velocity = new Vector3(burstVel.x, burstVel.y, 0f),
                    applyShapeToPosition = false
                };
                mistPS.Emit(ep, 1);
            }
        }
    }

    public void StopDash(bool clearImmediately = false)
    {
        isDashing = false;
        if (!initialized) return;

        ParticleSystemStopBehavior behavior = clearImmediately
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < loopingSystems.Count; i++)
        {
            if (loopingSystems[i] != null)
            {
                loopingSystems[i].Stop(true, behavior);
            }
        }

        if (ringPS != null)
        {
            ringPS.Stop(true, behavior);
        }
    }

    private void OnDisable()
    {
        StopDash(clearImmediately: true);
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        if (isDashing || AnySystemPlaying())
        {
            SyncTransforms();
        }
    }

    private bool AnySystemPlaying()
    {
        for (int i = 0; i < loopingSystems.Count; i++)
        {
            if (loopingSystems[i] != null && loopingSystems[i].isPlaying)
            {
                return true;
            }
        }
        return ringPS != null && ringPS.isPlaying;
    }

    private void EnsureCreated()
    {
        if (initialized) return;

        EnsureSharedAssets();

        bossCollider = GetComponent<Collider2D>();
        bossSprite = GetComponentInChildren<SpriteRenderer>();

        Transform existing = transform.Find(VfxRootName);
        GameObject rootGo;
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        rootGo = new GameObject(VfxRootName);
        rootGo.transform.SetParent(transform, false);
        vfxRoot = rootGo.transform;

        GameObject pivotGo = new GameObject("DashEmitterPivot");
        pivotGo.transform.SetParent(vfxRoot, false);
        emitterPivot = pivotGo.transform;

        streakPS = CreateStreakParticles();
        shardPS = CreateShardParticles();
        mistPS = CreateMistParticles();
        ringPS = CreateRingParticles();

        initialized = true;
        SyncTransforms();
        StopDash(clearImmediately: true);
    }

    private void SyncTransforms()
    {
        if (vfxRoot == null) return;

        // 블루보스 프리팹의 큰 스케일(9x) 및 좌우 반전(음수 스케일)을 상쇄해 월드 1:1 비율 유지
        Vector3 parentLossy = transform.lossyScale;
        vfxRoot.localScale = new Vector3(
            Mathf.Abs(parentLossy.x) > 0.001f ? 1f / parentLossy.x : 1f,
            Mathf.Abs(parentLossy.y) > 0.001f ? 1f / parentLossy.y : 1f,
            1f);
        vfxRoot.rotation = Quaternion.identity;

        Vector3 centerPos = transform.position;
        if (bossCollider != null && bossCollider.enabled)
        {
            centerPos = bossCollider.bounds.center;
        }
        else if (bossSprite != null)
        {
            centerPos = bossSprite.bounds.center;
        }
        vfxRoot.position = new Vector3(centerPos.x, centerPos.y, transform.position.z);

        if (emitterPivot != null)
        {
            float angleDeg = Mathf.Atan2(currentDashDir.y, currentDashDir.x) * Mathf.Rad2Deg;
            emitterPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        if (bossSprite != null)
        {
            int layerId = bossSprite.sortingLayerID;
            int baseOrder = bossSprite.sortingOrder;
            ApplySorting(streakPS, layerId, baseOrder - 1);
            ApplySorting(mistPS, layerId, baseOrder - 2);
            ApplySorting(ringPS, layerId, baseOrder - 2);
            ApplySorting(shardPS, layerId, baseOrder + 1);
        }
    }

    private void ApplyEmissionRates(float multiplier)
    {
        if (streakPS != null)
        {
            var em = streakPS.emission;
            em.rateOverDistance = 5.5f * multiplier;
            em.rateOverTime = 22f * multiplier;
        }
        if (shardPS != null)
        {
            var em = shardPS.emission;
            em.rateOverDistance = 4.5f * multiplier;
            em.rateOverTime = 16f * multiplier;
        }
        if (mistPS != null)
        {
            var em = mistPS.emission;
            em.rateOverDistance = 7.0f * multiplier;
            em.rateOverTime = 24f * multiplier;
        }
    }

    private ParticleSystem CreateStreakParticles()
    {
        // localRotation = (0, -90, 0) -> Cone의 +Z가 돌진 반대 방향(-dashDir)을 향함
        ParticleSystem ps = CreateStoppedSystem("DashSpeedStreaks", emitterPivot, Quaternion.Euler(0f, -90f, 0f), out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 9.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.58f);
        main.startColor = new ParticleSystem.MinMaxGradient(IceWhite, BrightCyan);
        main.maxParticles = 180;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverDistance = 5.5f;
        em.rateOverTime = 22f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 1.4f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(0.28f, 1f, 1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildTrailGradient(alphaPeak: 0.96f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0.05f)
        ));

        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 2.4f;
        r.velocityScale = 0.048f;
        r.sortingOrder = 249;
        r.sharedMaterial = sharedStreakMat;

        go.SetActive(true);
        loopingSystems.Add(ps);
        return ps;
    }

    private ParticleSystem CreateShardParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("DashIceShards", emitterPivot, Quaternion.Euler(0f, -90f, 0f), out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.44f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.28f;
        main.startColor = new ParticleSystem.MinMaxGradient(IceWhite, BrightCyan);
        main.maxParticles = 160;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverDistance = 4.5f;
        em.rateOverTime = 16f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 1.45f;
        shape.radiusThickness = 0.9f;
        shape.scale = new Vector3(0.35f, 1f, 1f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildTrailGradient(alphaPeak: 0.95f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(0.22f, 1.15f),
            new Keyframe(1f, 0f)
        ));

        ConfigureBillboardRenderer(ps, sharedDiamondMat, 251);
        go.SetActive(true);
        loopingSystems.Add(ps);
        return ps;
    }

    private ParticleSystem CreateMistParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("DashPixelMist", emitterPivot, Quaternion.Euler(0f, -90f, 0f), out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
        main.gravityModifier = -0.18f;
        main.startColor = new ParticleSystem.MinMaxGradient(BrightCyan, DeepCobalt);
        main.maxParticles = 200;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverDistance = 7.0f;
        em.rateOverTime = 24f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 36f;
        shape.radius = 1.3f;
        shape.radiusThickness = 1f;
        shape.scale = new Vector3(0.4f, 1f, 1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildTrailGradient(alphaPeak: 0.82f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.65f),
            new Keyframe(0.25f, 1.0f),
            new Keyframe(1f, 0f)
        ));

        ConfigureBillboardRenderer(ps, sharedPixelMat, 248);
        go.SetActive(true);
        loopingSystems.Add(ps);
        return ps;
    }

    private ParticleSystem CreateRingParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("DashPulseRing", vfxRoot, Quaternion.identity, out GameObject go);
        var main = ps.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.32f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 2.8f);
        main.startColor = IceWhite;
        main.maxParticles = 12;

        var em = ps.emission;
        em.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildTrailGradient(alphaPeak: 0.78f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(1f, 1.45f)
        ));

        ConfigureBillboardRenderer(ps, sharedRingMat, 248);
        go.SetActive(true);
        return ps;
    }

    private static ParticleSystem CreateStoppedSystem(string childName, Transform parent, Quaternion localRot, out GameObject go)
    {
        go = new GameObject(childName);
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.duration = 1f;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private static void ConfigureBillboardRenderer(ParticleSystem ps, Material mat, int fallbackOrder)
    {
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = fallbackOrder;
        if (mat != null)
        {
            r.sharedMaterial = mat;
        }
    }

    private static void ApplySorting(ParticleSystem ps, int layerId, int order)
    {
        if (ps == null) return;
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        if (r == null) return;
        r.sortingLayerID = layerId;
        r.sortingOrder = order;
    }

    private static ParticleSystem.MinMaxGradient BuildTrailGradient(float alphaPeak)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(IceWhite, 0f),
                new GradientColorKey(BrightCyan, 0.35f),
                new GradientColorKey(DeepCobalt, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alphaPeak, 0.14f),
                new GradientAlphaKey(alphaPeak * 0.55f, 0.58f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static void EnsureSharedAssets()
    {
        if (sharedStreakTex == null)
        {
            // 16x6 픽셀 속도선 텍스처 (양 끝이 얇아지는 계단형 픽셀 바)
            const int w = 16;
            const int h = 6;
            sharedStreakTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BlueBossDash_StreakTex"
            };
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool middleRows = y >= 2 && y <= 3;
                    bool innerRows = y >= 1 && y <= 4 && x >= 2 && x <= 13;
                    bool centerBright = middleRows && x >= 4 && x <= 12;
                    byte a = centerBright ? (byte)255 : (middleRows || innerRows ? (byte)195 : (byte)0);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            sharedStreakTex.SetPixels32(pixels);
            sharedStreakTex.Apply(false, true);
        }

        if (sharedDiamondTex == null)
        {
            const int size = 10;
            sharedDiamondTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BlueBossDash_DiamondTex"
            };
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    byte a = dist <= 2.2f ? (byte)255 : (dist <= 4.0f ? (byte)185 : (byte)0);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            sharedDiamondTex.SetPixels32(pixels);
            sharedDiamondTex.Apply(false, true);
        }

        if (sharedPixelTex == null)
        {
            const int size = 6;
            sharedPixelTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BlueBossDash_PixelTex"
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                    byte a = corner ? (byte)0 : (byte)255;
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            sharedPixelTex.SetPixels32(pixels);
            sharedPixelTex.Apply(false, true);
        }

        if (sharedRingTex == null)
        {
            const int size = 24;
            sharedRingTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "BlueBossDash_RingTex"
            };
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (r >= 7.8f && r <= 10.6f) ? (byte)245 : ((r >= 6.8f && r <= 11.4f) ? (byte)110 : (byte)0);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            sharedRingTex.SetPixels32(pixels);
            sharedRingTex.Apply(false, true);
        }

        if (sharedStreakMat == null) sharedStreakMat = CreateParticleMat("BlueBossDash_StreakMat", sharedStreakTex);
        if (sharedDiamondMat == null) sharedDiamondMat = CreateParticleMat("BlueBossDash_DiamondMat", sharedDiamondTex);
        if (sharedPixelMat == null) sharedPixelMat = CreateParticleMat("BlueBossDash_PixelMat", sharedPixelTex);
        if (sharedRingMat == null) sharedRingMat = CreateParticleMat("BlueBossDash_RingMat", sharedRingTex);
    }

    private static Material CreateParticleMat(string matName, Texture2D tex)
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
