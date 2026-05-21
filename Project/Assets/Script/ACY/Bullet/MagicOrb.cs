using System.Collections;
using UnityEngine;

public class MagicOrb : MonoBehaviour, IDamageable
{
    [Header("공전 설정")]
    [SerializeField] private float orbitSpeed = 90f;      // 공전 속도 (도/초)
    [SerializeField] private float bobSpeed = 2f;         // 둥실 속도
    [SerializeField] private float bobAmount = 0.2f;      // 둥실 진폭

    [Header("발사 설정")]
    [SerializeField] private float launchSpeed = 15f;     // 돌진 속도
    [SerializeField] private float arrivalDistance = 0.3f;// 도달 판정 거리

    [Header("데미지")]
    [SerializeField] private int damage = 1;

    private const string POOL_KEY = "RedBossOrb";

    private Transform pivot;          // 보스 위치 기준점
    private float orbitAngle;         // 현재 공전 각도
    private float orbitRadius;        // 공전 반지름
    private float bobTimer;

    private bool isLaunched = false;
    private Vector3 targetPos;        // 발사 시 플레이어 위치 (고정)

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    // 소환 시 초기화
    public void Init(Transform bossPivot, float angle, float radius)
    {
        pivot = bossPivot;
        orbitAngle = angle;
        orbitRadius = radius;
        bobTimer = 0f;
        isLaunched = false;

        if (col != null) col.enabled = true;

        // 초기 위치 세팅
        UpdateOrbitPosition();
    }

    // 보스가 호출 → 발사
    public void Launch(Vector3 target)
    {
        isLaunched = true;
        targetPos = target;
    }

    private void Update()
    {
        if (isLaunched)
        {
            MoveLaunched();
        }
        else
        {
            UpdateOrbit();
        }
    }

    // 공전 + 둥실
    private void UpdateOrbit()
    {
        if (pivot == null) return;

        orbitAngle += orbitSpeed * Time.deltaTime;
        bobTimer += bobSpeed * Time.deltaTime;

        UpdateOrbitPosition();
    }

    private void UpdateOrbitPosition()
    {
        if (pivot == null) return;

        float rad = orbitAngle * Mathf.Deg2Rad;
        float bob = Mathf.Sin(bobTimer) * bobAmount;

        Vector3 offset = new Vector3
        (
            Mathf.Cos(rad) * orbitRadius,
            Mathf.Sin(rad) * orbitRadius + bob,
            0f
        );

        transform.position = pivot.position + offset;
    }

    // 목표 위치로 돌진
    private void MoveLaunched()
    {
        transform.position = Vector3.MoveTowards
        (
            transform.position,
            targetPos,
            launchSpeed * Time.deltaTime
        );

        // 목표 위치 도달 시 소멸
        if (Vector3.Distance(transform.position, targetPos) <= arrivalDistance)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLaunched) return; // 공전 중엔 충돌 무시

        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            ReturnToPool();
        }
    }

    // 플레이어 공격으로 격추 가능
    public void TakeDamage(int damage) => ReturnToPool();

    private void ReturnToPool()
    {
        if (col != null) col.enabled = false;
        PoolingManager.Instance.Return(POOL_KEY, gameObject);
    }
}