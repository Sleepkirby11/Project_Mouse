using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 W 스킬 적중 시 일반 몬스터 및 보스 모두에게
/// 빨간색(고화력 타격 섬광/스파크/충격파)과 초록색(생명력 흡수 오브 + 플레이어 '+' 회복 파티클) 이펙트를 재생합니다.
/// </summary>
public static class SkillHitParticles
{
    private sealed class PoolHost : MonoBehaviour
    {
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ReturnAllActive();
        }
    }

    private sealed class EffectInstance : MonoBehaviour
    {
        private ParticleSystem mainPs;
        private ParticleSystem subPs;
        private ParticleSystem ringPs;
        private Coroutine routine;

        private struct OrbTrailItem
        {
            public Transform tf;
            public ParticleSystem ps;
            public Vector3 startPos;
            public Vector3 controlOffset;
        }

        private readonly List<OrbTrailItem> drainOrbs = new List<OrbTrailItem>();

        public void EnsureBuilt()
        {
            if (mainPs != null) return;

            mainPs = CreateChildPs("MainBurst");
            subPs = CreateChildPs("SubBurst");
            ringPs = CreateChildPs("RingBurst");
        }

        public void PlayRedPowerHit(Vector3 targetCenter, float targetRadius)
        {
            EnsureBuilt();
            StopRunning();

            float scale = Mathf.Clamp(targetRadius * 0.85f, 0.7f, 2.3f);
            transform.position = targetCenter;

            // 1. 크리티컬 X-스타버스트 섬광
            ConfigureBurstPs(
                mainPs,
                starMat,
                sortingOrder: 262,
                minLife: 0.16f, maxLife: 0.28f,
                minSpeed: 0.2f, maxSpeed: 0.8f,
                minSize: 0.9f * scale, maxSize: 1.5f * scale,
                radius: 0.2f * scale,
                gravity: 0f,
                upwardVel: 0f,
                noiseStr: 0f,
                randomRot: true,
                colorA: new Color(1f, 0.96f, 0.78f, 1f),
                colorB: new Color(1f, 0.22f, 0.16f, 1f),
                endColor: new Color(0.70f, 0.04f, 0.10f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.2f, 1.15f),
                    new Keyframe(1f, 0.05f)
                )
            );

            // 2. 사방으로 튀는 고화력 크림슨·골드 스파크
            ConfigureBurstPs(
                subPs,
                diamondMat,
                sortingOrder: 261,
                minLife: 0.22f, maxLife: 0.44f,
                minSpeed: 3.5f * scale, maxSpeed: 9.2f * scale,
                minSize: 0.18f, maxSize: 0.38f,
                radius: 0.25f * scale,
                gravity: 0.2f,
                upwardVel: 0.4f,
                noiseStr: 0.5f,
                randomRot: true,
                colorA: new Color(1f, 0.92f, 0.55f, 1f),
                colorB: new Color(1f, 0.18f, 0.16f, 1f),
                endColor: new Color(0.55f, 0.02f, 0.08f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.35f, 1.05f),
                    new Keyframe(1f, 0f)
                )
            );

            // 3. 확장 충격파 링
            ConfigureBurstPs(
                ringPs,
                ringMat,
                sortingOrder: 260,
                minLife: 0.20f, maxLife: 0.26f,
                minSpeed: 0f, maxSpeed: 0f,
                minSize: 1.4f * scale, maxSize: 1.8f * scale,
                radius: 0.02f,
                gravity: 0f,
                upwardVel: 0f,
                noiseStr: 0f,
                randomRot: true,
                colorA: new Color(1f, 0.80f, 0.50f, 0.95f),
                colorB: new Color(1f, 0.24f, 0.18f, 0.95f),
                endColor: new Color(0.65f, 0.04f, 0.10f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(1f, 1.55f)
                )
            );

            mainPs.Play(false);
            mainPs.Emit(3);
            subPs.Play(false);
            subPs.Emit(24);
            ringPs.Play(false);
            ringPs.Emit(2);

            routine = StartCoroutine(ReturnAfterDelay(0.55f));
        }

        public void PlayGreenHealHit(Vector3 targetCenter, float targetRadius, Transform playerTransform)
        {
            EnsureBuilt();
            StopRunning();

            float scale = Mathf.Clamp(targetRadius * 0.75f, 0.6f, 1.8f);
            transform.position = targetCenter;

            // 1. 피격 대상에서 터져 나오는 에메랄드/민트 생명력 입자
            ConfigureBurstPs(
                mainPs,
                diamondMat,
                sortingOrder: 261,
                minLife: 0.22f, maxLife: 0.42f,
                minSpeed: 2.0f * scale, maxSpeed: 5.2f * scale,
                minSize: 0.18f, maxSize: 0.34f,
                radius: 0.35f * scale,
                gravity: -0.25f,
                upwardVel: 1.0f,
                noiseStr: 0.8f,
                randomRot: true,
                colorA: new Color(0.84f, 1f, 0.88f, 1f),
                colorB: new Color(0.18f, 0.94f, 0.44f, 1f),
                endColor: new Color(0.05f, 0.52f, 0.22f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 0.8f),
                    new Keyframe(0.3f, 1.1f),
                    new Keyframe(1f, 0f)
                )
            );
            mainPs.Play(false);
            mainPs.Emit(16);

            // 2. 회복 대상 주변 피회복 '+' 십자 픽셀 파티클
            if (playerTransform != null)
            {
                Vector3 healCenter = ResolveTargetCenter(playerTransform);
                subPs.transform.position = healCenter;
                ConfigureBurstPs(
                    subPs,
                    plusMat,
                    sortingOrder: 262,
                    minLife: 0.38f, maxLife: 0.65f,
                    minSpeed: 0.3f, maxSpeed: 1.0f,
                    minSize: 0.28f, maxSize: 0.44f,
                    radius: 0.55f,
                    gravity: -0.35f,
                    upwardVel: 1.8f,
                    noiseStr: 0.35f,
                    randomRot: false,
                    colorA: new Color(0.88f, 1f, 0.88f, 1f),
                    colorB: new Color(0.22f, 0.96f, 0.46f, 1f),
                    endColor: new Color(0.08f, 0.62f, 0.26f, 0f),
                    sizeCurve: new AnimationCurve(
                        new Keyframe(0f, 0.65f),
                        new Keyframe(0.25f, 1.1f),
                        new Keyframe(1f, 0.3f)
                    )
                );

                // 회복 링 펄스
                ringPs.transform.position = healCenter;
                ConfigureBurstPs(
                    ringPs,
                    ringMat,
                    sortingOrder: 260,
                    minLife: 0.28f, maxLife: 0.36f,
                    minSpeed: 0f, maxSpeed: 0f,
                    minSize: 1.0f, maxSize: 1.25f,
                    radius: 0.02f,
                    gravity: 0f,
                    upwardVel: 0f,
                    noiseStr: 0f,
                    randomRot: true,
                    colorA: new Color(0.82f, 1f, 0.86f, 0.9f),
                    colorB: new Color(0.20f, 0.95f, 0.44f, 0.9f),
                    endColor: new Color(0.06f, 0.55f, 0.24f, 0f),
                    sizeCurve: new AnimationCurve(
                        new Keyframe(0f, 0.4f),
                        new Keyframe(1f, 1.45f)
                    )
                );

                subPs.Play(false);
                subPs.Emit(7);
                ringPs.Play(false);
                ringPs.Emit(1);
            }

            routine = StartCoroutine(GreenDrainRoutine(targetCenter, playerTransform));
        }

        public void PlaySpiritHealBurst(Vector3 bossCenter, float bossRadius, Color spiritAccent)
        {
            EnsureBuilt();
            StopRunning();

            float scale = Mathf.Clamp(bossRadius * 0.85f, 0.8f, 2.0f);
            transform.position = bossCenter;

            // 1. 나비가 흡수되며 흩어지는 정령 가루 스파크
            ConfigureBurstPs(
                mainPs,
                diamondMat,
                sortingOrder: 261,
                minLife: 0.28f, maxLife: 0.54f,
                minSpeed: 1.6f * scale, maxSpeed: 4.4f * scale,
                minSize: 0.18f, maxSize: 0.36f,
                radius: 0.45f * scale,
                gravity: -0.15f,
                upwardVel: 0.65f,
                noiseStr: 0.95f,
                randomRot: true,
                colorA: Color.Lerp(spiritAccent, Color.white, 0.45f),
                colorB: spiritAccent,
                endColor: new Color(0.06f, 0.48f, 0.20f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 0.7f),
                    new Keyframe(0.25f, 1.15f),
                    new Keyframe(1f, 0f)
                )
            );

            // 2. 보스 몸체 주변으로 피어오르는 '+' 회복 십자 파티클
            ConfigureBurstPs(
                subPs,
                plusMat,
                sortingOrder: 262,
                minLife: 0.40f, maxLife: 0.68f,
                minSpeed: 0.35f, maxSpeed: 1.1f,
                minSize: 0.30f, maxSize: 0.48f,
                radius: 0.65f * scale,
                gravity: -0.18f,
                upwardVel: 0.9f,
                noiseStr: 0.35f,
                randomRot: false,
                colorA: new Color(0.90f, 1f, 0.90f, 1f),
                colorB: new Color(0.22f, 0.96f, 0.46f, 1f),
                endColor: new Color(0.08f, 0.60f, 0.24f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 0.65f),
                    new Keyframe(0.28f, 1.1f),
                    new Keyframe(1f, 0.3f)
                )
            );

            // 3. 회복 파동 링
            ConfigureBurstPs(
                ringPs,
                ringMat,
                sortingOrder: 260,
                minLife: 0.28f, maxLife: 0.38f,
                minSpeed: 0f, maxSpeed: 0f,
                minSize: 1.2f * scale, maxSize: 1.6f * scale,
                radius: 0.02f,
                gravity: 0f,
                upwardVel: 0f,
                noiseStr: 0f,
                randomRot: true,
                colorA: Color.Lerp(spiritAccent, Color.white, 0.35f),
                colorB: new Color(0.20f, 0.95f, 0.44f, 0.9f),
                endColor: new Color(0.06f, 0.55f, 0.24f, 0f),
                sizeCurve: new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(1f, 1.5f)
                )
            );

            mainPs.Play(false);
            mainPs.Emit(20);
            subPs.Play(false);
            subPs.Emit(9);
            ringPs.Play(false);
            ringPs.Emit(2);

            routine = StartCoroutine(ReturnAfterDelay(0.75f));
        }

        private static Vector3 ResolveTargetCenter(Transform target)
        {
            if (target == null) return Vector3.zero;
            if (target.TryGetComponent<Collider2D>(out var col) && col.enabled)
            {
                return col.bounds.center;
            }
            return target.position + Vector3.up * 0.3f;
        }

        public void ForceReturnNow()
        {
            StopRunning();
            ClearAllSystems();
            ReleaseInstance(this);
        }

        private IEnumerator GreenDrainRoutine(Vector3 startCenter, Transform playerTransform)
        {
            const int orbCount = 5;
            const float travelDuration = 0.28f;

            if (playerTransform == null)
            {
                yield return new WaitForSeconds(0.55f);
                routine = null;
                ClearAllSystems();
                ReleaseInstance(this);
                yield break;
            }

            EnsureDrainOrbs(orbCount);

            for (int i = 0; i < orbCount; i++)
            {
                OrbTrailItem item = drainOrbs[i];
                float angle = (i / (float)orbCount) * Mathf.PI * 2f + Random.Range(-0.35f, 0.35f);
                item.startPos = startCenter + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.35f;
                item.controlOffset = new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(0.8f, 2.2f), 0f);
                item.tf.position = item.startPos;
                item.tf.gameObject.SetActive(true);
                item.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                item.ps.Play(false);
                item.ps.Emit(3);
                drainOrbs[i] = item;
            }

            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float eased = t * t * (3f - 2f * t);
                Vector3 targetPos = playerTransform != null ? ResolveTargetCenter(playerTransform) : startCenter;

                for (int i = 0; i < orbCount; i++)
                {
                    OrbTrailItem item = drainOrbs[i];
                    Vector3 mid = Vector3.Lerp(item.startPos, targetPos, 0.5f) + item.controlOffset;
                    Vector3 a = Vector3.Lerp(item.startPos, mid, eased);
                    Vector3 b = Vector3.Lerp(mid, targetPos, eased);
                    item.tf.position = Vector3.Lerp(a, b, eased);
                }

                yield return null;
            }

            // 흡수 오브가 대상에 도착하는 시점에 추가 '+' 회복 버스트
            if (playerTransform != null && subPs != null)
            {
                subPs.transform.position = ResolveTargetCenter(playerTransform);
                subPs.Emit(6);
            }

            for (int i = 0; i < orbCount; i++)
            {
                drainOrbs[i].ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            yield return new WaitForSeconds(0.55f);

            for (int i = 0; i < orbCount; i++)
            {
                drainOrbs[i].tf.gameObject.SetActive(false);
            }

            routine = null;
            ClearAllSystems();
            ReleaseInstance(this);
        }

        private void EnsureDrainOrbs(int count)
        {
            while (drainOrbs.Count < count)
            {
                GameObject go = new GameObject($"DrainOrb_{drainOrbs.Count}");
                go.SetActive(false);
                go.transform.SetParent(transform, false);

                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.duration = 1f;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.20f, 0.32f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.88f, 1f, 0.90f, 1f),
                    new Color(0.22f, 0.96f, 0.46f, 1f)
                );
                main.maxParticles = 24;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 38f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.08f;

                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = BuildColorGradient(
                    new Color(0.90f, 1f, 0.92f, 1f),
                    new Color(0.20f, 0.95f, 0.45f, 0.95f),
                    new Color(0.06f, 0.55f, 0.24f, 0f)
                );

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

                ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
                if (pr != null)
                {
                    pr.renderMode = ParticleSystemRenderMode.Billboard;
                    pr.sortingOrder = 262;
                    if (diamondMat != null) pr.sharedMaterial = diamondMat;
                }

                drainOrbs.Add(new OrbTrailItem { tf = go.transform, ps = ps });
            }
        }

        private IEnumerator ReturnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            routine = null;
            ClearAllSystems();
            ReleaseInstance(this);
        }

        private void StopRunning()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            for (int i = 0; i < drainOrbs.Count; i++)
            {
                if (drainOrbs[i].tf != null)
                {
                    drainOrbs[i].ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    drainOrbs[i].tf.gameObject.SetActive(false);
                }
            }
        }

        private void ClearAllSystems()
        {
            if (mainPs != null)
            {
                mainPs.transform.localPosition = Vector3.zero;
                mainPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (subPs != null)
            {
                subPs.transform.localPosition = Vector3.zero;
                subPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (ringPs != null)
            {
                ringPs.transform.localPosition = Vector3.zero;
                ringPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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

        private static void ConfigureBurstPs(
            ParticleSystem ps,
            Material mat,
            int sortingOrder,
            float minLife, float maxLife,
            float minSpeed, float maxSpeed,
            float minSize, float maxSize,
            float radius,
            float gravity,
            float upwardVel,
            float noiseStr,
            bool randomRot,
            Color colorA,
            Color colorB,
            Color endColor,
            AnimationCurve sizeCurve)
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = randomRot
                ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
                : new ParticleSystem.MinMaxCurve(0f, 0f);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.02f, radius);
            shape.radiusThickness = 1f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = Mathf.Abs(upwardVel) > 0.001f;
            if (vel.enabled)
            {
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(upwardVel * 0.65f, upwardVel * 1.25f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            var noise = ps.noise;
            noise.enabled = noiseStr > 0.01f;
            if (noise.enabled)
            {
                noise.strength = noiseStr;
                noise.frequency = 2.0f;
                noise.scrollSpeed = 1.2f;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = BuildColorGradient(colorA, Color.Lerp(colorA, colorB, 0.5f), endColor);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr != null)
            {
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                pr.sortingOrder = sortingOrder;
                if (mat != null) pr.sharedMaterial = mat;
            }
        }
    }

    private static PoolHost host;
    private static readonly Queue<EffectInstance> pool = new Queue<EffectInstance>();
    private static readonly List<EffectInstance> activeInstances = new List<EffectInstance>();

    private static Texture2D diamondTex;
    private static Texture2D starTex;
    private static Texture2D plusTex;
    private static Texture2D ringTex;
    private static Material diamondMat;
    private static Material starMat;
    private static Material plusMat;
    private static Material ringMat;

    public static void SpawnRedPowerHit(Vector3 targetCenter, float targetRadius)
    {
        EnsureHostAndAssets();
        EffectInstance inst = AcquireInstance();
        if (inst == null) return;

        inst.gameObject.SetActive(true);
        activeInstances.Add(inst);
        inst.PlayRedPowerHit(targetCenter, targetRadius);
    }

    public static void SpawnGreenHealHit(Vector3 targetCenter, float targetRadius, Transform playerTransform)
    {
        EnsureHostAndAssets();
        EffectInstance inst = AcquireInstance();
        if (inst == null) return;

        inst.gameObject.SetActive(true);
        activeInstances.Add(inst);
        inst.PlayGreenHealHit(targetCenter, targetRadius, playerTransform);
    }

    public static void SpawnSpiritHealBurst(Vector3 bossCenter, float bossRadius, Color spiritAccent)
    {
        EnsureHostAndAssets();
        EffectInstance inst = AcquireInstance();
        if (inst == null) return;

        inst.gameObject.SetActive(true);
        activeInstances.Add(inst);
        inst.PlaySpiritHealBurst(bossCenter, bossRadius, spiritAccent);
    }

    private static void EnsureHostAndAssets()
    {
        if (host == null)
        {
            GameObject hostGo = new GameObject("SkillHitParticlesHost");
            Object.DontDestroyOnLoad(hostGo);
            host = hostGo.AddComponent<PoolHost>();
        }

        if (diamondTex == null) diamondTex = CreateDiamondTexture();
        if (starTex == null) starTex = CreateStarSlashTexture();
        if (plusTex == null) plusTex = CreatePlusTexture();
        if (ringTex == null) ringTex = CreateRingTexture();

        if (diamondMat == null) diamondMat = CreateMat("SkillHit_DiamondMat", diamondTex);
        if (starMat == null) starMat = CreateMat("SkillHit_StarMat", starTex);
        if (plusMat == null) plusMat = CreateMat("SkillHit_PlusMat", plusTex);
        if (ringMat == null) ringMat = CreateMat("SkillHit_RingMat", ringTex);
    }

    private static EffectInstance AcquireInstance()
    {
        while (pool.Count > 0)
        {
            EffectInstance pooled = pool.Dequeue();
            if (pooled != null) return pooled;
        }

        if (host == null) return null;

        GameObject go = new GameObject("SkillHitEffectInstance");
        go.SetActive(false);
        go.transform.SetParent(host.transform, false);
        EffectInstance inst = go.AddComponent<EffectInstance>();
        inst.EnsureBuilt();
        return inst;
    }

    private static void ReleaseInstance(EffectInstance inst)
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
            if (activeInstances[i] != null)
            {
                activeInstances[i].ForceReturnNow();
            }
        }
        activeInstances.Clear();
    }

    private static Gradient BuildColorGradient(Color start, Color mid, Color end)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(mid, 0.42f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a * 0.9f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return grad;
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

    private static Texture2D CreateStarSlashTexture()
    {
        const int size = 11;
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
                bool axisRay = (ax == 0 && ay <= 5) || (ay == 0 && ax <= 5);
                bool diagRay = (ax == ay && ax <= 4);
                bool core = (ax <= 1 && ay <= 1);
                float a = (axisRay || diagRay || core) ? 1f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D CreatePlusTexture()
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
                bool isPlus = (ax <= 1 && ay <= 3) || (ay <= 1 && ax <= 3);
                pixels[y * size + x] = new Color(1f, 1f, 1f, isPlus ? 1f : 0f);
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
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                bool isRing = dist >= 5.2f && dist <= 7.0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, isRing ? 1f : 0f);
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
