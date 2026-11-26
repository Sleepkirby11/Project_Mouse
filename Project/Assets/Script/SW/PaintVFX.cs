using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Paint.cs 기반 공격 선(isTrigger=true)과 발판 선(isTrigger=false)의 32bit 픽셀 페인트 VFX 컨트롤러.
/// - 공격 선: 붓끝 물감 방울 + 발동 시 잉크 스플래터 + 배경에 묻어 남는 잉크 얼룩 잔상
/// - 발판 선: 붓끝 물감 방울 + 응고 플래시/결정 스파크 -> 고체 유지 -> 기체로 증발
/// </summary>
public class PaintVFX : MonoBehaviour
{
    [Header("공통 설정")]
    [Tooltip("픽셀 파티클과 배경 잉크 잔상에 사용할 머티리얼 (비어있으면 기본 Sprites/Default 사용)")]
    [SerializeField] private Material pixelMaterial;
    [SerializeField] private float drawDropDistance = 0.45f;
    [SerializeField] private float drawingWidth = 0.25f;

    [Header("공격 선 (isTrigger = true) - 배경에 잉크 묻히기")]
    [SerializeField] private float attackBaseWidth = 0.72f;
    [SerializeField] private float attackSpreadBoost = 1.28f;
    [SerializeField] private float attackSpreadDuration = 0.08f;
    [Tooltip("판정 종료 후 배경에 잉크 얼룩이 더 남아 마르는 시간(초)")]
    [SerializeField] private float stainExtraDuration = 0.38f;
    [SerializeField] private int stainSortingOrder = 2;
    [SerializeField] private int maxStainPool = 6;
    [SerializeField] private float splatterDensityPerUnit = 2.6f;
    [SerializeField] private int maxSplatterCount = 42;

    [Header("발판 선 (isTrigger = false) - 응고 후 기화")]
    [SerializeField] private float groundBaseWidth = 0.42f;
    [Range(0.05f, 0.35f)]
    [SerializeField] private float solidifyRatio = 0.16f;
    [Range(0.35f, 0.8f)]
    [SerializeField] private float vaporizeStartRatio = 0.56f;
    [SerializeField] private float vaporEmitRate = 55f;

    [Header("발판 명암 디테일 (상단 하이라이트 + 하단 그림자)")]
    [SerializeField] private bool enableGroundShading = true;
    [SerializeField] private float groundHighlightOffset = 0.11f;
    [SerializeField] private float groundShadowOffset = -0.11f;
    [Range(0.15f, 0.8f)]
    [SerializeField] private float groundHighlightWidthRatio = 0.34f;
    [Range(0.8f, 1.4f)]
    [SerializeField] private float groundShadowWidthRatio = 1.08f;
    [Range(0f, 1f)]
    [SerializeField] private float groundHighlightTint = 0.72f;
    [Range(0f, 1f)]
    [SerializeField] private float groundShadowDarken = 0.62f;

    private TrailRenderer trail;
    private EdgeCollider2D edgeCol;
    private PlayerStatus playerStatus;

    private ParticleSystem inkDropPS;
    private ParticleSystem vaporPS;

    // 발판 입체 명암용 보조 LineRenderer (방향과 무관하게 항상 윗면 밝음 / 아랫면 어두움)
    private GameObject groundHighlightObj;
    private LineRenderer groundHighlightLine;
    private GameObject groundShadowObj;
    private LineRenderer groundShadowLine;

    private bool isAttackMode;
    private Gradient cachedBaseGradient;
    private Color cachedMainColor = Color.white;
    private Vector3 lastDropPos;
    private bool hasLastDropPos;

    private readonly List<Vector3> activePath = new List<Vector3>(256);
    private float vaporEmitAccumulator;
    private float activeDuration = 0.5f;

    // 공격 선 배경 잉크 얼룩 잔상 풀
    private class InkStainInstance
    {
        public GameObject obj;
        public LineRenderer line;
        public Gradient baseGradient;
        public float baseWidth;
        public float elapsed;
        public float holdDuration;
        public float fadeDuration;
        public bool active;
    }

    private readonly List<InkStainInstance> stainPool = new List<InkStainInstance>();
    private int stainCursor;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        edgeCol = GetComponent<EdgeCollider2D>();
        isAttackMode = edgeCol != null && edgeCol.isTrigger;

