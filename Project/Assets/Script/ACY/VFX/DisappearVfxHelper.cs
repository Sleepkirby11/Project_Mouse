using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스 투사체·기믹이 공격당하거나 상성 원소에 카운터당해 소멸할 때
/// 상황에 맞는 픽셀 파티클(화염·증기·얼음 파편·연기/재 등)을 월드 공간에서 독립 재생하는 경량 풀링 헬퍼입니다.
/// </summary>
public static class DisappearVfxHelper
{
    public enum Style
    {
        FireArrowBreak,
        MagicOrbDissipate,
        RgbRedCounteredByBlue,
        RgbBlueCounteredByGreen,
        RgbGreenCounteredByRed,
        RgbExpireRed,
        RgbExpireBlue,
        RgbExpireGreen,
        IceHammerShatterByGreen,
        MushroomBurnByRed
    }

    private enum TexShape
    {
        Square,
        Diamond,
        Puff,
        Shard
    }

    private struct LayerPreset
    {
        public TexShape shape;
        public Color startColorA;
        public Color startColorB;
        public Color endColor;
        public float minLifetime;
        public float maxLifetime;
        public float minSpeed;
        public float maxSpeed;
        public float minSize;
        public float maxSize;
        public float radius;
        public float gravityModifier;
        public float upwardBias;
        public float noiseStrength;
        public bool randomRotation;
        public int burstCount;
        public int sortingOrder;
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

    private sealed class VfxInstance : MonoBehaviour
    {
        private ParticleSystem primaryPs;
        private ParticleSystem secondaryPs;
        private Coroutine returnRoutine;

        public void EnsureBuilt()
        {
            if (primaryPs != null && secondaryPs != null)
            {
                return;
            }

            primaryPs = CreateChildPs("PrimaryBurst");
            secondaryPs = CreateChildPs("SecondaryBurst");
        }

        public void Play(LayerPreset primary, LayerPreset secondary)
        {
            EnsureBuilt();

            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }

            ApplyLayer(primaryPs, primary);
            ApplyLayer(secondaryPs, secondary);

            if (primary.burstCount > 0)
            {
                primaryPs.Play(false);
                primaryPs.Emit(primary.burstCount);
            }
            if (secondary.burstCount > 0)
            {
                secondaryPs.Play(false);
                secondaryPs.Emit(secondary.burstCount);
            }

            float waitTime = Mathf.Max(primary.maxLifetime, secondary.maxLifetime) + 0.1f;
            returnRoutine = StartCoroutine(ReturnAfterDelay(waitTime));
        }

        public void ForceReturnNow()
        {
            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }
            ClearSystems();
            ReleaseInstance(this);
        }

