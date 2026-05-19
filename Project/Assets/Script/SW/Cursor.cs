using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/*
 * Cursor: 마우스를 따라 Trail을 생성하고 collider를 입히는 과정
 * trail의 width가 0.25f이면 그리는 중
 * lifeTime이 0이 아니면 공격 실행
 * 공격 실행 시 SetColliderPointsFromTrail()로 Collider를 Trail에 입힘
 */

public class Cursor : MonoBehaviour
{
    //Trail의 지속시간
    public float lifeTime;

    //드로잉 가능 여부
    public bool isMove;

    TrailRenderer trail;
    EdgeCollider2D col;

    //Trail의 정점 변수
    List<Vector2> points = new List<Vector2>();
    int positionCount;
    public float trailLength;

    private Vector3[] trailPositions = new Vector3[20];

    //마우스 좌표 저장
    public Transform mouse;

    public int damage;


    //초기화
    void Start()
    {
        trail = GetComponent<TrailRenderer>();
        col = GetComponent<EdgeCollider2D>();
        trail.time = 9999;
    }

    void Update()
    {
        //endWidth는 항상 startWidth와 동일시
        trail.endWidth = trail.startWidth;

        //지속 시간에 따른 trail의 크기 변화 및 collider의 enabled 여부
        if (lifeTime > 0)
        {
            col.enabled = true;

            lifeTime -= Time.deltaTime;
            trail.startWidth = lifeTime * 2;
        }
        else if (lifeTime < 0)
        {
            lifeTime = 0;
            trail.startWidth = 0;
        }
        else
        {
            col.enabled = false;
        }

        //그리는 중 마우스 좌표 따라 이동
        if (isMove)
        {
            transform.position = mouse.transform.position;
            // 트레일의 정점 개수 업데이트 확인
            if(positionCount != trail.positionCount)
            {
                positionCount = trail.positionCount;
                trailLength = GetTrailLength();
            }
        }


    }

    float GetTrailLength()
    {
        float length = 0f;
        if (positionCount < 2) return length;

        trail.GetPositions(trailPositions);
        for (int i = 1; i < positionCount; i++)
        {
            length += Vector3.Distance(trailPositions[i - 1], trailPositions[i]);
        }
        return length;
    }

    //Trail에 Collider를 입히는 과정
    public void SetColliderPointsFromTrail()
    {
        if (positionCount < 2) return;

        trail.GetPositions(trailPositions);

        points.Clear();

        // 트레일 좌표를 로컬 좌표로 변환 후 할당
        for (int i = 0; i < positionCount; i++)
        {
            points.Add(transform.InverseTransformPoint(trailPositions[i]));
        }

        col.points = points.ToArray();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            collision.gameObject.GetComponent<EnemyStatus>().TakeDamage(damage);
        }
    }
}
