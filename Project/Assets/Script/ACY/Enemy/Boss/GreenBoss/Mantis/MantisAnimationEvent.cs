using UnityEngine;

public class MantisAnimationEvent : MonoBehaviour
{
    private MantisAttack parentAttack;

    private void Start()
    {
        // 부모 오브젝트의 MantisAttack 컴포넌트를 캐싱합니다.
        parentAttack = GetComponentInParent<MantisAttack>();
    }

    // Slam 내려찍기 타격 순간 호출되는 애니메이션 이벤트
    public void OnSlamHit()
    {
        if (parentAttack != null)
        {
            parentAttack.OnSlamHit();
        }
    }

    // Stab 연속 찌르기 타격 순간(들) 호출되는 애니메이션 이벤트
    public void OnStabHit()
    {
        if (parentAttack != null)
        {
            parentAttack.OnStabHit();
        }
    }

    // 호환용 통합 근접 타격 이벤트
    public void OnMeleeHit()
    {
        if (parentAttack != null)
        {
            parentAttack.OnMeleeHit();
        }
    }

    // 근접 공격 모션이 완전히 끝나고 복귀할 때 호출되는 애니메이션 이벤트
    public void OnMeleeEnd()
    {
        if (parentAttack != null)
        {
            parentAttack.OnMeleeEnd();
        }
    }

    // 원거리 칼날 발사 순간 호출되는 애니메이션 이벤트
    public void OnRangedShoot()
    {
        if (parentAttack != null)
        {
            parentAttack.OnRangedShoot();
        }
    }

    // 원거리 공격 모션이 완전히 끝나고 복귀할 때 호출되는 애니메이션 이벤트
    public void OnRangedEnd()
    {
        if (parentAttack != null)
        {
            parentAttack.OnRangedEnd();
        }
    }
}
