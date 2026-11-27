using System.Collections;
using UnityEngine;

/// <summary>
/// 발판/레버 연동 벽(Wall)의 짙은 보라색 반투명 마법 배리어 막과
/// 내부 마력 순환 파티클·주변 입자 파티클을 전담하는 VFX 컴포넌트입니다.
/// </summary>
public class MagicBarrierVFX : MonoBehaviour
{
    private const string VfxRootName = "MagicBarrierVFX";

    [SerializeField] private float collapseDuration = 0.32f;

    private SpriteRenderer baseSpriteRenderer;
    private Collider2D wallCollider;

    private Transform visualRoot;
    private SpriteRenderer membraneRenderer;
    private ParticleSystem fieldParticles;
    private ParticleSystem ambientMotes;

    private Color membraneBaseColor = Color.white;
    private bool isCollapsing;
    private bool visualBuilt;

    private static Texture2D sharedBarrierTex;
    private static Sprite sharedBarrierSprite;
    private static Texture2D sharedSoftOrbTex;
    private static Texture2D sharedDiamondTex;
    private static Material sharedSpriteMat;
    private static Material sharedSoftOrbParticleMat;
    private static Material sharedDiamondParticleMat;

    public static MagicBarrierVFX Attach(GameObject target)
    {
        if (target == null) return null;

        MagicBarrierVFX vfx = target.GetComponent<MagicBarrierVFX>();
        if (vfx == null)
        {
            vfx = target.AddComponent<MagicBarrierVFX>();
        }
        vfx.EnsureInitialized();
        return vfx;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        if (isCollapsing) return;

        EnsureInitialized();
        SetBarrierActiveState(true);
    }

    private void Update()
    {
        if (isCollapsing || membraneRenderer == null || !membraneRenderer.enabled)
        {
            return;
        }

        // 반투명 마법 막이 은은하게 일렁이는 숨결 효과
        float pulse = 0.90f + 0.10f * Mathf.Sin(Time.time * 2.6f);
        Color c = membraneBaseColor;
        c.a *= pulse;
        membraneRenderer.color = c;
    }

    public void EnsureInitialized()
    {
        if (baseSpriteRenderer == null)
        {
            baseSpriteRenderer = GetComponent<SpriteRenderer>();
        }
        if (wallCollider == null)
        {
            wallCollider = GetComponent<Collider2D>();
        }

        EnsureMagicBarrierVisuals();
    }

    public void OnRebuild()
    {
        isCollapsing = false;
        EnsureInitialized();
        if (wallCollider != null)
        {
            wallCollider.enabled = true;
        }
        SetBarrierActiveState(true);
    }

    public void PlayCollapseAndDestroy(GameObject targetObject)
    {
        if (isCollapsing) return;
        isCollapsing = true;

        if (wallCollider != null)
        {
            wallCollider.enabled = false;
        }

        if (!gameObject.activeInHierarchy)
        {
            Destroy(targetObject);
            return;
        }

        StartCoroutine(CollapseRoutine(targetObject));
    }

    private IEnumerator CollapseRoutine(GameObject targetObject)
    {
        if (fieldParticles != null)
        {
            fieldParticles.Emit(14);
            fieldParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (ambientMotes != null)
        {
            ambientMotes.Emit(18);
            ambientMotes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, collapseDuration);
        Color startColor = membraneRenderer != null ? membraneRenderer.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (membraneRenderer != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                membraneRenderer.color = c;
            }
            yield return null;
        }

        if (targetObject != null)
        {
            Destroy(targetObject);
        }
    }

