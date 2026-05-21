using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RedBossAttack : MonoBehaviour, IStunnable, IHitReaction
{
    [Header("공통")]
    [SerializeField] private Transform firePoint;

    [Header("화살 설정")]
    [SerializeField] private float angleSpread = 15f;     // 각도 차이
    [SerializeField] private float attackCooldown = 3f;   // 공격 간격 

    private const string ARROW_KEY = "RedBossArrow";


    [Header("캐스팅 UI")]
    [SerializeField] private Slider meteorCastSlider;

    [Header("메테오 설정")]
    [SerializeField] private int meteorWaveCount = 3;
    [SerializeField] private int meteorPerWave = 5;
    [SerializeField] private float waveInterval = 0.5f;
    [SerializeField] private float meteorCastTime = 2f;
    [SerializeField] private Vector2 meteorSpawnXRange = new Vector2(-6f, 6f);
    [SerializeField] private Vector2 meteorSpawnYRange = new Vector2(6f, 9f);
    [SerializeField] private float meteorTargetY = -2.5f;
    private const string METEOR_KEY = "RedBossMeteor";

    [Header("스턴 설정")]
    [SerializeField] private float stunDuration = 2f;

    [Header("분신 설정")]
    [SerializeField] private Transform[] teleportPoints;
    [SerializeField] private GameObject cloneDisappearVFX;
    [SerializeField] private GameObject cloneSpawnVFX;

    [Header("레이저 설정")]
    [SerializeField] private GameObject laserCrossPrefab;
    [SerializeField] private Transform laserSpawnPoint;

    [SerializeField] private float laserWarningTime = 1.5f;
    [SerializeField] private float laserDuration = 4f;

    [Header("구체 설정")]
    [SerializeField] private float orbOrbitRadius = 2f;    // 공전 반지름
    [SerializeField] private int orbCount = 3;
    [SerializeField] private float orbWaitTime = 2f;       // 발사 전 대기
    private const string ORB_KEY = "RedBossOrb";

    private List<GameObject> currentClones = new List<GameObject>();

    private bool isCastingMeteor = false;
    private bool isStunned = false;

    public bool IsStunned => isStunned;

    private SpriteRenderer sr;
    private Color originalColor;

    private void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            originalColor = sr.color;
        }

        if (meteorCastSlider != null)
        {
            meteorCastSlider.gameObject.SetActive(false);
        }

        StartCoroutine(AttackRoutine());
    }

    // 일정 간격으로 화살 발사
    private IEnumerator AttackRoutine()
    {
        //while (true)
        //{
        //    yield return new WaitForSeconds(attackCooldown); // 대기
        //    yield return StartCoroutine(AttackArrow());      // 3연발 발사
        //}
        while (true)
        {
            yield return new WaitForSeconds(attackCooldown); // 대기
            yield return StartCoroutine(AttackOrb());     
        }
    }

    // ------------------------공격패턴 1 : 화살 3연발-------------------------
    public IEnumerator AttackArrow()
    {
        SpawnArrow(0f);              // 가운데
        SpawnArrow(-angleSpread);    // 아래쪽/왼쪽 방향
        SpawnArrow(angleSpread);     // 위쪽/오른쪽 방향

        yield break;
    }

    private void SpawnArrow(float angleOffset)
    {
        GameObject obj = PoolingManager.Instance.Get(ARROW_KEY, firePoint.position, Quaternion.identity);
        if (obj == null)
        {
            return;
        }

        obj.GetComponent<FireArrow>()?.Init(angleOffset);
    }

    // ----------------------공격패턴 2 : 메테오 ---------------------------
    public IEnumerator AttackMeteor()
    {
        isCastingMeteor = true;

        SpawnClone();

        yield return StartCoroutine(ShowMeteorCastUI());

        // 캐스팅 중 스턴당했으면 취소
        if (isStunned)
        {
            isCastingMeteor = false;
            RemoveClone();
            yield break;
        }

        RemoveClone();

        for (int wave = 0; wave < meteorWaveCount; wave++)
        {
            SpawnMeteorWave();

            yield return new WaitForSeconds(waveInterval);
        }
        isCastingMeteor = false;
    }

    private void SpawnMeteorWave()
    {
        float totalWidth = meteorSpawnXRange.y - meteorSpawnXRange.x;
        float sectionWidth = totalWidth / meteorPerWave;

        for (int i = 0; i < meteorPerWave; i++)
        {
            float sectionMinX = meteorSpawnXRange.x + sectionWidth * i;
            float sectionMaxX = sectionMinX + sectionWidth;

            float randomX = Random.Range(sectionMinX, sectionMaxX);
            float randomY = Random.Range(meteorSpawnYRange.x, meteorSpawnYRange.y);

            Vector3 spawnPos = new Vector3(randomX, randomY, 0f);
            Vector3 targetPos = new Vector3(randomX, meteorTargetY, 0f);

            GameObject obj = PoolingManager.Instance.Get(METEOR_KEY, spawnPos, Quaternion.identity);
            if (obj != null)
            {
                obj.GetComponent<Meteor>()?.Init(targetPos);
            }
        }
    }
    private IEnumerator ShowMeteorCastUI()
    {
        if (meteorCastSlider == null)
        {
            yield break;
        }

        meteorCastSlider.gameObject.SetActive(true);
        meteorCastSlider.maxValue = meteorCastTime;
        meteorCastSlider.value = 0f;

        float timer = 0f;

        while (timer < meteorCastTime)
        {
            timer += Time.deltaTime;
            meteorCastSlider.value = timer;
            yield return null;
        }

        meteorCastSlider.value = meteorCastTime;
        meteorCastSlider.gameObject.SetActive(false);
    }
    //---------------캐스팅 중 파훼 --------------
    public void ApplyStun(float duration)
    {
        if (isStunned)
        {
            return;
        }

        isStunned = true;
        isCastingMeteor = false;

        // 남은 분신 제거
        RemoveClone();

        StopAllCoroutines();
        StartCoroutine(StunRoutine(duration));
    }
    private IEnumerator StunRoutine(float duration)
    {
        if (meteorCastSlider != null)
        {
            meteorCastSlider.gameObject.SetActive(false);
        }

        if (sr != null)
        {
            sr.color = Color.yellow;
        }

        yield return new WaitForSeconds(duration);

        if (sr != null)
        {
            sr.color = originalColor;
        }

        isStunned = false;

        StartCoroutine(AttackRoutine());
    }
    public void OnHitDuringCast()
    {
        if (!isCastingMeteor || isStunned)
        {
            return;
        }

        ApplyStun(stunDuration);
    }
    public bool OnBeforeTakeDamage(EnemyStatus enemy, int damage)
    {
        return false;
    }

    public void OnAfterTakeDamage(EnemyStatus enemy, int damage)
    {
        OnHitDuringCast();
    }
    //-------------캐스팅 중 분신------------
    private void SpawnClone()
    {
        if (teleportPoints.Length == 0)
        {
            return;
        }

        currentClones.Clear();

        for (int i = 0; i < teleportPoints.Length; i++)
        {
            Vector3 spawnPos = teleportPoints[i].position;

            GameObject cloneObj = PoolingManager.Instance.Get
            (
                "RedBossClone",
                spawnPos,
                Quaternion.identity
            );

            if (cloneObj == null)
            {
                continue;
            }

            RedBossClone clone = cloneObj.GetComponent<RedBossClone>();

            if (clone != null)
            {
                clone.Init(this, cloneSpawnVFX, cloneDisappearVFX);
            }

            currentClones.Add(cloneObj);
        }
    }

    public void RemoveClone()
    {
        for (int i = 0; i < currentClones.Count; i++)
        {
            if (currentClones[i] == null)
            {
                continue;
            }

            RedBossClone clone =
                currentClones[i].GetComponent<RedBossClone>();

            if (clone != null)
            {
                clone.DestroyClone();
            }
        }

        currentClones.Clear();
    }
    // ---------------------공격패턴 3 : 레이저-----------------------
    public IEnumerator AttackLaser()
    {
        GameObject laserObj = Instantiate
        (
            laserCrossPrefab,
            laserSpawnPoint.position,
            Quaternion.identity
        );

        LaserCross laser =
            laserObj.GetComponent<LaserCross>();

        if (laser != null)
        {
            laser.Init
            (
                laserWarningTime,
                laserDuration
            );
        }

        yield return new WaitForSeconds
        (
            laserWarningTime + laserDuration
        );
    }
    //---------------구체 패턴-------------------
    public IEnumerator AttackOrb()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) yield break;

        // 구체 3개를 120도 간격으로 소환
        MagicOrb[] orbs = new MagicOrb[3];

        for (int i = 0; i < orbCount; i++)
        {
            float angle = i * (360f / orbCount);

            GameObject obj = PoolingManager.Instance.Get
            (
                ORB_KEY,
                transform.position,
                Quaternion.identity
            );

            if (obj == null) continue;

            MagicOrb orb = obj.GetComponent<MagicOrb>();
            orb?.Init(transform, angle, orbOrbitRadius);
            orbs[i] = orb;
        }

        // 대기 (공전 연출)
        yield return new WaitForSeconds(orbWaitTime);

        // 발사 시점의 플레이어 위치 고정 후 동시 발사
        Vector3 targetPos = player.transform.position;

        for (int i = 0; i < orbs.Length; i++)
        {
            if (orbs[i] == null) continue;
            orbs[i].Launch(targetPos);
        }

        // 구체 비행 시간만큼 대기 후 다음 패턴
        yield return new WaitForSeconds(2f);
    }
}