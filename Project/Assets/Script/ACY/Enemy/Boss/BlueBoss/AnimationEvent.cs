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
    public void SonicHitboxOn() => boss.SonicHitboxOn();
    public void SonicHitboxOff() => boss.SonicHitboxOff();

    public void RestJumpEvent() => boss.RestJumpEvent();
}
