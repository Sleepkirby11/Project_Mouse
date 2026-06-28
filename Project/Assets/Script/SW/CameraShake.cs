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

    public void Impulse()
    {
        if(impulseSource != null)
        {
            CinemachineImpulseManager.Instance.Clear();
            impulseSource.GenerateImpulse();
        }
    }
}
