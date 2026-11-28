using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class EnemyTrigger : MonoBehaviour
{
    // 인스펙터창에서 여러마리 등록
    [SerializeField] private GameObject[] enemies;

    // 트리거 발동 시 실행할 이벤트 (인스펙터에서 Wall.Rebuild를 연결할 곳)
    [SerializeField] private UnityEvent OnTriggerActivated;

    [SerializeField] private float activateDelay = 0f;

    [Header("보스 컷씬")]
    [SerializeField] private bool playBossCutscene = true;

    [Header("적 등장 연출")]
    [SerializeField] private bool useSpawnParticles = true;
    [SerializeField] private float spawnTelegraphDuration = 0.42f;
    [SerializeField] private float spawnStaggerDelay = 0f;

    private bool activated;
    public bool IsActivated => activated;

    public bool HasBoss() => FindBossInEnemies() != null;

    public void SetActivatedFromSave(bool value)
    {
        activated = value;
    }

    public void EnsureAliveEnemiesActive()
    {
        if (enemies == null) return;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            EnemyStatus status = enemies[i].GetComponent<EnemyStatus>();
            if (status != null && status.IsDead) continue;
            if (!enemies[i].activeSelf)
            {
                enemies[i].SetActive(true);
            }
        }
    }

    private void Awake()
    {
        if (!playBossCutscene || activated || enemies == null)
            return;

        if (FindBossInEnemies() == null)
            return;

        // 프리팹 수정 등으로 보스가 활성 상태로 저장되어도 트리거 진입 전에는 비활성 유지
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].activeSelf)
            {
                enemies[i].SetActive(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated)
            return;

        if (!other.CompareTag("Player"))
            return;

        activated = true;

        EnemyStatus boss = FindBossInEnemies();
        if (boss != null)
        {
            Vector3 restartPos = CalculateBossTriggerFrontPosition(other);
            SaveManager.NotifyBossFightStarted(this, restartPos);
        }
        else
        {
            SaveManager.MarkDirty();
        }

        StartCoroutine(ActivateRoutine());
    }

    private Vector3 CalculateBossTriggerFrontPosition(Collider2D playerCollider)
    {
        Vector3 playerPos = playerCollider.transform.position;
        BoxCollider2D triggerBox = GetComponent<BoxCollider2D>();
        if (triggerBox == null)
        {
            return playerPos;
        }

        Bounds b = triggerBox.bounds;
        bool enteredFromLeft = playerPos.x <= b.center.x;
        float frontX = enteredFromLeft ? (b.min.x - 1.8f) : (b.max.x + 1.8f);

        Player playerComp = playerCollider.GetComponent<Player>();
        float halfHeight = playerComp != null ? playerComp.ColliderHalfHeight : 0.9f;

        Vector2 rayOrigin = new Vector2(frontX, playerPos.y + 2.5f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 15f, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
        {
            return new Vector3(frontX, hit.point.y + halfHeight + 0.05f, playerPos.z);
        }

        if (playerComp != null && playerComp.HasLastGroundedPosition)
        {
            Vector3 groundedPos = playerComp.LastGroundedPosition;
            if (enteredFromLeft && groundedPos.x >= b.min.x - 0.5f)
            {
                groundedPos.x = frontX;
            }
            else if (!enteredFromLeft && groundedPos.x <= b.max.x + 0.5f)
            {
                groundedPos.x = frontX;
            }
            return groundedPos;
        }

        return new Vector3(frontX, playerPos.y, playerPos.z);
    }

    private IEnumerator ActivateRoutine()
    {
        EnemyStatus boss = FindBossInEnemies();

        if (boss != null)
        {
            DismissChasingGhostsForBossFight();
        }

        if (playBossCutscene && boss != null)
        {
            // 보스방 문 닫기 등 트리거 이벤트 선반영
            OnTriggerActivated?.Invoke();

            if (activateDelay > 0)
                yield return new WaitForSeconds(activateDelay);

            BossIntroCutscene.Play(boss, enemies);
            yield break;
        }

        if (activateDelay > 0)
            yield return new WaitForSeconds(activateDelay);

        OnTriggerActivated?.Invoke();

        if (enemies == null || enemies.Length == 0)
            yield break;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null)
                continue;

            ActivateEnemyWithEffect(enemy);

            if (useSpawnParticles && spawnStaggerDelay > 0f && i < enemies.Length - 1)
            {
                yield return new WaitForSeconds(spawnStaggerDelay);
            }
        }
    }

    private void ActivateEnemyWithEffect(GameObject enemy)
    {
        if (!useSpawnParticles)
        {
            enemy.SetActive(true);
            return;
        }

        if (enemy.activeSelf)
        {
            EnemySpawnParticles.PlayMaterializeBurst(enemy);
            return;
        }

        EnemySpawnParticles.PlaySpawnSequence(enemy, spawnTelegraphDuration, () =>
        {
            if (enemy != null)
            {
                enemy.SetActive(true);
            }
        });
    }

    private void DismissChasingGhostsForBossFight()
    {
        GhostEnemyMove[] ghosts = FindObjectsByType<GhostEnemyMove>(FindObjectsSortMode.None);
        for (int i = 0; i < ghosts.Length; i++)
        {
            GhostEnemyMove ghost = ghosts[i];
            if (ghost == null) continue;

            if (enemies != null && System.Array.IndexOf(enemies, ghost.gameObject) >= 0)
            {
                continue;
            }

            ghost.DismissIfChasing();
        }
    }

    private EnemyStatus FindBossInEnemies()
    {
        if (enemies == null) return null;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            EnemyStatus status = enemies[i].GetComponent<EnemyStatus>();
            if (status != null && status.IsBoss)
            {
                return status;
            }
        }

        return null;
    }
}