        private IEnumerator ReturnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            returnRoutine = null;
            ClearSystems();
            ReleaseInstance(this);
        }

        private void ClearSystems()
        {
            if (primaryPs != null) primaryPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (secondaryPs != null) secondaryPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private ParticleSystem CreateChildPs(string childName)
        {
            GameObject go = new GameObject(childName);
            go.SetActive(false);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            go.SetActive(true);
            return ps;
        }

        private static void ApplyLayer(ParticleSystem ps, LayerPreset p)
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (p.burstCount <= 0)
            {
                return;
            }

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.minLifetime, p.maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.minSpeed, p.maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(p.minSize, p.maxSize);
            main.startRotation = p.randomRotation
                ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
                : new ParticleSystem.MinMaxCurve(0f, 0f);
            main.gravityModifier = p.gravityModifier;
            main.startColor = new ParticleSystem.MinMaxGradient(p.startColorA, p.startColorB);
            main.maxParticles = Mathf.Max(32, p.burstCount + 8);

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.05f, p.radius);
            shape.radiusThickness = 1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = Mathf.Abs(p.upwardBias) > 0.001f;
            if (vel.enabled)
            {
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(p.upwardBias * 0.6f, p.upwardBias * 1.2f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            var noise = ps.noise;
            noise.enabled = p.noiseStrength > 0.01f;
            if (noise.enabled)
            {
                noise.strength = p.noiseStrength;
                noise.frequency = 2.0f;
                noise.scrollSpeed = 1.2f;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Color mid = Color.Lerp(p.startColorA, p.startColorB, 0.5f);
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(p.startColorA, 0f),
                    new GradientColorKey(mid, 0.4f),
                    new GradientColorKey(p.endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(p.startColorA.a, 0f),
                    new GradientAlphaKey(mid.a * 0.9f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.95f),
                new Keyframe(0.3f, 1.05f),
                new Keyframe(1f, 0.1f)
            ));

            ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sortingOrder = p.sortingOrder;
                Material mat = GetMaterial(p.shape);
                if (mat != null) pr.sharedMaterial = mat;
            }
        }
    }

    private static PoolHost host;
    private static readonly Queue<VfxInstance> pool = new Queue<VfxInstance>();
    private static readonly List<VfxInstance> activeInstances = new List<VfxInstance>();

    private static Texture2D squareTex;
    private static Texture2D diamondTex;
    private static Texture2D puffTex;
    private static Texture2D shardTex;
    private static Material squareMat;
    private static Material diamondMat;
    private static Material puffMat;
    private static Material shardMat;

    public static void Spawn(Style style, Vector3 worldPosition)
    {
        EnsureHostAndAssets();
        GetPresets(style, out LayerPreset primary, out LayerPreset secondary);

        VfxInstance inst = AcquireInstance();
        if (inst == null) return;

        inst.transform.position = worldPosition;
        inst.gameObject.SetActive(true);
        activeInstances.Add(inst);
        inst.Play(primary, secondary);
    }

    private static void EnsureHostAndAssets()
    {
        if (host == null)
        {
            GameObject hostGo = new GameObject("DisappearVfxPoolHost");
            Object.DontDestroyOnLoad(hostGo);
            host = hostGo.AddComponent<PoolHost>();
        }

        if (squareTex == null) squareTex = CreateSquareTexture();
        if (diamondTex == null) diamondTex = CreateDiamondTexture();
        if (puffTex == null) puffTex = CreatePuffTexture();
        if (shardTex == null) shardTex = CreateShardTexture();

        if (squareMat == null) squareMat = CreateMat("DisappearVfx_SquareMat", squareTex);
        if (diamondMat == null) diamondMat = CreateMat("DisappearVfx_DiamondMat", diamondTex);
        if (puffMat == null) puffMat = CreateMat("DisappearVfx_PuffMat", puffTex);
        if (shardMat == null) shardMat = CreateMat("DisappearVfx_ShardMat", shardTex);
    }

    private static VfxInstance AcquireInstance()
    {
        while (pool.Count > 0)
        {
            VfxInstance pooled = pool.Dequeue();
            if (pooled != null)
            {
                return pooled;
            }
        }

        if (host == null) return null;

        GameObject go = new GameObject("DisappearVfxInstance");
        go.SetActive(false);
        go.transform.SetParent(host.transform, false);
        VfxInstance inst = go.AddComponent<VfxInstance>();
        inst.EnsureBuilt();
        return inst;
    }

    private static void ReleaseInstance(VfxInstance inst)
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
            VfxInstance inst = activeInstances[i];
            if (inst != null)
            {
                inst.ForceReturnNow();
            }
        }
        activeInstances.Clear();
    }

    private static Material GetMaterial(TexShape shape)
    {
        return shape switch
        {
            TexShape.Square => squareMat,
            TexShape.Diamond => diamondMat,
            TexShape.Puff => puffMat,
            TexShape.Shard => shardMat,
            _ => squareMat
        };
    }

    private static void GetPresets(Style style, out LayerPreset primary, out LayerPreset secondary)
    {
        switch (style)
        {
            case Style.FireArrowBreak:
                // 불화살 격추/폭발: 튀는 화염 불티 + 위로 피어오르는 재와 연기
                primary = new LayerPreset
                {
                    shape = TexShape.Diamond,
                    startColorA = new Color(1f, 0.94f, 0.65f, 1f),
                    startColorB = new Color(1f, 0.36f, 0.10f, 1f),
                    endColor = new Color(0.70f, 0.08f, 0.08f, 0f),
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSpeed = 3.2f,
                    maxSpeed = 8.0f,
                    minSize = 0.16f,
                    maxSize = 0.34f,
                    radius = 0.22f,
                    gravityModifier = 0.35f,
                    upwardBias = 0.8f,
                    noiseStrength = 0.6f,
                    randomRotation = true,
                    burstCount = 20,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Puff,
                    startColorA = new Color(0.95f, 0.45f, 0.18f, 0.72f),
                    startColorB = new Color(0.32f, 0.26f, 0.28f, 0.72f),
                    endColor = new Color(0.16f, 0.15f, 0.17f, 0f),
                    minLifetime = 0.32f,
                    maxLifetime = 0.56f,
                    minSpeed = 0.8f,
                    maxSpeed = 2.6f,
                    minSize = 0.32f,
                    maxSize = 0.62f,
                    radius = 0.25f,
                    gravityModifier = -0.35f,
                    upwardBias = 1.6f,
                    noiseStrength = 0.8f,
                    randomRotation = false,
                    burstCount = 12,
                    sortingOrder = 104
                };
                break;

            case Style.MagicOrbDissipate:
                primary = new LayerPreset
                {
                    shape = TexShape.Diamond,
                    startColorA = new Color(1f, 0.86f, 0.92f, 1f),
                    startColorB = new Color(0.98f, 0.22f, 0.45f, 1f),
                    endColor = new Color(0.55f, 0.05f, 0.35f, 0f),
                    minLifetime = 0.20f,
                    maxLifetime = 0.38f,
                    minSpeed = 2.4f,
                    maxSpeed = 6.2f,
                    minSize = 0.16f,
                    maxSize = 0.32f,
                    radius = 0.22f,
                    gravityModifier = 0f,
                    upwardBias = 0f,
                    noiseStrength = 0.5f,
                    randomRotation = true,
                    burstCount = 18,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Puff,
                    startColorA = new Color(0.95f, 0.35f, 0.55f, 0.65f),
                    startColorB = new Color(0.65f, 0.10f, 0.35f, 0.60f),
                    endColor = new Color(0.35f, 0.04f, 0.20f, 0f),
                    minLifetime = 0.26f,
                    maxLifetime = 0.45f,
                    minSpeed = 0.6f,
                    maxSpeed = 2.0f,
                    minSize = 0.28f,
                    maxSize = 0.52f,
                    radius = 0.25f,
                    gravityModifier = -0.15f,
                    upwardBias = 0.6f,
                    noiseStrength = 0.4f,
                    randomRotation = false,
                    burstCount = 9,
                    sortingOrder = 104
                };
                break;

            case Style.RgbRedCounteredByBlue:
                // 불 탄환이 물(Blue)에 소화됨: 피어오르는 하얀 수증기 + 물보라 입자
                primary = new LayerPreset
                {
                    shape = TexShape.Puff,
                    startColorA = new Color(0.95f, 0.99f, 1f, 0.88f),
                    startColorB = new Color(0.76f, 0.90f, 1f, 0.82f),
                    endColor = new Color(0.68f, 0.82f, 0.96f, 0f),
                    minLifetime = 0.34f,
                    maxLifetime = 0.62f,
                    minSpeed = 0.8f,
                    maxSpeed = 2.8f,
                    minSize = 0.32f,
                    maxSize = 0.64f,
                    radius = 0.30f,
                    gravityModifier = -0.55f,
                    upwardBias = 2.4f,
                    noiseStrength = 1.1f,
                    randomRotation = false,
                    burstCount = 16,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Square,
                    startColorA = new Color(0.75f, 0.96f, 1f, 1f),
                    startColorB = new Color(1f, 0.38f, 0.25f, 0.95f),
                    endColor = new Color(0.20f, 0.58f, 1f, 0f),
                    minLifetime = 0.20f,
                    maxLifetime = 0.38f,
                    minSpeed = 3.0f,
                    maxSpeed = 6.8f,
                    minSize = 0.14f,
                    maxSize = 0.26f,
                    radius = 0.24f,
                    gravityModifier = 0.5f,
                    upwardBias = 0.5f,
                    noiseStrength = 0.4f,
                    randomRotation = false,
                    burstCount = 18,
                    sortingOrder = 104
                };
                break;

            case Style.RgbBlueCounteredByGreen:
                // 물/전기 탄환이 자연(Green)에 흡수·분산됨: 민트/초록 역전 스파크 + 흩어지는 물방울
                primary = new LayerPreset
                {
                    shape = TexShape.Diamond,
                    startColorA = new Color(0.84f, 1f, 0.86f, 1f),
                    startColorB = new Color(0.22f, 0.92f, 0.46f, 1f),
                    endColor = new Color(0.08f, 0.55f, 0.24f, 0f),
                    minLifetime = 0.22f,
                    maxLifetime = 0.44f,
                    minSpeed = 2.6f,
                    maxSpeed = 6.4f,
                    minSize = 0.18f,
                    maxSize = 0.34f,
                    radius = 0.28f,
                    gravityModifier = -0.25f,
                    upwardBias = 1.0f,
                    noiseStrength = 1.0f,
                    randomRotation = true,
                    burstCount = 20,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Square,
                    startColorA = new Color(0.72f, 0.94f, 1f, 0.95f),
                    startColorB = new Color(0.24f, 0.72f, 1f, 0.9f),
                    endColor = new Color(0.10f, 0.36f, 0.85f, 0f),
                    minLifetime = 0.26f,
                    maxLifetime = 0.48f,
                    minSpeed = 2.4f,
                    maxSpeed = 5.5f,
                    minSize = 0.14f,
                    maxSize = 0.26f,
                    radius = 0.26f,
                    gravityModifier = 0.65f,
                    upwardBias = 0.2f,
                    noiseStrength = 0.3f,
                    randomRotation = false,
                    burstCount = 16,
                    sortingOrder = 104
                };
                break;

            case Style.RgbGreenCounteredByRed:
                // 자연/독 탄환이 불(Red)에 소각됨: 화염 섬광 + 타오르는 재/독기 연기
                primary = new LayerPreset
                {
                    shape = TexShape.Diamond,
                    startColorA = new Color(1f, 0.92f, 0.58f, 1f),
                    startColorB = new Color(1f, 0.26f, 0.12f, 1f),
                    endColor = new Color(0.65f, 0.06f, 0.10f, 0f),
                    minLifetime = 0.22f,
                    maxLifetime = 0.42f,
                    minSpeed = 2.8f,
                    maxSpeed = 6.8f,
                    minSize = 0.18f,
                    maxSize = 0.36f,
                    radius = 0.26f,
                    gravityModifier = -0.3f,
                    upwardBias = 1.4f,
                    noiseStrength = 0.7f,
                    randomRotation = true,
                    burstCount = 20,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Puff,
                    startColorA = new Color(0.45f, 0.85f, 0.42f, 0.70f),
                    startColorB = new Color(0.28f, 0.22f, 0.22f, 0.75f),
                    endColor = new Color(0.16f, 0.14f, 0.16f, 0f),
                    minLifetime = 0.32f,
                    maxLifetime = 0.56f,
                    minSpeed = 0.8f,
                    maxSpeed = 2.5f,
                    minSize = 0.30f,
                    maxSize = 0.58f,
                    radius = 0.28f,
                    gravityModifier = -0.45f,
                    upwardBias = 2.0f,
                    noiseStrength = 0.9f,
                    randomRotation = false,
                    burstCount = 14,
                    sortingOrder = 104
                };
                break;

            case Style.RgbExpireRed:
                primary = BuildSimpleBurst(TexShape.Diamond, new Color(1f, 0.82f, 0.65f, 1f), new Color(1f, 0.24f, 0.20f, 1f), new Color(0.58f, 0.04f, 0.12f, 0f), 14);
                secondary = BuildSimplePuff(new Color(1f, 0.35f, 0.25f, 0.55f), new Color(0.45f, 0.06f, 0.12f, 0f), 8);
                break;

            case Style.RgbExpireBlue:
                primary = BuildSimpleBurst(TexShape.Diamond, new Color(0.84f, 0.95f, 1f, 1f), new Color(0.22f, 0.62f, 1f, 1f), new Color(0.06f, 0.24f, 0.78f, 0f), 14);
                secondary = BuildSimplePuff(new Color(0.32f, 0.74f, 1f, 0.55f), new Color(0.08f, 0.28f, 0.78f, 0f), 8);
                break;

            case Style.RgbExpireGreen:
                primary = BuildSimpleBurst(TexShape.Diamond, new Color(0.84f, 1f, 0.86f, 1f), new Color(0.20f, 0.90f, 0.42f, 1f), new Color(0.05f, 0.48f, 0.20f, 0f), 14);
                secondary = BuildSimplePuff(new Color(0.26f, 0.88f, 0.46f, 0.55f), new Color(0.06f, 0.42f, 0.20f, 0f), 8);
                break;

            case Style.IceHammerShatterByGreen:
                // 얼음 망치가 Green에 산산조각남: 떨어지는 얼음 파편 + 냉기 서리 & 초록 카운터 섬광
                primary = new LayerPreset
                {
                    shape = TexShape.Shard,
                    startColorA = new Color(0.94f, 0.99f, 1f, 1f),
                    startColorB = new Color(0.48f, 0.86f, 1f, 1f),
                    endColor = new Color(0.24f, 0.64f, 0.98f, 0f),
                    minLifetime = 0.34f,
                    maxLifetime = 0.62f,
                    minSpeed = 3.2f,
                    maxSpeed = 7.8f,
                    minSize = 0.24f,
                    maxSize = 0.48f,
                    radius = 0.75f,
                    gravityModifier = 1.35f,
                    upwardBias = 0.8f,
                    noiseStrength = 0.25f,
                    randomRotation = true,
                    burstCount = 26,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Puff,
                    startColorA = new Color(0.68f, 1f, 0.82f, 0.82f),
                    startColorB = new Color(0.76f, 0.94f, 1f, 0.76f),
                    endColor = new Color(0.55f, 0.85f, 1f, 0f),
                    minLifetime = 0.30f,
                    maxLifetime = 0.55f,
                    minSpeed = 1.2f,
                    maxSpeed = 3.6f,
                    minSize = 0.42f,
                    maxSize = 0.82f,
                    radius = 0.85f,
                    gravityModifier = -0.15f,
                    upwardBias = 0.8f,
                    noiseStrength = 0.7f,
                    randomRotation = false,
                    burstCount = 16,
                    sortingOrder = 104
                };
                break;

            case Style.MushroomBurnByRed:
            default:
                // 독버섯이 Red(불)에 불타 소멸: 불꽃 파편 + 타오르는 재와 보라/녹색 포자 연기
                primary = new LayerPreset
                {
                    shape = TexShape.Diamond,
                    startColorA = new Color(1f, 0.92f, 0.56f, 1f),
                    startColorB = new Color(1f, 0.28f, 0.10f, 1f),
                    endColor = new Color(0.65f, 0.06f, 0.08f, 0f),
                    minLifetime = 0.26f,
                    maxLifetime = 0.50f,
                    minSpeed = 2.4f,
                    maxSpeed = 6.2f,
                    minSize = 0.18f,
                    maxSize = 0.38f,
                    radius = 0.48f,
                    gravityModifier = -0.35f,
                    upwardBias = 1.8f,
                    noiseStrength = 0.85f,
                    randomRotation = true,
                    burstCount = 22,
                    sortingOrder = 105
                };
                secondary = new LayerPreset
                {
                    shape = TexShape.Puff,
                    startColorA = new Color(0.62f, 0.42f, 0.78f, 0.78f),
                    startColorB = new Color(0.32f, 0.24f, 0.26f, 0.78f),
                    endColor = new Color(0.18f, 0.16f, 0.18f, 0f),
                    minLifetime = 0.36f,
                    maxLifetime = 0.64f,
                    minSpeed = 0.8f,
                    maxSpeed = 2.4f,
                    minSize = 0.36f,
                    maxSize = 0.72f,
                    radius = 0.55f,
                    gravityModifier = -0.50f,
                    upwardBias = 2.2f,
                    noiseStrength = 1.0f,
                    randomRotation = false,
                    burstCount = 16,
                    sortingOrder = 104
                };
                break;
        }
    }

    private static LayerPreset BuildSimpleBurst(TexShape shape, Color a, Color b, Color end, int count)
    {
        return new LayerPreset
        {
            shape = shape,
            startColorA = a,
            startColorB = b,
            endColor = end,
            minLifetime = 0.18f,
            maxLifetime = 0.34f,
            minSpeed = 2.0f,
            maxSpeed = 5.2f,
            minSize = 0.14f,
            maxSize = 0.28f,
            radius = 0.22f,
            gravityModifier = 0f,
            upwardBias = 0f,
            noiseStrength = 0.4f,
            randomRotation = true,
            burstCount = count,
            sortingOrder = 105
        };
    }

    private static LayerPreset BuildSimplePuff(Color a, Color end, int count)
    {
        return new LayerPreset
        {
            shape = TexShape.Puff,
            startColorA = a,
            startColorB = a,
            endColor = end,
            minLifetime = 0.24f,
            maxLifetime = 0.42f,
            minSpeed = 0.6f,
            maxSpeed = 1.8f,
            minSize = 0.26f,
            maxSize = 0.48f,
            radius = 0.24f,
            gravityModifier = -0.2f,
            upwardBias = 0.7f,
            noiseStrength = 0.5f,
            randomRotation = false,
            burstCount = count,
            sortingOrder = 104
        };
    }

    private static Texture2D CreateSquareTexture()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateDiamondTexture()
    {
        const int size = 7;
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
                int dist = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                float a = dist <= c ? (dist <= 1 ? 1f : 0.8f) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreatePuffTexture()
    {
        const int size = 10;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        float c = (size - 1) * 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d <= 2.8f ? 1f : (d <= 4.3f ? 0.55f : 0f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreateShardTexture()
    {
        const int size = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        string[] mask =
        {
            "....##..",
            "...####.",
            "..#####.",
            ".######.",
            ".#####..",
            "..####..",
            "...##...",
            "........"
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool filled = mask[size - 1 - y][x] == '#';
                pixels[y * size + x] = filled ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Material CreateMat(string matName, Texture2D tex)
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
