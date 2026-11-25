using UnityEngine;

/// <summary>
/// 보스 투사체 및 돌진 공격에 픽셀 감성의 테이퍼드 트레일 리본과 도트 파티클 잔상을 부여합니다.
/// 오브젝트 풀링 환경에서도 잔여 궤적이 남지 않도록 OnDisable 시 자동 정리됩니다.
/// </summary>
public class BossProjectileTrail : MonoBehaviour
{
    public enum TrailStyle
    {
        FireArrow,
        MagicOrb,
        Meteor,
        GreenBird,
        MantisSlash,
        BlueDash,
        IceWind,
        ElementalRed,
        ElementalGreen,
        ElementalBlue
    }

    private struct PresetConfig
    {
        public Color headColor;
        public Color midColor;
        public Color tailColor;
        public float trailTime;
        public float trailStartWidth;
        public float trailEndWidth;
        public float emitRate;
        public float minLifetime;
        public float maxLifetime;
        public float minSize;
        public float maxSize;
        public float minSpeed;
        public float maxSpeed;
        public float shapeRadius;
        public float gravityModifier;
        public bool useDiamond;
    }

    private Transform vfxRoot;
    private TrailRenderer trailRenderer;
    private ParticleSystem sparkParticle;
    private bool initialized = false;
    private bool hasConfiguredStyle = false;
    private TrailStyle currentStyle;
    private int currentSortingOrder = 99;

    private static Texture2D sharedSquareTex;
    private static Texture2D sharedDiamondTex;
    private static Material sharedSquareMat;
    private static Material sharedDiamondMat;
    private static Material sharedTrailMat;

    public static BossProjectileTrail Attach(GameObject target, TrailStyle style, int sortingOrder = 99)
    {
        if (target == null) return null;

        BossProjectileTrail trail = target.GetComponent<BossProjectileTrail>();
        if (trail == null)
        {
            trail = target.AddComponent<BossProjectileTrail>();
        }
        trail.Configure(style, sortingOrder);
        return trail;
    }

    public static TrailStyle StyleFromElement(EnemyStatus.EnemyElement element)
    {
        return element switch
        {
            EnemyStatus.EnemyElement.Red => TrailStyle.ElementalRed,
            EnemyStatus.EnemyElement.Green => TrailStyle.ElementalGreen,
            EnemyStatus.EnemyElement.Blue => TrailStyle.ElementalBlue,
            _ => TrailStyle.ElementalRed
        };
    }

    public void Configure(TrailStyle style, int sortingOrder = 99)
    {
        EnsureCreated();
        if (hasConfiguredStyle && currentStyle == style && currentSortingOrder == sortingOrder)
            return;

        currentStyle = style;
        currentSortingOrder = sortingOrder;
        hasConfiguredStyle = true;

        ApplyPreset(GetPreset(style), sortingOrder);
    }

