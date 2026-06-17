using UnityEngine;

public class ZoneSpawnPoint : MonoBehaviour
{
    // 에디터에서 스폰 위치를 쉽게 알아볼 수 있도록 씬 뷰에 기즈모를 그립니다.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        // 플레이어 크기만한 녹색 구체를 그려줍니다.
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }
}