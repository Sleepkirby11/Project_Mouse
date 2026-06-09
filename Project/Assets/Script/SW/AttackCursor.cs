using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class AttackCursor : MonoBehaviour
{
    Rigidbody2D rigid;

    //마우스 관련
    Vector2 mouse;
    public Transform target;
    float distance;
    Vector2 distanceVec;
    public float range = 15f;

    //초기화
    private void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        //target Null체크
        if(target != null)
        {
            //mouse, distance 업데이트
            mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            distance = Vector2.Distance(target.position, mouse);
            distanceVec = mouse - (Vector2)target.position;
        }

        //AttackCursor의 position 최대 값 range로 지정
        if (distance > range)
            transform.position = (Vector2)(target.position) + (distanceVec.normalized * range);
        else
            transform.position = (Vector2)mouse;
    }
}