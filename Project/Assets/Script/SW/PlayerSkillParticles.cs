using UnityEngine;

/// <summary>
/// 플레이어 W 스킬 활성화 시 스탠스별 도트 잉크 오라와 스파크 파티클을 구성·제어합니다.
/// </summary>
public class PlayerSkillParticles : MonoBehaviour
{
    private ParticleSystem auraParticle;
    private ParticleSystem sparkParticle;
    private bool configured;

    private static Texture2D sharedInkDropTex;
    private static Texture2D sharedInkSparkTex;
    private static Material sharedInkDropMat;
    private static Material sharedInkSparkMat;

    public void Initialize(ParticleSystem mainParticle)
    {
        auraParticle = mainParticle != null ? mainParticle : GetComponentInChildren<ParticleSystem>();
        if (auraParticle == null) return;

        Configure();
    }

    public void Refresh(bool isSkillActive, PlayerStatus.Stance stance, bool playActivationBurst = false)
    {
        if (auraParticle == null) return;
        if (!configured) Configure();

        if (!isSkillActive)
        {
            Stop(clearImmediately: false);
            return;
        }

        ApplyColors(stance);

        if (!auraParticle.isEmitting)
        {
            auraParticle.Play(false);
        }
        if (sparkParticle != null && !sparkParticle.isEmitting)
        {
            sparkParticle.Play(false);
        }

        if (playActivationBurst)
        {
            auraParticle.Emit(8);
            if (sparkParticle != null)
            {
                sparkParticle.Emit(26);
            }
        }
    }

    public void ResetParticles()
    {
        Stop(clearImmediately: true);
    }

    public void Stop(bool clearImmediately)
    {
        ParticleSystemStopBehavior behavior = clearImmediately
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        if (auraParticle != null)
        {
            auraParticle.Stop(true, behavior);
        }
        if (sparkParticle != null)
        {
            sparkParticle.Stop(true, behavior);
        }
    }

