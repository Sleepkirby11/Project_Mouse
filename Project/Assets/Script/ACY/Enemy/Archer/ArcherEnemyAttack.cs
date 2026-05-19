using UnityEngine;
//공격패턴: 일정 간격으로 플레이어에게 화살을 발사
public class ArcherEnemyAttack : MonoBehaviour
{
    [Header("공격 설정")]
    [SerializeField] private GameObject arrowPrefab; // 화살 프리팹
    [SerializeField] private Transform firePoint; // 화살 발사 위치
    [SerializeField] private float attackCooldown = 2f; // 공격 간격
    [SerializeField] private float arrowHeight = 3f; // 화살 포물선 높이
    [SerializeField] private float arrowDuration = 1.2f; // 화살 이동 시간

    private ArcherEnemyMove archerMove;
    private float nextAttackTime; // 다음 공격 가능 시간

    private void Awake()
    {
        // 같은 오브젝트 내 이동 컨트롤러 캐싱
        archerMove = GetComponent<ArcherEnemyMove>();
    }

    private void Update()
    {
        Transform target = archerMove.TargetPlayer;

        // 타겟이 있고, 백스텝 중이 아닐 때만 공격 시도
        if (target != null && !archerMove.IsBackstepping)
        {
            if (Time.time >= nextAttackTime)
            {
                Fire(target.position);
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }

    private void Fire(Vector3 targetPos)
    {
        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (arrowObj.TryGetComponent<Arrow>(out var arrow))
        {
            arrow.Initialize(firePoint.position, targetPos, arrowHeight, arrowDuration);
        }
    }
}