    public void SetActive(bool emitTrail, bool emitParticles = true, bool clearBefore = false)
    {
        EnsureCreated();

        if (clearBefore)
        {
            Clear();
        }

        if (trailRenderer != null)
        {
            trailRenderer.emitting = emitTrail;
        }

        if (sparkParticle != null)
        {
            if (emitParticles)
            {
                if (!sparkParticle.isEmitting)
                {
                    sparkParticle.Play(false);
                }
            }
            else
            {
                sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    public void EmitBurst(int count)
    {
        if (count <= 0) return;
        EnsureCreated();

        if (sparkParticle != null)
        {
            sparkParticle.Emit(count);
        }
    }

    public void Clear()
    {
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
            trailRenderer.Clear();
        }

        if (sparkParticle != null)
        {
            sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void EnsureCreated()
    {
        if (initialized) return;

        EnsureSharedAssets();

        Transform existing = transform.Find("BossAttackTrailVFX");
        GameObject go;
        if (existing == null)
        {
            go = new GameObject("BossAttackTrailVFX");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            trailRenderer = go.AddComponent<TrailRenderer>();
            sparkParticle = go.AddComponent<ParticleSystem>();
        }
        else
        {
            go = existing.gameObject;
            trailRenderer = go.GetComponent<TrailRenderer>() ?? go.AddComponent<TrailRenderer>();
            sparkParticle = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
        }

        var main = sparkParticle.main;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;

        trailRenderer.emitting = false;
        trailRenderer.minVertexDistance = 0.08f;
        trailRenderer.numCornerVertices = 2;
        trailRenderer.numCapVertices = 2;
        trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        if (sharedTrailMat != null)
        {
            trailRenderer.sharedMaterial = sharedTrailMat;
        }

        go.SetActive(true);
        sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        trailRenderer.Clear();

        vfxRoot = go.transform;
        initialized = true;
    }

    private void ApplyPreset(PresetConfig p, int sortingOrder)
    {
        // TrailRenderer 리본 설정
        if (trailRenderer != null)
        {
            trailRenderer.sortingOrder = sortingOrder - 1;
            trailRenderer.time = p.trailTime;
            trailRenderer.startWidth = p.trailStartWidth;
            trailRenderer.endWidth = p.trailEndWidth;

            Gradient ribbonGrad = new Gradient();
            ribbonGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(p.headColor, 0f),
                    new GradientColorKey(p.midColor, 0.38f),
                    new GradientColorKey(p.tailColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.58f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            trailRenderer.colorGradient = ribbonGrad;
        }

        // ParticleSystem 도트 잔상 설정
        if (sparkParticle != null)
        {
            bool wasPlaying = sparkParticle.isPlaying;
            if (wasPlaying)
            {
                sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var main = sparkParticle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.minLifetime, p.maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.minSpeed, p.maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(p.minSize, p.maxSize);
            main.startRotation = p.useDiamond
                ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
                : new ParticleSystem.MinMaxCurve(0f, 0f);
            main.gravityModifier = p.gravityModifier;
            main.startColor = new ParticleSystem.MinMaxGradient(p.headColor, p.midColor);

            var emission = sparkParticle.emission;
            emission.enabled = true;
            emission.rateOverTime = p.emitRate;

            var shape = sparkParticle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = p.shapeRadius;
            shape.radiusThickness = 0.75f;

            var col = sparkParticle.colorOverLifetime;
            col.enabled = true;
            Gradient partGrad = new Gradient();
            partGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(p.headColor, 0f),
                    new GradientColorKey(p.midColor, 0.45f),
                    new GradientColorKey(p.tailColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = partGrad;

            var sol = sparkParticle.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.9f),
                new Keyframe(0.35f, 1f),
                new Keyframe(1f, 0f)
            ));

            var noise = sparkParticle.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 2.2f;
            noise.scrollSpeed = 1f;

            ParticleSystemRenderer pr = sparkParticle.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sortingOrder = sortingOrder;
                Material mat = p.useDiamond ? sharedDiamondMat : sharedSquareMat;
                if (mat != null)
                {
                    pr.sharedMaterial = mat;
                }
            }
        }
    }

    private static PresetConfig GetPreset(TrailStyle style)
    {
        switch (style)
        {
            case TrailStyle.FireArrow:
                return new PresetConfig
                {
                    headColor = new Color(1f, 0.92f, 0.65f, 1f),
                    midColor = new Color(1f, 0.4f, 0.14f, 1f),
                    tailColor = new Color(0.72f, 0.06f, 0.1f, 1f),
                    trailTime = 0.2f,
                    trailStartWidth = 0.3f,
                    trailEndWidth = 0.02f,
                    emitRate = 28f,
                    minLifetime = 0.18f,
                    maxLifetime = 0.34f,
                    minSize = 0.12f,
                    maxSize = 0.24f,
                    minSpeed = 0.3f,
                    maxSpeed = 1.1f,
                    shapeRadius = 0.14f,
                    gravityModifier = -0.2f,
                    useDiamond = true
                };

            case TrailStyle.MagicOrb:
                return new PresetConfig
                {
                    headColor = new Color(1f, 0.85f, 0.92f, 1f),
                    midColor = new Color(0.98f, 0.22f, 0.45f, 1f),
                    tailColor = new Color(0.55f, 0.05f, 0.35f, 1f),
                    trailTime = 0.24f,
                    trailStartWidth = 0.34f,
                    trailEndWidth = 0.03f,
                    emitRate = 24f,
                    minLifetime = 0.22f,
                    maxLifetime = 0.4f,
                    minSize = 0.14f,
                    maxSize = 0.28f,
                    minSpeed = 0.25f,
                    maxSpeed = 0.9f,
                    shapeRadius = 0.22f,
                    gravityModifier = 0f,
                    useDiamond = true
                };

            case TrailStyle.Meteor:
                return new PresetConfig
                {
                    headColor = new Color(1f, 0.95f, 0.74f, 1f),
                    midColor = new Color(1f, 0.38f, 0.1f, 1f),
                    tailColor = new Color(0.58f, 0.06f, 0.08f, 1f),
                    trailTime = 0.32f,
                    trailStartWidth = 0.78f,
                    trailEndWidth = 0.06f,
                    emitRate = 48f,
                    minLifetime = 0.28f,
                    maxLifetime = 0.55f,
                    minSize = 0.18f,
                    maxSize = 0.38f,
                    minSpeed = 0.4f,
                    maxSpeed = 1.6f,
                    shapeRadius = 0.36f,
                    gravityModifier = -0.35f,
                    useDiamond = false
                };

            case TrailStyle.GreenBird:
                return new PresetConfig
                {
                    headColor = new Color(0.84f, 1f, 0.82f, 1f),
                    midColor = new Color(0.24f, 0.92f, 0.42f, 1f),
                    tailColor = new Color(0.06f, 0.48f, 0.2f, 1f),
                    trailTime = 0.22f,
                    trailStartWidth = 0.38f,
                    trailEndWidth = 0.03f,
                    emitRate = 26f,
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSize = 0.14f,
                    maxSize = 0.28f,
                    minSpeed = 0.4f,
                    maxSpeed = 1.2f,
                    shapeRadius = 0.25f,
                    gravityModifier = -0.1f,
                    useDiamond = true
                };

            case TrailStyle.MantisSlash:
                return new PresetConfig
                {
                    headColor = new Color(0.88f, 1f, 0.72f, 1f),
                    midColor = new Color(0.28f, 0.95f, 0.35f, 1f),
                    tailColor = new Color(0.04f, 0.52f, 0.22f, 1f),
                    trailTime = 0.16f,
                    trailStartWidth = 0.44f,
                    trailEndWidth = 0.04f,
                    emitRate = 34f,
                    minLifetime = 0.16f,
                    maxLifetime = 0.32f,
                    minSize = 0.15f,
                    maxSize = 0.3f,
                    minSpeed = 0.5f,
                    maxSpeed = 1.5f,
                    shapeRadius = 0.2f,
                    gravityModifier = 0f,
                    useDiamond = true
                };

            case TrailStyle.BlueDash:
                return new PresetConfig
                {
                    headColor = new Color(0.86f, 0.98f, 1f, 1f),
                    midColor = new Color(0.24f, 0.78f, 1f, 1f),
                    tailColor = new Color(0.08f, 0.32f, 0.85f, 1f),
                    trailTime = 0.24f,
                    trailStartWidth = 0.95f,
                    trailEndWidth = 0.08f,
                    emitRate = 45f,
                    minLifetime = 0.24f,
                    maxLifetime = 0.48f,
                    minSize = 0.18f,
                    maxSize = 0.36f,
                    minSpeed = 0.6f,
                    maxSpeed = 2.0f,
                    shapeRadius = 0.55f,
                    gravityModifier = -0.15f,
                    useDiamond = true
                };

            case TrailStyle.IceWind:
                return new PresetConfig
                {
                    headColor = new Color(0.88f, 0.98f, 1f, 1f),
                    midColor = new Color(0.38f, 0.85f, 1f, 1f),
                    tailColor = new Color(0.12f, 0.45f, 0.9f, 1f),
                    trailTime = 0.22f,
                    trailStartWidth = 0.38f,
                    trailEndWidth = 0.03f,
                    emitRate = 24f,
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSize = 0.14f,
                    maxSize = 0.28f,
                    minSpeed = 0.4f,
                    maxSpeed = 1.2f,
                    shapeRadius = 0.3f,
                    gravityModifier = -0.1f,
                    useDiamond = false
                };

            case TrailStyle.ElementalRed:
                return new PresetConfig
                {
                    headColor = new Color(1f, 0.86f, 0.72f, 1f),
                    midColor = new Color(1f, 0.22f, 0.22f, 1f),
                    tailColor = new Color(0.62f, 0.04f, 0.14f, 1f),
                    trailTime = 0.22f,
                    trailStartWidth = 0.42f,
                    trailEndWidth = 0.03f,
                    emitRate = 28f,
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSize = 0.15f,
                    maxSize = 0.3f,
                    minSpeed = 0.4f,
                    maxSpeed = 1.3f,
                    shapeRadius = 0.28f,
                    gravityModifier = -0.12f,
                    useDiamond = true
                };

            case TrailStyle.ElementalGreen:
                return new PresetConfig
                {
                    headColor = new Color(0.82f, 1f, 0.86f, 1f),
                    midColor = new Color(0.18f, 0.92f, 0.4f, 1f),
                    tailColor = new Color(0.04f, 0.5f, 0.2f, 1f),
                    trailTime = 0.22f,
                    trailStartWidth = 0.42f,
                    trailEndWidth = 0.03f,
                    emitRate = 28f,
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSize = 0.15f,
                    maxSize = 0.3f,
                    minSpeed = 0.4f,
                    maxSpeed = 1.3f,
                    shapeRadius = 0.28f,
                    gravityModifier = -0.12f,
                    useDiamond = true
                };

            case TrailStyle.ElementalBlue:
            default:
                return new PresetConfig
                {
                    headColor = new Color(0.82f, 0.94f, 1f, 1f),
                    midColor = new Color(0.22f, 0.62f, 1f, 1f),
                    tailColor = new Color(0.06f, 0.24f, 0.78f, 1f),
                    trailTime = 0.22f,
                    trailStartWidth = 0.42f,
                    trailEndWidth = 0.03f,
                    emitRate = 28f,
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSize = 0.15f,
                    maxSize = 0.3f,
                    minSpeed = 0.4f,
                    maxSpeed = 1.3f,
                    shapeRadius = 0.28f,
                    gravityModifier = -0.12f,
                    useDiamond = true
                };
        }
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
            sharedSquareTex.Apply();
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
                    float a = dist <= center ? (dist <= 1 ? 1f : 0.8f) : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sharedDiamondTex.SetPixels(pixels);
            sharedDiamondTex.Apply();
        }

        if (sharedTrailMat == null)
        {
            sharedTrailMat = CreateMaterial("BossProjectileTrail_RibbonMat", sharedSquareTex);
        }
        if (sharedSquareMat == null)
        {
            sharedSquareMat = CreateMaterial("BossProjectileTrail_SquareMat", sharedSquareTex);
        }
        if (sharedDiamondMat == null)
        {
            sharedDiamondMat = CreateMaterial("BossProjectileTrail_DiamondMat", sharedDiamondTex);
        }
    }

    private static Material CreateMaterial(string matName, Texture2D tex)
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
