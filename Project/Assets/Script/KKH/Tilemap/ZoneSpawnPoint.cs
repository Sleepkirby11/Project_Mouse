using UnityEngine;

public class ZoneSpawnPoint : MonoBehaviour
{
    // �����Ϳ��� ���� ��ġ�� ���� �˾ƺ� �� �ֵ��� �� �信 ����� �׸��ϴ�.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        // �÷��̾� ũ�⸸�� ��� ��ü�� �׷��ݴϴ�.
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }
}