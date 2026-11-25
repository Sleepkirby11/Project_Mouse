using UnityEngine;

/// <summary>
/// 파란색 스킬 등으로 몬스터가 스턴(감전) 상태가 되었을 때 몬스터 몸체 주변에 픽셀 아트풍 감전 파티클(전기 스파크, 번개 아크, 펄스 링)을 재생합니다.
/// </summary>
public class MonsterShockParticles : MonoBehaviour
{
    private const string ChildObjectName = "MonsterShockVFX";

    private ParticleSystem boltPS;
    private ParticleSystem sparkPS;
    private ParticleSystem ringPS;
    private bool configured;

    private EnemyStatus ownerEnemy;
    private Collider2D cachedCollider;
    private SpriteRenderer cachedSpriteRenderer;
    private float cachedRadius = 0.6f;

    private static Texture2D sharedBoltTex;
    private static Texture2D sharedSparkTex;
    private static Texture2D sharedRingTex;
    private static Material sharedBoltMat;
    private static Material sharedSparkMat;
    private static Material sharedRingMat;

    public static MonsterShockParticles GetOrCreate(EnemyStatus enemy)
    {
        if (enemy == null) return null;

        Transform existing = enemy.transform.Find(ChildObjectName);
        if (existing != null)
        {
            MonsterShockParticles existingComp = existing.GetComponent<MonsterShockParticles>();
            if (existingComp != null)
            {
                existingComp.Initialize(enemy);
                return existingComp;
            }
        }

        GameObject go = new GameObject(ChildObjectName);
        go.transform.SetParent(enemy.transform, false);
        MonsterShockParticles comp = go.AddComponent<MonsterShockParticles>();
        comp.Initialize(enemy);
        return comp;
    }

    public void Initialize(EnemyStatus enemy)
    {
        ownerEnemy = enemy;
        if (enemy != null)
        {
            cachedCollider = enemy.GetComponent<Collider2D>();
            cachedSpriteRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
        }

        if (!configured)
        {
            Configure();
        }

        SyncTransformAndSorting();
    }

    public void PlayShock()
    {
        if (!configured) Configure();
        SyncTransformAndSorting();

        if (boltPS != null)
        {
            if (!boltPS.isEmitting) boltPS.Play(false);
            boltPS.Emit(5);
        }
        if (sparkPS != null)
        {
            if (!sparkPS.isEmitting) sparkPS.Play(false);
            sparkPS.Emit(12);
        }
        if (ringPS != null)
        {
            if (!ringPS.isEmitting) ringPS.Play(false);
            ringPS.Emit(1);
        }
    }

    public void StopShock(bool clearImmediately)
    {
        ParticleSystemStopBehavior behavior = clearImmediately
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        if (boltPS != null) boltPS.Stop(true, behavior);
        if (sparkPS != null) sparkPS.Stop(true, behavior);
        if (ringPS != null) ringPS.Stop(true, behavior);
    }

    private void LateUpdate()
    {
        if (ownerEnemy == null) return;
        if ((boltPS != null && boltPS.isPlaying) ||
            (sparkPS != null && sparkPS.isPlaying) ||
            (ringPS != null && ringPS.isPlaying))
        {
            SyncTransformAndSorting();
        }
    }

    private void SyncTransformAndSorting()
    {
        if (ownerEnemy == null) return;

        Bounds b = ResolveEnemyBounds();
        transform.position = b.center;

        float extent = Mathf.Max(b.extents.x, b.extents.y);
        float radius = Mathf.Clamp(extent * 0.72f, 0.35f, 1.35f);
        cachedRadius = radius;

        // 부모 스케일이 음수(좌우 반전)거나 커도 파티클 크기가 왜곡되지 않도록 월드 기준 단위 스케일 보정
        Vector3 parentLossy = ownerEnemy.transform.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Abs(parentLossy.x) > 0.001f ? 1f / parentLossy.x : 1f,
            Mathf.Abs(parentLossy.y) > 0.001f ? 1f / parentLossy.y : 1f,
            1f);

        if (boltPS != null)
        {
            var shape = boltPS.shape;
            shape.radius = radius * 0.9f;
        }
        if (sparkPS != null)
        {
            var shape = sparkPS.shape;
            shape.radius = radius * 0.65f;
        }

