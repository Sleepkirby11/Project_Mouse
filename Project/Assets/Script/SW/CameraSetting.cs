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

    void Awake()
    {
        if (instance == null)
            instance = this;

        cameraConfiner = GetComponent<CinemachineConfiner2D>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        colliders.Clear();
        if (cameraConfiner != null)
        {
            cameraConfiner.BoundingShape2D = null;
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
