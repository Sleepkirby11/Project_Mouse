using System.Collections;
using UnityEngine;

public class MagicOrb : MonoBehaviour
{
    [Header("공전 설정")]
    [SerializeField] private float orbitSpeed = 90f;      // 공전 속도
    [SerializeField] private float bobSpeed = 2f;         // 뜨는 속도
    [SerializeField] private float bobAmount = 0.2f;      // 뜨는 높이

    [Header("발사 설정")]
    [SerializeField] private float launchSpeed = 20f;     //  속도
    [SerializeField] private float maxLaunchDuration = 5f;// 최대 생존 시간
    
    [Header("대미지")]
    [SerializeField] private int damage = 1;

    private const string POOL_KEY = "RedBossOrb";

    private Transform pivot;          // 보스 위치 기준점
    private float orbitAngle;         // 현재 공전 각도
    private float orbitRadius;        // 공전 반지름
    private float bobTimer;

    private bool isLaunched = false;
    private Vector3 launchDirection;   // 발사 시 결정된 직선 이동 방향
    private float launchTimer = 0f;

    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    // 소환 및 공전 초기화
    public void Init(Transform centerPivot, float angle, float radius)
    {
        pivot =  centerPivot;
        orbitAngle = angle;
        orbitRadius = radius;
        bobTimer = 0f;
        isLaunched = false;
        launchTimer = 0f;

        if (col != null)
        {
            col.enabled = true;
        }

        // 초기 위치 세팅
        UpdateOrbitPosition();
    }

    public void Launch(Vector3 playerPosition)
    {
        isLaunched = true;
        launchTimer = 0f;

        launchDirection = (playerPosition - transform.position).normalized;
    }

    private void Update()
    {
        if (isLaunched)
        {
            MoveStraight();
        }
        else
        {
            UpdateOrbit();
        }
    }

    // 공전 + 둥실거리는 효과
    private void UpdateOrbit()
    {
        if (pivot == null)
        {
            return;
        }

        orbitAngle += orbitSpeed * Time.deltaTime;
        bobTimer += bobSpeed * Time.deltaTime;

        UpdateOrbitPosition();
    }

    private void UpdateOrbitPosition()
    {
        if (pivot == null)
        {
            return;
        }

        float rad = orbitAngle * Mathf.Deg2Rad;
        float bob = Mathf.Sin(bobTimer) * bobAmount;

        Vector3 offset = new Vector3(Mathf.Cos(rad) * orbitRadius, Mathf.Sin(rad) * orbitRadius + bob, 0f);

        transform.position = pivot.position + offset;
    }

    private void MoveStraight()
    {
        launchTimer += Time.deltaTime;

        if (launchTimer >= maxLaunchDuration)
        {
            ReturnToPool();
            return;
        }

        transform.position += launchDirection * launchSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLaunched)
        {
            return;
        }
        if (other.CompareTag("Player"))
        {
            other.GetComponent<IDamageable>()?.TakeDamage(damage);
            ReturnToPool();
        }  
    }

    private void ReturnToPool()
    {
        if (col != null)
        {
            col.enabled = false;
        }
        PoolingManager.Instance.Return(POOL_KEY, gameObject);
    }
}