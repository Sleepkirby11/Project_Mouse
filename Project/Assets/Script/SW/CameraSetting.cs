using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class CameraSetting : MonoBehaviour
{
    public static CameraSetting instance { get; private set; }
    public List<BoxCollider2D> colliders = new List<BoxCollider2D>();
    CinemachineConfiner2D cameraConfiner;
    CinemachineCamera cinemachineCamera;

    void Awake()
    {
        if (instance == null)
            instance = this;

        cameraConfiner = GetComponent<CinemachineConfiner2D>();
        cinemachineCamera = GetComponent<CinemachineCamera>();
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        colliders.Clear();
        if (cameraConfiner != null)
        {
            cameraConfiner.BoundingShape2D = null;
            cameraConfiner.InvalidateBoundingShapeCache();
        }

        if (cinemachineCamera != null)
        {
            cinemachineCamera.PreviousStateIsValid = false;
        }
    }

    public void SnapToTarget(Transform target, Vector3 positionDelta)
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
            if (cinemachineCamera == null)
            {
                cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
            }
        }

        if (cinemachineCamera != null)
        {
            if (target != null)
            {
                cinemachineCamera.OnTargetObjectWarped(target, positionDelta);
            }
            cinemachineCamera.PreviousStateIsValid = false;
        }

        if (cameraConfiner != null)
        {
            cameraConfiner.InvalidateBoundingShapeCache();
        }
    }

    public void InvalidateConfinerCache()
    {
        if (cameraConfiner != null)
        {
            cameraConfiner.InvalidateBoundingShapeCache();
        }
    }

    public void ChangeCameraConfiner()
    {
        colliders.RemoveAll(c => c == null);
        if (colliders.Count == 0)
        {
            return;
        }

        if (cameraConfiner.BoundingShape2D != colliders.Last())
            cameraConfiner.BoundingShape2D = colliders.Last();
    }
}
