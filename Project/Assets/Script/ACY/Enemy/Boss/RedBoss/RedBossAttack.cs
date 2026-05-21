using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RedBossAttack : MonoBehaviour, IStunnable, IHitReaction
{
    [Header("공통")]
    [SerializeField] private Transform firePoint;

    [Header("화살 설정")]
    [SerializeField] private float fireInterval = 0.3f;   // 3발 사이 시간차
    [SerializeField] private float angleSpread = 15f;     // 각도 차이
    [SerializeField] private float attackCooldown = 3f;   // 공격 간격 ← 추가

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

    private bool isCastingMeteor = false;
    private bool isStunned = false;

    private SpriteRenderer sr;
    private Color originalColor;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();

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
            yield return StartCoroutine(AttackMeteor());     
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

        yield return StartCoroutine(ShowMeteorCastUI());

        // 캐스팅 중 스턴당했으면 취소
        if (isStunned)
        {
            isCastingMeteor = false;
            yield break;
        }

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
    public void ApplyStun(float duration)
    {
        if (isStunned)
        {
            return;
        }

        StopAllCoroutines();
        StartCoroutine(StunRoutine(duration));
    }
    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        isCastingMeteor = false;

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
    // ---------------------공격패턴 3 : 레이저 (나중에 구현)-----------------------
    public IEnumerator AttackLaser()
    {
        yield break;
    }
}