using UnityEngine;

public class Ice : MonoBehaviour
{
    public void ReturnToPool()
    {
        PoolingManager.Instance.Return("Ice", gameObject);
    }
}