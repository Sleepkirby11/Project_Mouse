using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class Zone : MonoBehaviour
{
    [HideInInspector]
    public int zoneId = -1;

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
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.UpdateOnPlayerZone(this.gameObject, true);
            }
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (ZoneManager.Instance != null)
            {
                ZoneManager.Instance.UpdateOnPlayerZone(this.gameObject, false);
            }
        }
    }

    //유니티 인스펙터 창에서 버튼 하나로 센서 크기를 자동 정렬하는 기능
    [ContextMenu("구역 감지 콜라이더 크기 자동 맞춤")]
    public void AutoFitTriggerCollider()
    {
        // 자식 오브젝트들 중에서 모든 타일맵 컴포넌트를 찾아냄
        Tilemap[] tilemaps = GetComponentsInChildren<Tilemap>();

        if (tilemaps == null || tilemaps.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 자식 오브젝트에서 Tilemap을 찾을 수 없어 범위를 계산할 수 없습니다.");
            return;
        }

        // 전체 타일맵들을 아우르는 거대한 바운드(영역 박스)를 초기화
        Bounds combinedBounds = new Bounds();
        bool hasInitBounds = false;

        foreach (Tilemap tilemap in tilemaps)
        {
            // 타일맵에 채워진 타일들이 차지하는 실제 영역을 계산
            tilemap.CompressBounds();
            Bounds localBounds = tilemap.localBounds;

            // 로컬 좌표계 영역을 최상위 부모(Root) 기준의 월드 좌표계 영역으로 변환
            Vector3 worldMin = tilemap.transform.TransformPoint(localBounds.min);
            Vector3 worldMax = tilemap.transform.TransformPoint(localBounds.max);

            Bounds worldBounds = new Bounds();
            worldBounds.SetMinMax(worldMin, worldMax);

            if (!hasInitBounds)
            {
                combinedBounds = worldBounds;
                hasInitBounds = true;
            }
            else
            {
                // 여러 개의 타일맵(바닥, 배경 등) 영역을 하나로 합침
                combinedBounds.Encapsulate(worldBounds);
            }
        }

        // 최상위 부모(Root)에 붙어 있는 구역 감지용 Box Collider 2D를 가져옴
        BoxCollider2D rootCollider = GetComponent<BoxCollider2D>();
        if (rootCollider != null)
        {
            // 물리 장부 기록(Undo) 시스템에 등록하여 에디터 수정을 안전하게 보존
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(rootCollider, "Auto Fit Zone Collider");
#endif

            // Root 기준의 로컬 좌표계로 영역의 중심점과 크기를 환산하여 대입
            Vector3 localCenter = transform.InverseTransformPoint(combinedBounds.center);

            rootCollider.offset = new Vector2(localCenter.x, localCenter.y);
            rootCollider.size = new Vector2(combinedBounds.size.x, combinedBounds.size.y);
            rootCollider.isTrigger = true;

            Debug.Log($"[{gameObject.name}] 자식 타일맵 크기를 계산하여 감지 센서(BoxCollider2D) 세팅을 완료했습니다. (Size: {rootCollider.size})");
        }
    }
}