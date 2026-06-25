using System.Collections;
using UnityEngine;

public class RgbBossAttack : MonoBehaviour
{
    [Header("FireGear Spawn")]
    [SerializeField] private float spawnOffsetY = -1f;
    [SerializeField] private float redAttackInterval = 3f;

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

        //StartCoroutine(AttackRoutine());
    }
    private void Update() // 테스트용 
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            PoolingManager.Instance.Get(
                "IceHammer",
                player.position + Vector3.up * 3f,
                Quaternion.identity);
        }
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
        SpawnFireGears();

        yield return new WaitForSeconds(redAttackInterval);
    }

    private IEnumerator GreenAttack()
    {
        // TODO : Green 전용 패턴

        yield return new WaitForSeconds(3f);
    }

    private IEnumerator BlueAttack()
    {
        Vector3 spawnPos =
            player.position + Vector3.up * 5f;

        PoolingManager.Instance.Get(
            "IceHammer",
            spawnPos,
            Quaternion.identity);

        yield return new WaitForSeconds(5f);
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