    private void SetBarrierActiveState(bool active)
    {
        if (baseSpriteRenderer != null)
        {
            // 기존 검은 단색 스프라이트는 숨기고 반투명 마법 막 + 파티클로 대체
            baseSpriteRenderer.enabled = false;
        }

        if (membraneRenderer != null)
        {
            membraneRenderer.enabled = active;
            membraneRenderer.color = membraneBaseColor;
        }

        if (fieldParticles != null)
        {
            if (active) fieldParticles.Play(true);
            else fieldParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (ambientMotes != null)
        {
            if (active) ambientMotes.Play(true);
            else ambientMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void EnsureMagicBarrierVisuals()
    {
        if (visualBuilt) return;

        EnsureSharedAssets();

        Vector3 lossy = transform.lossyScale;
        float scaleX = Mathf.Max(0.05f, Mathf.Abs(lossy.x));
        float scaleY = Mathf.Max(0.05f, Mathf.Abs(lossy.y));

        Vector2 size = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (wallCollider is BoxCollider2D boxCol)
        {
            size = boxCol.size;
            offset = boxCol.offset;
        }

        float worldWidth = Mathf.Max(0.2f, Mathf.Abs(size.x) * scaleX);
        float worldHeight = Mathf.Max(0.5f, Mathf.Abs(size.y) * scaleY);

        int sortingLayerId = baseSpriteRenderer != null ? baseSpriteRenderer.sortingLayerID : 0;
        int sortingOrder = baseSpriteRenderer != null ? baseSpriteRenderer.sortingOrder : 20;

        // 부모 벽의 비균일 스케일(예: 2 x 10, 2 x 20)이 입자 모양을 찌그러뜨리지 않도록 역스케일 보정
        GameObject rootObj = new GameObject(VfxRootName);
        rootObj.layer = gameObject.layer;
        visualRoot = rootObj.transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = offset;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = new Vector3(1f / scaleX, 1f / scaleY, 1f);

        CreateMembraneLayer(worldWidth, worldHeight, sortingLayerId, sortingOrder);
        fieldParticles = CreateFieldParticles(worldWidth, worldHeight, sortingLayerId, sortingOrder + 1);
        ambientMotes = CreateAmbientMotes(worldWidth, worldHeight, sortingLayerId, sortingOrder + 2);

        if (baseSpriteRenderer != null)
        {
            baseSpriteRenderer.enabled = false;
        }

        visualBuilt = true;
    }

    private void CreateMembraneLayer(float worldWidth, float worldHeight, int sortingLayerId, int sortingOrder)
    {
        GameObject memObj = new GameObject("BarrierMembrane");
        memObj.layer = gameObject.layer;
        memObj.transform.SetParent(visualRoot, false);
        memObj.transform.localPosition = Vector3.zero;
        memObj.transform.localScale = Vector3.one;

        membraneRenderer = memObj.AddComponent<SpriteRenderer>();
        membraneRenderer.sprite = sharedBarrierSprite;
        membraneRenderer.drawMode = SpriteDrawMode.Sliced;
        membraneRenderer.size = new Vector2(worldWidth, worldHeight);
        membraneRenderer.sharedMaterial = sharedSpriteMat;
        membraneRenderer.sortingLayerID = sortingLayerId;
        membraneRenderer.sortingOrder = sortingOrder;
        membraneBaseColor = Color.white;
        membraneRenderer.color = membraneBaseColor;
    }

    private ParticleSystem CreateFieldParticles(float worldWidth, float worldHeight, int sortingLayerId, int sortingOrder)
    {
        GameObject go = new GameObject("BarrierFieldParticles");
        go.layer = gameObject.layer;
        go.transform.SetParent(visualRoot, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.56f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.32f, 0.10f, 0.64f, 0.48f),
            new Color(0.66f, 0.34f, 0.98f, 0.62f));
        main.maxParticles = 110;

        float span = Mathf.Max(worldWidth, worldHeight);
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(span * 2.6f, 16f, 60f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.2f, worldWidth * 0.85f), Mathf.Max(0.2f, worldHeight * 0.85f), 0.1f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.32f, 0.32f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.12f, 0.52f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 내부에서 마력이 소용돌이치며 흐르는 느낌을 주는 터뷸런스 노이즈
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.68f);
        noise.frequency = 0.85f;
        noise.scrollSpeed = 0.75f;
        noise.octaveCount = 2;
        noise.damping = true;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.36f, 0.12f, 0.72f), 0f),
                new GradientColorKey(new Color(0.72f, 0.44f, 1.00f), 0.5f),
                new GradientColorKey(new Color(0.26f, 0.08f, 0.54f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.46f, 0.30f),
                new GradientAlphaKey(0.38f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.45f, 1.05f),
            new Keyframe(1f, 0.35f)));

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sharedSoftOrbParticleMat;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;

        return ps;
    }

    private ParticleSystem CreateAmbientMotes(float worldWidth, float worldHeight, int sortingLayerId, int sortingOrder)
    {
        GameObject go = new GameObject("BarrierAmbientMotes");
        go.layer = gameObject.layer;
        go.transform.SetParent(visualRoot, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.30f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.16f, 0.85f, 0.85f),
            new Color(0.80f, 0.52f, 1.00f, 0.92f));
        main.maxParticles = 72;

        float span = Mathf.Max(worldWidth, worldHeight);
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(span * 1.5f, 10f, 36f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(worldWidth + 1.0f, worldHeight + 1.0f, 0.1f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.8f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 0.24f, 0.95f), 0f),
                new GradientColorKey(new Color(0.84f, 0.62f, 1.00f), 0.5f),
                new GradientColorKey(new Color(0.38f, 0.12f, 0.72f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0.65f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sharedDiamondParticleMat;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;

        return ps;
    }

    private static void EnsureSharedAssets()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (sharedBarrierTex == null)
        {
            const int size = 32;
            sharedBarrierTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int edgeY = Mathf.Min(y, size - 1 - y);
                for (int x = 0; x < size; x++)
                {
                    int edgeX = Mathf.Min(x, size - 1 - x);
                    int edgeDist = Mathf.Min(edgeX, edgeY);

                    Color c;
                    if (edgeDist == 0)
                    {
                        // 상하좌우 4면 외곽 발광 보라빛 경계선
                        c = new Color(0.76f, 0.52f, 1.00f, 0.86f);
                    }
                    else if (edgeDist == 1)
                    {
                        c = new Color(0.52f, 0.24f, 0.90f, 0.74f);
                    }
                    else if (edgeDist == 2)
                    {
                        c = new Color(0.34f, 0.11f, 0.66f, 0.60f);
                    }
                    else
                    {
                        // 안쪽은 가로/세로 방향에 무관하게 매끄러운 짙은 보라색 반투명 막
                        c = new Color(0.16f, 0.04f, 0.34f, 0.45f);
                    }

                    pixels[y * size + x] = c;
                }
            }

            sharedBarrierTex.SetPixels(pixels);
            sharedBarrierTex.Apply();
        }

        if (sharedBarrierSprite == null && sharedBarrierTex != null)
        {
            sharedBarrierSprite = Sprite.Create(
                sharedBarrierTex,
                new Rect(0, 0, sharedBarrierTex.width, sharedBarrierTex.height),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4f, 4f, 4f, 4f));
            sharedBarrierSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        if (sharedSoftOrbTex == null)
        {
            const int size = 9;
            sharedSoftOrbTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float half = (size - 1) * 0.5f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            sharedSoftOrbTex.SetPixels(pixels);
            sharedSoftOrbTex.Apply();
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
                    float a = dist <= center ? (dist <= 1 ? 1f : 0.7f) : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sharedDiamondTex.SetPixels(pixels);
            sharedDiamondTex.Apply();
        }

        if (shader != null)
        {
            if (sharedSpriteMat == null)
            {
                sharedSpriteMat = new Material(shader)
                {
                    name = "MagicBarrier_SpriteMat",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            if (sharedSoftOrbParticleMat == null)
            {
                sharedSoftOrbParticleMat = new Material(shader)
                {
                    name = "MagicBarrier_SoftOrbMat",
                    mainTexture = sharedSoftOrbTex,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            if (sharedDiamondParticleMat == null)
            {
                sharedDiamondParticleMat = new Material(shader)
                {
                    name = "MagicBarrier_DiamondMat",
                    mainTexture = sharedDiamondTex,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }
    }
}
