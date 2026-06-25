using System.Collections;
using UnityEngine;

public class RgbBossAttack : MonoBehaviour
{
    [Header("FireGear Spawn")]
    [SerializeField] private float spawnOffsetY = -1f;

    private Transform player;
    private EnemyStatus enemyStatus;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");

        if (playerObj != null)
            player = playerObj.transform;

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            switch (enemyStatus.CurrentElement)
            {
                case EnemyStatus.EnemyElement.Red:
                    yield return StartCoroutine(RedAttack());
                    break;

                case EnemyStatus.EnemyElement.Green:
                    yield return StartCoroutine(GreenAttack());
                    break;

                case EnemyStatus.EnemyElement.Blue:
                    yield return StartCoroutine(BlueAttack());
                    break;

                default:
                    yield return null;
                    break;
            }
        }
    }

    private IEnumerator RedAttack()
    {
        // 플레이어 위치에 FireGear 생성
        SpawnFireGears();

        yield return new WaitForSeconds(3f);
    }

    private IEnumerator GreenAttack()
    {
        // TODO : Green 전용 패턴

        yield return new WaitForSeconds(3f);
    }

    private IEnumerator BlueAttack()
    {
        // TODO : Blue 전용 패턴

        yield return new WaitForSeconds(3f);
    }

    private void SpawnFireGears()
    {
        if (player == null)
            return;

        Vector3 spawnPos = new Vector3(
            player.position.x,
            player.position.y + spawnOffsetY,
            0f);

        PoolingManager.Instance.Get(
            "FireGear",
            spawnPos,
            Quaternion.identity);
    }
}