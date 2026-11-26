using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/*
 * Paint: 마우스를 따라 Trail을 생성하고 collider를 입히는 과정
 * trail의 width가 0.25f이면 그리는 중
 * lifeTime이 0이 아니면 공격 실행
 * 공격 실행 시 SetColliderPointsFromTrail()로 Collider를 Trail에 입힘
 */

public class Paint : MonoBehaviour
{
    GameObject player;
    
    Player playerScript;
    PlayerStatus playerStatus;

    //Trail의 지속시간
    public float lifeTime;

    //드로잉 가능 여부
    public bool isMove;

    [HideInInspector] public bool isSkill;

    TrailRenderer trail;
    EdgeCollider2D col;
    PaintVFX paintVFX;

    //VFX 상태 추적용
    bool wasMoving;
    float initialLifeTime;

    //Trail의 정점 변수
    List<Vector2> points = new List<Vector2>();
    int positionCount;
    public float trailLength;
    public float lastLength;

    private Vector3[] trailPositions = new Vector3[2000];

    //마우스 좌표 저장
    public Transform mouse;

    public int damage;


    //초기화
    void Start()
    {
        trail = GetComponent<TrailRenderer>();
        col = GetComponent<EdgeCollider2D>();
        paintVFX = GetComponent<PaintVFX>();
        player = GameObject.FindWithTag("Player");
        playerScript = player.gameObject.GetComponent<Player>();
        playerStatus = player.gameObject.GetComponent<PlayerStatus>();
        trail.time = 9999;
    }

    void FixedUpdate()
    {
        //endWidth는 항상 startWidth와 동일시
        trail.endWidth = trail.startWidth;

        //그리기 시작 감지
        if (isMove && !wasMoving)
        {
            wasMoving = true;
            paintVFX?.OnDrawStart(mouse != null ? mouse.position : transform.position);
        }
        else if (!isMove && wasMoving)
        {
            wasMoving = false;
        }

        //지속 시간에 따른 trail의 크기 변화 및 collider의 enabled 여부
        if (lifeTime > 0)
        {
            lifeTime -= Time.deltaTime;
            if (paintVFX != null)
            {
                paintVFX.OnLifeTimeUpdate(lifeTime, initialLifeTime);
            }
            else
            {
                trail.startWidth = lifeTime * 2;
            }
        }
        else if (lifeTime < 0)
        {
            lifeTime = 0;
            trail.startWidth = 0;
            paintVFX?.OnFinish();
        }
        else
        {
            col.enabled = false;
        }

        //그리는 중 마우스 좌표 따라 이동
        if (isMove)
        {
            transform.position = mouse.transform.position;
            paintVFX?.OnDrawStep(transform.position);
            // 트레일의 정점 개수 업데이트 확인
            if(positionCount != trail.positionCount)
            {
                trailUpdate();
                trailLength = GetTrailLength();
                lastLength = GetLastTrailUpdate();
            }
        }


    }

    public void trailUpdate()
    {
        positionCount = trail.positionCount;
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

    float GetLastTrailUpdate()
    {
        float length = 0f;
        if(positionCount < 2) return length;
        trail.GetPositions(trailPositions);
        
        length = Vector3.Distance(trailPositions[positionCount - 1], trailPositions[positionCount - 2]);

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
        if (positionCount >= 2)
        {
            col.enabled = true;
            initialLifeTime = lifeTime > 0f ? lifeTime : 0.5f;
            paintVFX?.OnActivate(trailPositions, positionCount, initialLifeTime, isSkill);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        IStunnable stunnable = collision.gameObject.GetComponent<IStunnable>();

        if (damageable != null && !collision.gameObject.CompareTag("Player"))
        {
            Bounds targetBounds = collision.bounds;
            Vector3 targetCenter = targetBounds.center;
            float targetRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.y);

            damageable.TakeDamage(damage);
            if (isSkill && playerStatus != null)
            {
                EnemyStatus enemyStatus = collision.gameObject.GetComponent<EnemyStatus>();
                bool hitBlocked = enemyStatus != null && enemyStatus.WasBlockedThisFrame;

                if (!hitBlocked)
                {
                    if (playerStatus.currentStance == PlayerStatus.Stance.Red)
                    {
                        SkillHitParticles.SpawnRedPowerHit(targetCenter, targetRadius);
                    }
                    else if (playerStatus.currentStance == PlayerStatus.Stance.Green)
                    {
                        playerStatus.Heal(2);
                        SkillHitParticles.SpawnGreenHealHit(targetCenter, targetRadius, player != null ? player.transform : null);
                    }
                    else if (playerStatus.currentStance == PlayerStatus.Stance.Blue && stunnable != null)
                    {
                        stunnable.ApplyStun(3);
                    }
                }
            }
        }
    }
}