    private void Configure()
    {
        if (auraParticle == null) return;

        EnsureAssets();

        // 프리팹에 stopAction=Destroy가 켜져 있을 수 있으므로 정지 전에 반드시 None으로 해제
        var main = auraParticle.main;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        auraParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = -0.15f;
        main.maxParticles = 50;

        var emission = auraParticle.emission;
        emission.enabled = true;
        emission.rateOverTime = 16f;

        var shape = auraParticle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.58f;
        shape.radiusThickness = 0.45f;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;

        var vel = auraParticle.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        vel.y = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = auraParticle.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 1.6f;
        noise.scrollSpeed = 0.8f;

        var sol = auraParticle.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0f)
        ));

        ParticleSystemRenderer auraRenderer = auraParticle.GetComponent<ParticleSystemRenderer>();
        if (auraRenderer != null)
        {
            auraRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            auraRenderer.sortingOrder = 299;
            if (sharedInkDropMat != null)
            {
                auraRenderer.sharedMaterial = sharedInkDropMat;
            }
        }

        // 보조 픽셀 스파크 파티클 (월드 좌표계로 잔상이 남는 브러시 스파크)
        Transform existingSpark = auraParticle.transform.Find("SkillSparkParticles");
        GameObject sparkGo;
        if (existingSpark == null)
        {
            sparkGo = new GameObject("SkillSparkParticles");
            sparkGo.SetActive(false);
            sparkGo.transform.SetParent(auraParticle.transform, false);
            sparkParticle = sparkGo.AddComponent<ParticleSystem>();
        }
        else
        {
            sparkGo = existingSpark.gameObject;
            sparkParticle = sparkGo.GetComponent<ParticleSystem>();
        }

        var sparkMain = sparkParticle.main;
        sparkMain.stopAction = ParticleSystemStopAction.None;
        sparkMain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        sparkMain.duration = 1f;
        sparkMain.loop = true;
        sparkMain.playOnAwake = false;
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.35f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.34f, 0.54f);
        sparkMain.startColor = Color.white;
        sparkMain.maxParticles = 120;

        var sparkEmission = sparkParticle.emission;
        sparkEmission.enabled = true;
        sparkEmission.rateOverTime = 36f;

        var sparkShape = sparkParticle.shape;
        sparkShape.enabled = true;
        sparkShape.shapeType = ParticleSystemShapeType.Circle;
        sparkShape.radius = 1f;
        sparkShape.radiusThickness = 0.65f;

        var sparkVel = sparkParticle.velocityOverLifetime;
        sparkVel.enabled = true;
        sparkVel.space = ParticleSystemSimulationSpace.Local;
        sparkVel.x = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
        sparkVel.y = new ParticleSystem.MinMaxCurve(0.95f, 1.9f);
        sparkVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sparkSol = sparkParticle.sizeOverLifetime;
        sparkSol.enabled = true;
        sparkSol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.15f, 1.15f),
            new Keyframe(0.65f, 0.9f),
            new Keyframe(1f, 0f)
        ));

        ParticleSystemRenderer sparkRenderer = sparkParticle.GetComponent<ParticleSystemRenderer>();
        if (sparkRenderer != null)
        {
            sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            sparkRenderer.sortingOrder = 302;
            if (sharedInkSparkMat != null)
            {
                sparkRenderer.sharedMaterial = sharedInkSparkMat;
            }
        }

        sparkGo.SetActive(true);
        sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        configured = true;
    }

    private void ApplyColors(PlayerStatus.Stance stance)
    {
        GetStancePalette(stance, out Color highlight, out Color core, out Color tail);

        Gradient auraGrad = new Gradient();
        auraGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(highlight, 0f),
                new GradientColorKey(core, 0.45f),
                new GradientColorKey(tail, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.95f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        Gradient sparkGrad = new Gradient();
        sparkGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.Lerp(highlight, Color.white, 0.55f), 0f),
                new GradientColorKey(core, 0.5f),
                new GradientColorKey(tail, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(1f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        if (auraParticle != null)
        {
            var main = auraParticle.main;
            main.startColor = new ParticleSystem.MinMaxGradient(highlight, core);

            var col = auraParticle.colorOverLifetime;
            col.enabled = true;
            col.color = auraGrad;
        }

        if (sparkParticle != null)
        {
            var sparkMain = sparkParticle.main;
            sparkMain.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(highlight, Color.white, 0.35f), core);

            var sparkCol = sparkParticle.colorOverLifetime;
            sparkCol.enabled = true;
            sparkCol.color = sparkGrad;
        }
    }

    private static void GetStancePalette(PlayerStatus.Stance stance, out Color highlight, out Color core, out Color tail)
    {
        switch (stance)
        {
            case PlayerStatus.Stance.Red:
                highlight = new Color(1f, 0.72f, 0.62f, 1f);
                core = new Color(1f, 0.16f, 0.2f, 1f);
                tail = new Color(0.68f, 0.04f, 0.12f, 1f);
                break;
            case PlayerStatus.Stance.Green:
                highlight = new Color(0.74f, 1f, 0.82f, 1f);
                core = new Color(0.14f, 0.92f, 0.38f, 1f);
                tail = new Color(0.05f, 0.55f, 0.22f, 1f);
                break;
            case PlayerStatus.Stance.Blue:
                highlight = new Color(0.74f, 0.9f, 1f, 1f);
                core = new Color(0.18f, 0.52f, 1f, 1f);
                tail = new Color(0.06f, 0.22f, 0.72f, 1f);
                break;
            default:
                highlight = Color.white;
                core = new Color(0.88f, 0.93f, 1f, 1f);
                tail = new Color(0.62f, 0.72f, 0.85f, 1f);
                break;
        }
    }

    private static void EnsureAssets()
    {
        if (sharedInkDropTex == null)
        {
            const int size = 8;
            sharedInkDropTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
                    float dx = (x - center) / 3.4f;
                    float dy = (y - center) / 3.4f;
                    float d2 = dx * dx + dy * dy;
                    float a = d2 <= 0.85f ? 1f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sharedInkDropTex.SetPixels(pixels);
            sharedInkDropTex.Apply();
        }

        if (sharedInkSparkTex == null)
        {
            const int size = 9;
            sharedInkSparkTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
                    float a = isCross ? 1f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sharedInkSparkTex.SetPixels(pixels);
            sharedInkSparkTex.Apply();
        }

        if (sharedInkDropMat == null)
        {
            sharedInkDropMat = CreateUnlitParticleMaterial("PlayerSkillInkDropMat", sharedInkDropTex);
        }
        if (sharedInkSparkMat == null)
        {
            sharedInkSparkMat = CreateUnlitParticleMaterial("PlayerSkillInkSparkMat", sharedInkSparkTex);
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
