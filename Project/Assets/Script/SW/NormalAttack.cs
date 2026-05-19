using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NormalAttack : MonoBehaviour
{
    //Trail의 지속시간
    public float lifeTime;

    //드로잉 가능 여부
    public bool isMove;

    TrailRenderer trail;
    EdgeCollider2D col;

    //Trail의 정점 변수
    List<Vector2> points = new List<Vector2>();

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
        else
        {
            col.enabled = false;
        }

        //그리는 중 마우스 좌표 따라 이동
        if (isMove)
        {
            transform.position = mouse.transform.position;
        }


    }

    //Trail에 Collider를 입히는 과정
    public void SetColliderPointsFromTrail()
    {
        // 트레일의 정점 개수(2개 이상) 확인
        int positionCount = trail.positionCount;
        if (positionCount < 2) return;

        Vector3[] trailPositions = new Vector3[positionCount];
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
        if(collision.gameObject.CompareTag("Enemy"))
        {
            collision.gameObject.GetComponent<EnemyStatus>().TakeDamage(damage);
        }
    }
}

