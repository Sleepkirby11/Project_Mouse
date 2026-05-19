using System.Collections;
using UnityEngine;

public class RedBossAttack : MonoBehaviour
{
    [Header("공통")]
    [SerializeField] private Transform firePoint;

    [Header("화살 설정")]
    [SerializeField] private float fireInterval = 0.3f;  // 3발 사이 시간차
    [SerializeField] private float angleSpread = 15f;    // 각도 차이

    private const string ARROW_KEY = "RedBossArrow";

    // ─────────────────────────────────────
    // 공격패턴 1 : 화살 3연발
    // ─────────────────────────────────────
    public IEnumerator AttackArrow()
    {
        float[] angles = { 0f, -angleSpread, angleSpread };

        foreach (float angle in angles)
        {
            SpawnArrow(angle);
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void SpawnArrow(float angleOffset)
    {
        GameObject obj = PoolingManager.Instance.Get(ARROW_KEY, firePoint.position, Quaternion.identity);
        if (obj == null) return;

        obj.GetComponent<FireArrow>()?.Init(angleOffset);
    }

    // ─────────────────────────────────────
    // 공격패턴 2 : 메테오 (나중에 구현)
    // ─────────────────────────────────────
    public IEnumerator AttackMeteor()
    {
        yield break; // TODO
    }

    // ─────────────────────────────────────
    // 공격패턴 3 : 레이저 (나중에 구현)
    // ─────────────────────────────────────
    public IEnumerator AttackLaser()
    {
        yield break; // TODO
    }
}