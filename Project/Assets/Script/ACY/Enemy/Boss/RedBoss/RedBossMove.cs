using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * �����ð����� ������ ��ġ�� �����̵�
 */
public class RedBossMove : MonoBehaviour
{
    [Header("Ÿ�� ����")]
    [SerializeField] private Transform playerTransform; // �÷��̾� ��ġ ����
    public bool isFacingRight = true; // ���� �ٶ󺸴� ���� (�⺻ ������)

    [Header("�ڷ���Ʈ ����")]
    [SerializeField] private Transform[] teleportPoints;
    [SerializeField] private float teleportInterval = 3f;

    [Header("�ڷ���Ʈ ȿ��")]
    [SerializeField] private GameObject disappearVFX;   // ����� �� ����Ʈ
    [SerializeField] private GameObject appearVFX;      // ������ �� ����Ʈ

    private int currentPointIndex = -1;
    private RedBossAttack bossAttack;

    private void Start()
    {
        bossAttack = GetComponent<RedBossAttack>();
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        StartCoroutine(TeleportRoutine());
    }
    private void Update()
    {
        FlipToTarget();
    }

    private IEnumerator TeleportRoutine()
    {
        while (this != null && gameObject != null)
        {
            yield return new WaitForSeconds(teleportInterval);

            if (bossAttack != null && (bossAttack.IsStunned || bossAttack.IsInvincible))
            {
                continue;
            }
            if (this == null || gameObject == null)
            {
                yield break;
            }
            yield return StartCoroutine(TeleportSequence());
        }
    }

    private IEnumerator TeleportSequence()
    {
        int newIndex = GetRandomDifferentIndex();
        int oldIndex = currentPointIndex; // �̵� �� ���� ��ġ �ε��� ���

        // ������� ����Ʈ ���
        SpawnVFX(disappearVFX, transform.position);

        // ��� ��� (����Ʈ ���� �ð�)
        yield return new WaitForSeconds(0.2f);

        if (bossAttack != null && bossAttack.IsCastingMeteor) // ���׿� ���� ���̶�� Ŭ�а� ���� ��ġ ��ü
        {
            bossAttack.SwapCloneAndBoss(oldIndex, newIndex);
        }

        // ���� �̵�
        currentPointIndex = newIndex;
        transform.position = teleportPoints[currentPointIndex].position;

        // ���� ����Ʈ ���
        SpawnVFX(appearVFX, transform.position);
    }

    public void Teleport()
    {
        StartCoroutine(TeleportSequence());
    }

    private int GetRandomDifferentIndex()
    {
        if (teleportPoints.Length == 1)
        {
            return 0;
        }

        List<int> candidates = new List<int>();
        for (int i = 0; i < teleportPoints.Length; i++)
        {
            if (i != currentPointIndex)
            {
                candidates.Add(i);
            }
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void SpawnVFX(GameObject vfxPrefab, Vector3 position)
    {
        if (vfxPrefab == null)
        {
            return;
        }

        GameObject vfx = Instantiate(vfxPrefab, position, Quaternion.identity);

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();

            // ��ƼŬ ��� ������ �ڵ� ����
            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }
    private void FlipToTarget()
    {
        if (playerTransform == null)
        {
            return;
        }

        float direction = playerTransform.position.x - transform.position.x;

        if (direction > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (direction < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}