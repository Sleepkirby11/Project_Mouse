using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Zone : MonoBehaviour
{
    [HideInInspector]
    public int zoneId = -1; // 기본값을 -1로 명시하여 초기화 오류를 방지합니다.

    [Header("스폰 설정")]
    public Transform spawnPoint;

    [Header("연결된 구역들")]
    [Tooltip("이 구역과 인접한 주변 구역들의 리스트(Element 번호)를 적어주세요.")]
    public List<int> connectedZoneIds = new List<int>();

    private void Awake()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 재생성되거나 갱신된 싱글톤 인스턴스가 존재할 때만 신호를 보냅니다.
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.OnPlayerEnterZone(this);
            }
        }
    }
}