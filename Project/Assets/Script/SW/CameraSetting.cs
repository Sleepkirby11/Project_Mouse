using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;

public class CameraSetting : MonoBehaviour
{
    public static CameraSetting instance { get; private set; }
    public List<BoxCollider2D> colliders = new List<BoxCollider2D>();
    CinemachineConfiner2D cameraConfiner;

    void Awake()
    {
        if (instance == null)
            instance = this;

        cameraConfiner = GetComponent<CinemachineConfiner2D>();
    }


    public void ChangeCameraConfiner()
    {
        if(colliders.Count == 0)
        {
            return;
        }

        if(cameraConfiner.BoundingShape2D != colliders.Last())
            cameraConfiner.BoundingShape2D = colliders.Last();
    }
}
