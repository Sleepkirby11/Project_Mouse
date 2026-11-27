using UnityEngine;
using Unity.Cinemachine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake instance;
    private CinemachineImpulseSource impulseSource;

    void Awake()
    {
        if(instance == null)
            instance = this;
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void Impulse()
    {
        if(impulseSource != null)
        {
            CinemachineImpulseManager.Instance.Clear();
            impulseSource.GenerateImpulse();
        }
    }
}
