using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Zone : MonoBehaviour
{
    [Header("���� ����")]
    public int zoneId;

    [Header("���� ����")]
    public Transform spawnPoint;

    [Header("����� ������")]
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