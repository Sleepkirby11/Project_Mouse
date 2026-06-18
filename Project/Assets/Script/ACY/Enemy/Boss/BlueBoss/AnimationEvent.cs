using UnityEngine;

public class AnimationEvent : MonoBehaviour
{
    private BossController boss;

    private void Awake()
    {
        boss = GetComponentInParent<BossController>();
    }

    public void ClawHitboxOn() => boss.ClawHitboxOn();
    public void ClawHitboxOff() => boss.ClawHitboxOff();
    public void SonicFire() => boss.SonicFire();
    public void RestJumpEvent() => boss.RestJumpEvent();
}
