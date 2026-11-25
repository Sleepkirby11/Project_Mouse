using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그린보스 바람 밀쳐내기 공격(BossWindBlast)에 기존 바람 스프라이트(W401)와 동일한 색상·픽셀 크기·속도의
/// 가로 바람선과 사각 픽셀 조각 파티클을 더해 바람 연출을 깔끔하고 풍성하게 보강합니다.
/// </summary>
public class Wind : MonoBehaviour
{
    [Header("바람 연출 설정")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float lifeTime = 1.5f;

    private const string POOL_KEY = "BossWindBlast";
    private float currentLifeTime;
    private bool particlesInitialized;

    private ParticleSystem dashPs;
    private ParticleSystem dotPs;
    private readonly List<ParticleSystem> allParticles = new List<ParticleSystem>();

    private static Texture2D sharedDashTex;
    private static Texture2D sharedSquareTex;
    private static Material sharedDashMat;
    private static Material sharedSquareMat;

    private static readonly Color WindWhite = Color.white;
    private static readonly Color WindGrey = new Color(170f / 255f, 165f / 255f, 171f / 255f, 1f);

    private void Awake()
    {
        RemoveLegacyTrailIfPresent();
        EnsureParticleSystems();
    }

    private void OnEnable()
    {
        currentLifeTime = lifeTime;

        if (transform.parent != null && transform.parent.GetComponent<PoolingManager>() != null)
        {
            return;
        }

        EnsureParticleSystems();
        PlayWindBurst();
    }

    private void OnDisable()
    {
        StopAndClearParticles();
    }

    private void Update()
    {
        transform.Translate(Vector3.left * speed * Time.deltaTime);

        currentLifeTime -= Time.deltaTime;
        if (currentLifeTime <= 0f)
        {
            if (PoolingManager.Instance != null)
            {
                PoolingManager.Instance.Return(POOL_KEY, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    private void RemoveLegacyTrailIfPresent()
    {
        BossProjectileTrail legacyComp = GetComponent<BossProjectileTrail>();
        if (legacyComp != null)
        {
            Destroy(legacyComp);
        }

        Transform legacyObj = transform.Find("BossAttackTrailVFX");
        if (legacyObj != null)
        {
            Destroy(legacyObj.gameObject);
        }
    }

    private void EnsureParticleSystems()
    {
        if (particlesInitialized)
        {
            return;
        }

        EnsureSharedAssets();

        dashPs = CreateWindDashParticles();
        dotPs = CreateWindDotParticles();

        particlesInitialized = true;
    }

    private void PlayWindBurst()
    {
        StopAndClearParticles();

        for (int i = 0; i < allParticles.Count; i++)
        {
            if (allParticles[i] != null)
            {
                allParticles[i].Play(false);
            }
        }

        if (dashPs != null) dashPs.Emit(6);
        if (dotPs != null) dotPs.Emit(6);
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

    private ParticleSystem CreateWindDashParticles()
    {
        // 기존 W401 스프라이트 영역(X: -5~+2, Y: -2~+2)과 동일한 위치·속도(Local)로 이동
        ParticleSystem ps = CreateStoppedSystem("WindPixelDashes", new Vector3(-0.8f, -0.2f, 0f), out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 2.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(WindWhite, WindGrey);
        main.maxParticles = 24;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 20f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4.5f, 3.6f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-5.5f, -3.0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildSnappyAlphaGradient();

        ConfigureBillboardRenderer(ps, sharedDashMat, 99);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateWindDotParticles()
    {
        ParticleSystem ps = CreateStoppedSystem("WindPixelDots", new Vector3(-0.8f, -0.2f, 0f), out GameObject go);
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.48f);
        main.startColor = new ParticleSystem.MinMaxGradient(WindWhite, WindGrey);
        main.maxParticles = 28;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(4.8f, 3.8f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-6.5f, -3.5f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = BuildSnappyAlphaGradient();

        ConfigureBillboardRenderer(ps, sharedSquareMat, 101);
        go.SetActive(true);
        allParticles.Add(ps);
        return ps;
    }

    private ParticleSystem CreateStoppedSystem(string objName, Vector3 localPos, out GameObject go)
    {
        go = new GameObject(objName);
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
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

    private static Gradient BuildSnappyAlphaGradient()
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return grad;
    }

    private static void EnsureSharedAssets()
    {
        if (sharedDashTex == null)
        {
            const int size = 16;
            sharedDashTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            // 16x16 정사각 텍스처 중앙에 W401 선 두께 비율과 동일한 3px 높이 가로 바람선 생성
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                bool inBarY = (y >= 7 && y <= 9);
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = inBarY ? Color.white : Color.clear;
                }
            }
            sharedDashTex.SetPixels(pixels);
            sharedDashTex.Apply(false, true);
        }

        if (sharedSquareTex == null)
        {
            sharedSquareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            sharedSquareTex.SetPixels(pixels);
            sharedSquareTex.Apply(false, true);
        }

        if (sharedDashMat == null) sharedDashMat = CreateParticleMaterial("Wind_PixelDashMat", sharedDashTex);
        if (sharedSquareMat == null) sharedSquareMat = CreateParticleMaterial("Wind_PixelSquareMat", sharedSquareTex);
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