        if (cachedSpriteRenderer != null)
        {
            int layerId = cachedSpriteRenderer.sortingLayerID;
            int baseOrder = cachedSpriteRenderer.sortingOrder;
            ApplySorting(boltPS, layerId, baseOrder + 5);
            ApplySorting(sparkPS, layerId, baseOrder + 6);
            ApplySorting(ringPS, layerId, baseOrder + 4);
        }
    }

    private Bounds ResolveEnemyBounds()
    {
        if (cachedCollider != null && cachedCollider.enabled)
        {
            return cachedCollider.bounds;
        }
        if (cachedSpriteRenderer != null)
        {
            return cachedSpriteRenderer.bounds;
        }
        return new Bounds(ownerEnemy.transform.position + Vector3.up * 0.5f, Vector3.one);
    }

    private void Configure()
    {
        EnsureAssets();

        // 1. 픽셀 번개 아크 (몸체 주변에서 빠르게 점멸)
        boltPS = CreateChildSystem("ShockBolts");
        var boltMain = boltPS.main;
        boltMain.stopAction = ParticleSystemStopAction.None;
        boltMain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        boltMain.duration = 1f;
        boltMain.loop = true;
        boltMain.playOnAwake = false;
        boltMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        boltMain.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        boltMain.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        boltMain.startSize = new ParticleSystem.MinMaxCurve(0.42f, 0.72f);
        boltMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        boltMain.startColor = Color.white;
        boltMain.maxParticles = 40;

        var boltEmission = boltPS.emission;
        boltEmission.enabled = true;
        boltEmission.rateOverTime = 26f;

        var boltShape = boltPS.shape;
        boltShape.enabled = true;
        boltShape.shapeType = ParticleSystemShapeType.Circle;
        boltShape.radius = cachedRadius * 0.9f;
        boltShape.radiusThickness = 0.85f;

        var boltColor = boltPS.colorOverLifetime;
        boltColor.enabled = true;
        boltColor.color = BuildElectricGradient(alphaPeak: 1f);

        var boltSol = boltPS.sizeOverLifetime;
        boltSol.enabled = true;
        boltSol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.75f),
            new Keyframe(0.25f, 1.15f),
            new Keyframe(1f, 0.25f)
        ));

        SetupRenderer(boltPS, sharedBoltMat, 105);

        // 2. 튀어오르는 전기 십자 스파크
        sparkPS = CreateChildSystem("ShockSparks");
        var sparkMain = sparkPS.main;
        sparkMain.stopAction = ParticleSystemStopAction.None;
        sparkMain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        sparkMain.duration = 1f;
        sparkMain.loop = true;
        sparkMain.playOnAwake = false;
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.0f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        sparkMain.startColor = Color.white;
        sparkMain.maxParticles = 60;

        var sparkEmission = sparkPS.emission;
        sparkEmission.enabled = true;
        sparkEmission.rateOverTime = 22f;

        var sparkShape = sparkPS.shape;
        sparkShape.enabled = true;
        sparkShape.shapeType = ParticleSystemShapeType.Circle;
        sparkShape.radius = cachedRadius * 0.65f;
        sparkShape.radiusThickness = 0.6f;

        var sparkColor = sparkPS.colorOverLifetime;
        sparkColor.enabled = true;
        sparkColor.color = BuildElectricGradient(alphaPeak: 0.95f);

        var sparkSol = sparkPS.sizeOverLifetime;
        sparkSol.enabled = true;
        sparkSol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.9f),
            new Keyframe(0.2f, 1.1f),
            new Keyframe(1f, 0f)
        ));

        SetupRenderer(sparkPS, sharedSparkMat, 106);

        // 3. 감전 파동 링 (몬스터 중심에서 간헐적으로 펄스)
        ringPS = CreateChildSystem("ShockPulseRing");
        var ringMain = ringPS.main;
        ringMain.stopAction = ParticleSystemStopAction.None;
        ringMain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        ringMain.duration = 1f;
        ringMain.loop = true;
        ringMain.playOnAwake = false;
        ringMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        ringMain.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.28f);
        ringMain.startSpeed = 0f;
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.85f, 1.15f);
        ringMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ringMain.startColor = Color.white;
        ringMain.maxParticles = 8;

        var ringEmission = ringPS.emission;
        ringEmission.enabled = true;
        ringEmission.rateOverTime = 5.5f;

        var ringShape = ringPS.shape;
        ringShape.enabled = false;

        var ringRot = ringPS.rotationOverLifetime;
        ringRot.enabled = true;
        ringRot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

        var ringColor = ringPS.colorOverLifetime;
        ringColor.enabled = true;
        ringColor.color = BuildElectricGradient(alphaPeak: 0.65f);

        var ringSol = ringPS.sizeOverLifetime;
        ringSol.enabled = true;
        ringSol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(1f, 1.25f)
        ));

        SetupRenderer(ringPS, sharedRingMat, 104);

        StopShock(clearImmediately: true);
        configured = true;
    }

    private ParticleSystem CreateChildSystem(string childName)
    {
        GameObject child = new GameObject(childName);
        child.SetActive(false);
        child.transform.SetParent(transform, false);
        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        child.SetActive(true);
        return ps;
    }

    private static void SetupRenderer(ParticleSystem ps, Material mat, int fallbackOrder)
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

    private static Gradient BuildElectricGradient(float alphaPeak)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.95f, 0.99f, 1f), 0f),
                new GradientColorKey(new Color(0.32f, 0.90f, 1f), 0.38f),
                new GradientColorKey(new Color(0.10f, 0.45f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(alphaPeak, 0f),
                new GradientAlphaKey(alphaPeak * 0.9f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return grad;
    }

    private static void EnsureAssets()
    {
        if (sharedBoltTex == null)
        {
            sharedBoltTex = CreateBoltTexture();
        }
        if (sharedSparkTex == null)
        {
            sharedSparkTex = CreateSparkTexture();
        }
        if (sharedRingTex == null)
        {
            sharedRingTex = CreateRingTexture();
        }

        if (sharedBoltMat == null)
        {
            sharedBoltMat = CreateUnlitParticleMaterial("MonsterShockBoltMat", sharedBoltTex);
        }
        if (sharedSparkMat == null)
        {
            sharedSparkMat = CreateUnlitParticleMaterial("MonsterShockSparkMat", sharedSparkTex);
        }
        if (sharedRingMat == null)
        {
            sharedRingMat = CreateUnlitParticleMaterial("MonsterShockRingMat", sharedRingTex);
        }
    }

    private static Texture2D CreateBoltTexture()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];

        // 지그재그 픽셀 번개 경로 그리기
        Vector2Int[] segments =
        {
            new Vector2Int(10, 14),
            new Vector2Int(6, 9),
            new Vector2Int(11, 8),
            new Vector2Int(5, 2)
        };

        for (int s = 0; s < segments.Length - 1; s++)
        {
            DrawPixelLine(pixels, size, segments[s], segments[s + 1], thickness: 1);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateSparkTexture()
    {
        const int size = 9;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        int c = size / 2;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int ax = Mathf.Abs(x - c);
                int ay = Mathf.Abs(y - c);
                bool isCross = (ax == 0 && ay <= 4) || (ay == 0 && ax <= 4) || (ax <= 1 && ay <= 1);
                pixels[y * size + x] = new Color(1f, 1f, 1f, isCross ? 1f : 0f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateRingTexture()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                // 끊어진 전기 고리 느낌을 위해 각도에 따라 일부 구간 비우기
                bool gap = Mathf.Sin(angle * 4f) < -0.55f;
                bool isRing = !gap && dist >= 5.2f && dist <= 6.8f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, isRing ? 1f : 0f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static void DrawPixelLine(Color[] pixels, int size, Vector2Int a, Vector2Int b, int thickness)
    {
        int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        if (steps <= 0) return;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int px = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
            int py = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));

            for (int ox = 0; ox <= thickness; ox++)
            {
                for (int oy = 0; oy <= thickness; oy++)
                {
                    int x = px + ox;
                    int y = py + oy;
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
    }

    private static Material CreateUnlitParticleMaterial(string matName, Texture2D tex)
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
