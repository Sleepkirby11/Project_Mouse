using UnityEngine;

public class BlueLaser : MonoBehaviour
{
    public void ReturnToPool() // 애니메이션 이벤트로 호출
    {
        PoolingManager.Instance.Return("BlueLaser", gameObject);
    }
}
