using Unity.Cinemachine;
using UnityEngine;

public class ConfinerZone : MonoBehaviour
{
    BoxCollider2D col;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            CameraSetting.instance.colliders.Add(col);
            CameraSetting.instance.ChangeCameraConfiner();
        }
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (CameraSetting.instance.colliders.Contains(col))
            {
                CameraSetting.instance.colliders.Remove(col);
                CameraSetting.instance.ChangeCameraConfiner();
            }
        }
    }
}
