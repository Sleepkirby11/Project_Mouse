using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class RedBossMove : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform[] teleportPoints;
    [SerializeField] private float teleportInterval = 3f;

    [Header("Teleport VFX")]
    [SerializeField] private GameObject disappearVFX;   // 사라질 때 이펙트
    [SerializeField] private GameObject appearVFX;      // 등장할 때 이펙트

    private int currentPointIndex = -1;
    private RedBossAttack bossAttack;

    private void Start()
    {
        bossAttack = GetComponent<RedBossAttack>();
        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        while (this != null && gameObject != null)
        {
            yield return new WaitForSeconds(teleportInterval);

            if (bossAttack != null && bossAttack.IsStunned)
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

        // 사라지는 이펙트 재생
        SpawnVFX(disappearVFX, transform.position);

        // 잠깐 대기 (이펙트 연출 시간)
        yield return new WaitForSeconds(0.2f);

        // 보스 이동
        currentPointIndex = newIndex;
        transform.position = teleportPoints[currentPointIndex].position;

        // 등장 이펙트 재생
        SpawnVFX(appearVFX, transform.position);

        Debug.Log($"[RedBoss] 순간이동 → Point {currentPointIndex} {transform.position}");
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

            // 파티클 재생 끝나면 자동 제거
            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
        }
    }
}