        EnsureMaterial();
        CreateParticleSystems();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerStatus = playerObj.GetComponent<PlayerStatus>();
        }

        if (trail != null)
        {
            cachedBaseGradient = CloneGradient(trail.colorGradient);
        }

        if (isAttackMode)
        {
            InitializeStainPool();
        }
        else if (enableGroundShading)
        {
            InitializeGroundShadingLines();
        }
    }

    private void Update()
    {
        if (stainPool.Count > 0)
        {
            UpdateInkStains(Time.deltaTime);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < stainPool.Count; i++)
        {
            if (stainPool[i].obj != null)
            {
                Destroy(stainPool[i].obj);
            }
        }
        stainPool.Clear();

        if (groundHighlightObj != null) Destroy(groundHighlightObj);
        if (groundShadowObj != null) Destroy(groundShadowObj);
    }

    #region Public Hooks (Called by Paint.cs)

    public void OnDrawStart(Vector3 startWorldPos)
    {
        RefreshStanceVisuals();

        if (trail != null)
        {
            trail.colorGradient = CloneGradient(cachedBaseGradient);
            trail.startWidth = drawingWidth;
            trail.endWidth = drawingWidth;
        }

        activePath.Clear();
        vaporEmitAccumulator = 0f;
        SetGroundShadingEnabled(false);
        lastDropPos = startWorldPos;
        hasLastDropPos = true;
    }

    public void OnDrawStep(Vector3 currentWorldPos)
    {
        if (!hasLastDropPos)
        {
            lastDropPos = currentWorldPos;
            hasLastDropPos = true;
            return;
        }

        float dist = Vector3.Distance(lastDropPos, currentWorldPos);
        if (dist >= drawDropDistance)
        {
            EmitInkDrop(currentWorldPos, isSmallDrawDrop: true, skillBoost: false);
            lastDropPos = currentWorldPos;
        }
    }

    public void OnActivate(Vector3[] positions, int count, float duration, bool isSkill)
    {
        hasLastDropPos = false;
        activeDuration = Mathf.Max(0.01f, duration);
        vaporEmitAccumulator = 0f;
        RefreshStanceVisuals();

        activePath.Clear();
        for (int i = 0; i < count; i++)
        {
            activePath.Add(positions[i]);
        }

        if (isAttackMode)
        {
            ActivateAttackVFX(isSkill);
        }
        else
        {
            ActivateGroundVFX();
        }
    }

    public void OnLifeTimeUpdate(float remainingTime, float totalDuration)
    {
        if (trail == null) return;

        float maxDur = Mathf.Max(0.01f, totalDuration > 0f ? totalDuration : activeDuration);
        float elapsed = Mathf.Clamp(maxDur - remainingTime, 0f, maxDur);
        float progress = Mathf.Clamp01(elapsed / maxDur);

        if (isAttackMode)
        {
            UpdateAttackLine(elapsed, progress);
        }
        else
        {
            UpdateGroundLine(progress, Time.fixedDeltaTime);
        }
    }

    public void OnFinish()
    {
        if (trail != null)
        {
            trail.startWidth = 0f;
            trail.endWidth = 0f;
            if (cachedBaseGradient != null)
            {
                trail.colorGradient = CloneGradient(cachedBaseGradient);
            }
        }

        activePath.Clear();
        vaporEmitAccumulator = 0f;
        SetGroundShadingEnabled(false);
    }

    #endregion

    #region Attack Mode (Ink Stain & Splatter)

    private void ActivateAttackVFX(bool isSkill)
    {
        float width = isSkill ? attackBaseWidth * 1.25f : attackBaseWidth;

        // 1. 배경에 묻어 남는 잉크 얼룩 잔상 스폰 (판정 종료 후에도 stainExtraDuration만큼 더 남았다 마름)
        SpawnBackgroundInkStain(width);

        // 2. 선 경로를 따라 픽셀 잉크 스플래터 방출
        if (activePath.Count < 2) return;

        float totalLength = CalculatePathLength(activePath);
        float density = isSkill ? splatterDensityPerUnit * 1.55f : splatterDensityPerUnit;
        int emitCount = Mathf.Clamp(Mathf.RoundToInt(totalLength * density), 6, isSkill ? Mathf.RoundToInt(maxSplatterCount * 1.4f) : maxSplatterCount);

        for (int i = 0; i < emitCount; i++)
        {
            Vector3 pt = SamplePointAlongPath(activePath, Random.value);
            EmitInkDrop(pt, isSmallDrawDrop: false, skillBoost: isSkill);
        }
    }

    private void UpdateAttackLine(float elapsed, float progress)
    {
        // 발동 직후 찰나(attackSpreadDuration) 잉크가 탁! 번지듯 커졌다가 안정
        float width;
        if (elapsed < attackSpreadDuration)
        {
            float t = elapsed / Mathf.Max(0.001f, attackSpreadDuration);
            width = Mathf.Lerp(drawingWidth, attackBaseWidth * attackSpreadBoost, EaseOutQuad(t));
        }
        else
        {
            float settleT = Mathf.Clamp01((elapsed - attackSpreadDuration) / Mathf.Max(0.001f, activeDuration - attackSpreadDuration));
            width = Mathf.Lerp(attackBaseWidth * attackSpreadBoost, attackBaseWidth * 0.92f, settleT);
        }

        trail.startWidth = width;
        trail.endWidth = width;

        // 본체 트레일은 판정 종료 시점에 맞춰 부드럽게 알파만 페이드 (배경 InkStain으로 매끄럽게 이어짐)
        float alpha = progress < 0.45f ? 1f : Mathf.Lerp(1f, 0f, (progress - 0.45f) / 0.55f);
        ApplyGradientAlpha(trail, cachedBaseGradient, alpha, Color.white, 0f);
    }

    private void InitializeStainPool()
    {
        int count = Mathf.Max(1, maxStainPool);
        for (int i = 0; i < count; i++)
        {
            GameObject stainObj = new GameObject($"InkStain_{i}");
            DontDestroyOnLoad(stainObj);

            LineRenderer lr = stainObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = pixelMaterial;
            lr.sortingOrder = stainSortingOrder;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;

            stainPool.Add(new InkStainInstance
            {
                obj = stainObj,
                line = lr,
                active = false
            });
        }
    }

    private void SpawnBackgroundInkStain(float width)
    {
        if (stainPool.Count == 0 || activePath.Count < 2) return;

        InkStainInstance stain = stainPool[stainCursor];
        stainCursor = (stainCursor + 1) % stainPool.Count;

        if (stain.line == null) return;

        stain.line.positionCount = activePath.Count;
        for (int i = 0; i < activePath.Count; i++)
        {
            stain.line.SetPosition(i, activePath[i]);
        }

        stain.baseGradient = CloneGradient(cachedBaseGradient);
        stain.baseWidth = width * 0.96f;
        stain.elapsed = 0f;
        stain.holdDuration = activeDuration * 0.65f;
        stain.fadeDuration = activeDuration * 0.35f + stainExtraDuration;
        stain.active = true;

        stain.line.startWidth = stain.baseWidth;
        stain.line.endWidth = stain.baseWidth;
        stain.line.colorGradient = BuildModifiedGradient(stain.baseGradient, 0.85f, Color.black, 0.08f);
        stain.line.enabled = true;
    }

    private void UpdateInkStains(float dt)
    {
        for (int i = 0; i < stainPool.Count; i++)
        {
            InkStainInstance stain = stainPool[i];
            if (!stain.active || stain.line == null) continue;

            stain.elapsed += dt;
            float total = stain.holdDuration + stain.fadeDuration;

            if (stain.elapsed >= total)
            {
                stain.active = false;
                stain.line.enabled = false;
                continue;
            }

            if (stain.elapsed <= stain.holdDuration)
            {
                // 배경에 번지며 살짝 넓어짐
                float spreadT = stain.elapsed / Mathf.Max(0.01f, stain.holdDuration);
                float w = Mathf.Lerp(stain.baseWidth, stain.baseWidth * 1.12f, EaseOutQuad(spreadT));
                stain.line.startWidth = w;
                stain.line.endWidth = w;
            }
            else
            {
                // 서서히 마르며 알파 페이드 아웃
                float fadeT = (stain.elapsed - stain.holdDuration) / Mathf.Max(0.01f, stain.fadeDuration);
                float alpha = Mathf.Lerp(0.85f, 0f, fadeT);
                stain.line.colorGradient = BuildModifiedGradient(stain.baseGradient, alpha, Color.black, 0.1f);
            }
        }
    }

    #endregion

    #region Ground Mode (Solidify -> Hold -> Vaporize)

    private void InitializeGroundShadingLines()
    {
        int baseOrder = trail != null ? trail.sortingOrder : 0;

        groundHighlightLine = CreateGroundShadingLine("GroundHighlight_Rim", baseOrder + 1, out groundHighlightObj);
        groundShadowLine = CreateGroundShadingLine("GroundShadow_Depth", baseOrder - 1, out groundShadowObj);
    }

    private LineRenderer CreateGroundShadingLine(string objName, int sortingOrder, out GameObject createdObj)
    {
        createdObj = new GameObject(objName);
        DontDestroyOnLoad(createdObj);

        LineRenderer lr = createdObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = pixelMaterial;
        lr.sortingOrder = sortingOrder;
        lr.numCapVertices = 1;
        lr.numCornerVertices = 1;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    private void SetGroundShadingEnabled(bool enabled)
    {
        if (groundHighlightLine != null) groundHighlightLine.enabled = enabled;
        if (groundShadowLine != null) groundShadowLine.enabled = enabled;
    }

    private void ApplyGroundShadingPath()
    {
        if (!enableGroundShading || activePath.Count < 2) return;

        if (groundHighlightLine == null || groundShadowLine == null)
        {
            InitializeGroundShadingLines();
        }

        Vector3 hiOffset = new Vector3(0f, groundHighlightOffset, 0f);
        Vector3 shOffset = new Vector3(0f, groundShadowOffset, 0f);

        groundHighlightLine.positionCount = activePath.Count;
        groundShadowLine.positionCount = activePath.Count;

        for (int i = 0; i < activePath.Count; i++)
        {
            groundHighlightLine.SetPosition(i, activePath[i] + hiOffset);
            groundShadowLine.SetPosition(i, activePath[i] + shOffset);
        }

        SetGroundShadingEnabled(true);
    }

    private void UpdateGroundShadingVisual(float baseWidth, float alpha, float solidifyFlash)
    {
        if (!enableGroundShading) return;

        if (groundHighlightLine != null && groundHighlightLine.enabled)
        {
            float hiWidth = baseWidth * groundHighlightWidthRatio;
            groundHighlightLine.startWidth = hiWidth;
            groundHighlightLine.endWidth = hiWidth;
            float hiTint = Mathf.Clamp01(groundHighlightTint + solidifyFlash * 0.28f);
            groundHighlightLine.colorGradient = BuildModifiedGradient(cachedBaseGradient, alpha * 0.92f, Color.white, hiTint);
        }

        if (groundShadowLine != null && groundShadowLine.enabled)
        {
            float shWidth = baseWidth * groundShadowWidthRatio;
            groundShadowLine.startWidth = shWidth;
            groundShadowLine.endWidth = shWidth;
            groundShadowLine.colorGradient = BuildModifiedGradient(cachedBaseGradient, alpha * 0.95f, Color.black, groundShadowDarken);
        }
    }

    private void ActivateGroundVFX()
    {
        if (activePath.Count < 2) return;

        // 1. 월드 Y 기준 상단 하이라이트 / 하단 그림자 레이어 활성화
        ApplyGroundShadingPath();
        UpdateGroundShadingVisual(groundBaseWidth, 1f, 0.65f);

        // 2. 응고 시 선 경로를 따라 픽셀 결정 스파크 톡톡 방출
        float totalLength = CalculatePathLength(activePath);
        int sparkCount = Mathf.Clamp(Mathf.RoundToInt(totalLength * 2.2f), 5, 24);

        for (int i = 0; i < sparkCount; i++)
        {
            Vector3 pt = SamplePointAlongPath(activePath, Random.value);
            EmitSolidifySpark(pt);
        }
    }

    private void UpdateGroundLine(float progress, float dt)
    {
        // 1단계: 응고 (0 ~ solidifyRatio) - 하얀 하이라이트 플래시 후 단단히 고정
        if (progress < solidifyRatio)
        {
            float t = progress / Mathf.Max(0.001f, solidifyRatio);
            float flash = Mathf.Lerp(0.65f, 0f, EaseOutQuad(t));
            float width = Mathf.Lerp(drawingWidth, groundBaseWidth, EaseOutQuad(t));

            trail.startWidth = width;
            trail.endWidth = width;
            ApplyGradientAlpha(trail, cachedBaseGradient, 1f, Color.white, flash);
            UpdateGroundShadingVisual(width, 1f, flash);
            return;
        }

        // 2단계: 고체 유지 (solidifyRatio ~ vaporizeStartRatio)
        if (progress < vaporizeStartRatio)
        {
            trail.startWidth = groundBaseWidth;
            trail.endWidth = groundBaseWidth;
            ApplyGradientAlpha(trail, cachedBaseGradient, 1f, Color.white, 0f);
            UpdateGroundShadingVisual(groundBaseWidth, 1f, 0f);

            // 중후반부터 아주 미세하게 기체 입자 1~2개씩 피어오름
            float holdT = (progress - solidifyRatio) / Mathf.Max(0.001f, vaporizeStartRatio - solidifyRatio);
            if (holdT > 0.55f)
            {
                EmitVaporAlongPath(vaporEmitRate * 0.25f * dt);
            }
            return;
        }

        // 3단계: 서서히 기체가 되어 증발 (vaporizeStartRatio ~ 1.0, 깜빡임 없음)
        float vapT = Mathf.Clamp01((progress - vaporizeStartRatio) / Mathf.Max(0.001f, 1f - vaporizeStartRatio));
        float alpha = Mathf.Lerp(1f, 0f, vapT);
        float vaporWidth = Mathf.Lerp(groundBaseWidth, groundBaseWidth * 0.28f, vapT);

        trail.startWidth = vaporWidth;
        trail.endWidth = vaporWidth;
        ApplyGradientAlpha(trail, cachedBaseGradient, alpha, Color.white, vapT * 0.35f);
        UpdateGroundShadingVisual(vaporWidth, alpha, 0f);

        EmitVaporAlongPath(vaporEmitRate * dt);
    }

    private void EmitVaporAlongPath(float amount)
    {
        if (activePath.Count < 2 || vaporPS == null) return;

        vaporEmitAccumulator += amount;
        int toEmit = Mathf.FloorToInt(vaporEmitAccumulator);
        if (toEmit <= 0) return;

        vaporEmitAccumulator -= toEmit;

        for (int i = 0; i < toEmit; i++)
        {
            Vector3 pt = SamplePointAlongPath(activePath, Random.value);
            EmitVaporParticle(pt);
        }
    }

    #endregion

    #region Particle System Setup & Emission

    private void EnsureMaterial()
    {
        if (pixelMaterial != null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            pixelMaterial = new Material(shader);
        }
    }

    private void CreateParticleSystems()
    {
        int order = trail != null ? trail.sortingOrder + 1 : 10;

        // 1. 잉크 방울 / 스플래터 / 응고 스파크용 파티클 (중력 적용)
        inkDropPS = CreateChildParticleSystem("PaintInkDrops_PS", order, gravity: 1.4f, useNoise: false);

        // 2. 발판 기화 연기용 파티클 (위로 상승 + 살랑임)
        if (!isAttackMode)
        {
            vaporPS = CreateChildParticleSystem("PaintVapor_PS", order, gravity: -0.25f, useNoise: true);
        }
    }

    private ParticleSystem CreateChildParticleSystem(string childName, int sortingOrder, float gravity, bool useNoise)
    {
        GameObject psObj = new GameObject(childName);
        psObj.transform.SetParent(transform, false);
        psObj.transform.localPosition = Vector3.zero;
        psObj.transform.localRotation = Quaternion.identity;
        psObj.transform.localScale = Vector3.one;

        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.gravityModifier = gravity;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = useNoise
            ? new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.35f, 1.15f), new Keyframe(1f, 0f))
            : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaGrad = new Gradient();
        alphaGrad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.45f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGrad);

        var limitVelocity = ps.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = useNoise ? 1.8f : 1.5f;
        limitVelocity.dampen = useNoise ? 0.15f : 0.22f;

        if (useNoise)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.75f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;
        }

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = pixelMaterial;
        renderer.sortingOrder = sortingOrder;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        return ps;
    }

    private void EmitInkDrop(Vector3 worldPos, bool isSmallDrawDrop, bool skillBoost)
    {
        if (inkDropPS == null) return;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float speed = isSmallDrawDrop
            ? Random.Range(0.6f, 1.8f)
            : (skillBoost ? Random.Range(2.4f, 6.2f) : Random.Range(1.6f, 4.5f));

        float size = isSmallDrawDrop
            ? Random.Range(0.14f, 0.24f)
            : (skillBoost ? Random.Range(0.24f, 0.46f) : Random.Range(0.18f, 0.36f));

        Color c = Color.Lerp(cachedMainColor, Color.white, Random.Range(0f, 0.18f));
        c.a = Random.Range(0.8f, 1f);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = worldPos + (Vector3)(Random.insideUnitCircle * (isSmallDrawDrop ? 0.08f : 0.18f)),
            velocity = new Vector3(dir.x * speed, dir.y * speed + (isSmallDrawDrop ? 0.5f : 1.2f), 0f),
            startSize = size,
            startLifetime = isSmallDrawDrop ? Random.Range(0.2f, 0.35f) : Random.Range(0.28f, 0.52f),
            startColor = c
        };

        inkDropPS.Emit(emitParams, 1);
    }

    private void EmitSolidifySpark(Vector3 worldPos)
    {
        if (inkDropPS == null) return;

        Vector2 dir = Random.insideUnitCircle.normalized;
        Color c = Color.Lerp(cachedMainColor, Color.white, Random.Range(0.55f, 0.9f));
        c.a = 0.95f;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = worldPos + (Vector3)(Random.insideUnitCircle * 0.12f),
            velocity = new Vector3(dir.x * Random.Range(0.8f, 2.0f), Mathf.Abs(dir.y) * Random.Range(0.8f, 2.0f), 0f),
            startSize = Random.Range(0.14f, 0.24f),
            startLifetime = Random.Range(0.14f, 0.24f),
            startColor = c
        };

        inkDropPS.Emit(emitParams, 1);
    }

    private void EmitVaporParticle(Vector3 worldPos)
    {
        if (vaporPS == null) return;

        Color c = Color.Lerp(cachedMainColor, Color.white, Random.Range(0.35f, 0.7f));
        c.a = Random.Range(0.45f, 0.72f);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = worldPos + (Vector3)(Random.insideUnitCircle * 0.14f),
            velocity = new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(1.2f, 2.5f), 0f),
            startSize = Random.Range(0.24f, 0.44f),
            startLifetime = Random.Range(0.38f, 0.68f),
            startColor = c
        };

        vaporPS.Emit(emitParams, 1);
    }

    #endregion

    #region Helpers

    private void RefreshStanceVisuals()
    {
        if (playerStatus != null)
        {
            Gradient stanceGrad = playerStatus.ChangeStance(playerStatus.currentStance);
            if (stanceGrad != null)
            {
                cachedBaseGradient = CloneGradient(stanceGrad);
                cachedMainColor = cachedBaseGradient.Evaluate(0.5f);
                return;
            }
        }

        if (trail != null)
        {
            cachedBaseGradient = CloneGradient(trail.colorGradient);
            cachedMainColor = cachedBaseGradient.Evaluate(0.5f);
        }
    }

    private static float CalculatePathLength(List<Vector3> path)
    {
        float len = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            len += Vector3.Distance(path[i - 1], path[i]);
        }
        return len;
    }

    private static Vector3 SamplePointAlongPath(List<Vector3> path, float normalizedT)
    {
        if (path.Count == 0) return Vector3.zero;
        if (path.Count == 1) return path[0];

        float scaled = Mathf.Clamp01(normalizedT) * (path.Count - 1);
        int idx = Mathf.FloorToInt(scaled);
        int nextIdx = Mathf.Min(idx + 1, path.Count - 1);
        float frac = scaled - idx;
        return Vector3.Lerp(path[idx], path[nextIdx], frac);
    }

    private static Gradient CloneGradient(Gradient source)
    {
        Gradient copy = new Gradient();
        if (source == null) return copy;

        copy.mode = source.mode;
        copy.SetKeys(source.colorKeys, source.alphaKeys);
        return copy;
    }

    private static Gradient BuildModifiedGradient(Gradient source, float alphaScale, Color tintColor, float tintAmount)
    {
        Gradient grad = new Gradient();
        if (source == null) return grad;

        GradientColorKey[] cKeys = source.colorKeys;
        GradientAlphaKey[] aKeys = source.alphaKeys;

        GradientColorKey[] newCKeys = new GradientColorKey[cKeys.Length];
        for (int i = 0; i < cKeys.Length; i++)
        {
            newCKeys[i] = new GradientColorKey(
                tintAmount > 0f ? Color.Lerp(cKeys[i].color, tintColor, tintAmount) : cKeys[i].color,
                cKeys[i].time
            );
        }

        GradientAlphaKey[] newAKeys = new GradientAlphaKey[aKeys.Length];
        for (int i = 0; i < aKeys.Length; i++)
        {
            newAKeys[i] = new GradientAlphaKey(aKeys[i].alpha * alphaScale, aKeys[i].time);
        }

        grad.mode = source.mode;
        grad.SetKeys(newCKeys, newAKeys);
        return grad;
    }

    private static void ApplyGradientAlpha(TrailRenderer target, Gradient source, float alphaScale, Color tintColor, float tintAmount)
    {
        if (target == null || source == null) return;
        target.colorGradient = BuildModifiedGradient(source, alphaScale, tintColor, tintAmount);
    }

    private static float EaseOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    #endregion
}
