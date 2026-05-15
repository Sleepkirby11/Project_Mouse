using UnityEngine;

public class NormalAttack : MonoBehaviour
{
    Rigidbody2D rigid;
    Vector2 mouse;
    void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        rigid.position = mouse;
    }
}
