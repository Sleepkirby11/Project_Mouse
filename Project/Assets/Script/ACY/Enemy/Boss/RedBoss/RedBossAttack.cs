using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RedBossAttack : MonoBehaviour, IStunnable, IHitReaction
{
    [Header("공통")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform pivotPoint;

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
    private bool hasClones = false;

    [Header("레이저 설정")]
    [SerializeField] private GameObject laserCrossPrefab;
    [SerializeField] private Transform laserSpawnPoint;
    private const string LASER_KEY = "RedBossLaser";

    [SerializeField] private float laserWarningTime = 1.5f;
    [SerializeField] private float laserDuration = 4f;

    [Header("구체 설정")]
    [SerializeField] private float orbOrbitRadius = 2f;    // 공전 반지름
    [SerializeField] private int orbCount = 3;
    [SerializeField] private float orbWaitTime = 2f;       // 발사 전 대기
    private const string ORB_KEY = "RedBossOrb";

    private GameObject[] currentClones;

    private bool isCastingMeteor = false;
    private bool isStunned = false;

    public bool IsStunned => isStunned;
    public bool IsCastingMeteor => isCastingMeteor;

    private SpriteRenderer sr;
    private Color originalColor;
    private Animator anim;

    private void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        if (sr != null)
        {
            originalColor = sr.color;
        }

        if (meteorCastSlider != null)
        {
            meteorCastSlider.gameObject.SetActive(false);
        }
        currentClones = new GameObject[teleportPoints.Length];
        StartCoroutine(AttackRoutine());
    }

    // 일정 간격으로 화살 발사
    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(attackCooldown);
            yield return StartCoroutine(AttackArrow());

            yield return new WaitForSeconds(attackCooldown);
            yield return StartCoroutine(AttackOrb());

            yield return new WaitForSeconds(attackCooldown);
            yield return StartCoroutine(AttackMeteor());

            yield return new WaitForSeconds(attackCooldown);
            yield return StartCoroutine(AttackLaser());
        }
    }

    // ------------------------공격패턴 1 : 유도 화살-------------------------
    public IEnumerator AttackArrow()
    {
        if (anim != null)
        {
            anim.SetTrigger("Shoot");
        }

        yield return new WaitForSeconds(1.2f);
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
    //---------------공격 패턴 2 : 마법 구체 -------------------
    public IEnumerator AttackOrb()
    {
        if (anim != null)
        {
            anim.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(0.9f); 

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            yield break;
        }

        // 구체 3개를 120도 간격으로 소환
        MagicOrb[] orbs = new MagicOrb[3];
        Vector3 spawnPos = pivotPoint != null ? pivotPoint.position : transform.position;

        for (int i = 0; i < orbCount; i++)
        {
            float angle = i * (360f / orbCount);

            GameObject obj = PoolingManager.Instance.Get(ORB_KEY, spawnPos, Quaternion.identity);

            if (obj == null)
            {
                continue;
            }

            MagicOrb orb = obj.GetComponent<MagicOrb>();
            orb?.Init(pivotPoint != null ? pivotPoint : transform, angle, orbOrbitRadius);
            orbs[i] = orb;
        }

        // 대기 (공전 연출)
        yield return new WaitForSeconds(orbWaitTime);

        // 발사 시점의 플레이어 위치 고정 후 동시 발사
        Vector3 targetPos = player.transform.position;

        if (anim != null)
        {
            anim.SetTrigger("Shoot");
        }
        for (int i = 0; i < orbs.Length; i++)
        {
            if (orbs[i] == null)
            {
                continue;
            }
            orbs[i].Launch(targetPos);
        }

        // 구체 비행 시간만큼 대기 후 다음 패턴
        yield return new WaitForSeconds(2f);
    }

    // ----------------------공격패턴 3 : 메테오 ---------------------------
    public IEnumerator AttackMeteor()
    {
        isCastingMeteor = true;

        if (anim != null)
        {
            anim.SetBool("IsCasting", true);
        }

        SpawnClone();

        yield return StartCoroutine(ShowMeteorCastUI());

        // 캐스팅 중 스턴당했으면 취소
        if (isStunned)
        {
            isCastingMeteor = false;
            if (anim != null)
            {
                anim.SetBool("IsCasting", false);
            }
            RemoveClone();
            yield break;
        }
        RemoveClone();
        isCastingMeteor = false;

        if (anim != null)
        {
            anim.SetBool("IsCasting", false);
        }

        yield return null;

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

        float diagonalOffset = 3f;

        for (int i = 0; i < meteorPerWave; i++)
        {
            float sectionMinX = meteorSpawnXRange.x + sectionWidth * i;
            float sectionMaxX = sectionMinX + sectionWidth;

            float randomX = Random.Range(sectionMinX, sectionMaxX);
            float randomY = Random.Range(meteorSpawnYRange.x, meteorSpawnYRange.y);

            Vector3 targetPos = new Vector3(randomX, meteorTargetY, 0f);

            Vector3 spawnPos = new Vector3(randomX - diagonalOffset, randomY, 0f);

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
        if (anim != null)
        {
            anim.SetBool("IsCasting", false);
        }

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
        if (!hasClones)
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

        hasClones = true;

        System.Array.Clear(currentClones, 0, currentClones.Length);

        for (int i = 0; i < teleportPoints.Length; i++)
        {
            Vector3 spawnPos = teleportPoints[i].position;

            if (Vector3.Distance(spawnPos, transform.position) < 0.5f)
            {
                continue;
            }

            GameObject cloneObj = PoolingManager.Instance.Get("RedBossClone", spawnPos, Quaternion.identity);

            if (cloneObj == null)
            {
                continue;
            }

            RedBossClone clone = cloneObj.GetComponent<RedBossClone>();

            if (clone != null)
            {
                clone.Init(this, cloneSpawnVFX, cloneDisappearVFX);
                clone.PlayCastAnimation();
            }

            currentClones[i] = cloneObj;
        }
    }
    public void SwapCloneAndBoss(int oldIndex, int newIndex)
    {
        if (!isCastingMeteor || currentClones == null) // 분신이 없는 상태면 스왑 불가
        {
            return;
        }

        if (newIndex >= 0 && newIndex < currentClones.Length && currentClones[newIndex] != null) // 이동할 위치에 분신이 있으면 제거
        {
            currentClones[newIndex].GetComponent<RedBossClone>()?.DestroyClone(); // 분신 제거
            currentClones[newIndex] = null; // 배열에서 제거
        }

        if (oldIndex >= 0 && oldIndex < teleportPoints.Length)
        {
            Vector3 spawnPos = teleportPoints[oldIndex].position;
            GameObject cloneObj = PoolingManager.Instance.Get("RedBossClone", spawnPos, Quaternion.identity);

            if (cloneObj != null)
            {
                RedBossClone clone = cloneObj.GetComponent<RedBossClone>();
                clone?.Init(this, cloneSpawnVFX, cloneDisappearVFX);

                if (anim != null)
                {
                    AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                    clone?.SyncAnimation(stateInfo.fullPathHash, stateInfo.normalizedTime);
                }
                else
                {
                    clone?.PlayCastAnimation();
                }

                currentClones[oldIndex] = cloneObj;
            }
        }
    }

    public void RemoveClone()
    {
        if (currentClones == null)
        {
            return;
        }
        for (int i = 0; i < currentClones.Length; i++)
        {
            if (currentClones[i] == null) continue;

            currentClones[i].GetComponent<RedBossClone>()?.DestroyClone();
            currentClones[i] = null;
        }

        hasClones = false;
    }
    // ---------------------공격패턴 4 : 레이저-----------------------
    public IEnumerator AttackLaser()
    {
        GameObject laserObj = PoolingManager.Instance.Get(LASER_KEY, laserSpawnPoint.position, Quaternion.identity);

        LaserCross laser = laserObj.GetComponent<LaserCross>();

        if (laser != null)
        {
            laser.Init(laserWarningTime, laserDuration);
        }

        yield return new WaitForSeconds(laserWarningTime + laserDuration);
    }
}