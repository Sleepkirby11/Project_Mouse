using UnityEngine;

public class Wall : MonoBehaviour
{
    public void Collapse()
    {
        Debug.Log($"{gameObject.name} 벽이 파괴되었습니다.");
        Destroy(gameObject);
    }
}