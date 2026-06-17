using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Zone : MonoBehaviour
{
    [Header("구역 설정")]
    public int zoneId;

    [Header("스폰 설정")]
    public Transform spawnPoint;

    [Header("연결된 구역들")]
    public List<int> connectedZoneIds = new List<int>();

    private void Awake()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ZoneManager.Instance.OnPlayerEnterZone(this);
        }
    }
}