public interface IHitReaction
{
    bool OnBeforeTakeDamage(EnemyStatus enemyStatus, int damage); // 대미지를 받을지 말지 판단
    void OnAfterTakeDamage(EnemyStatus enemyStatus, int damage); // 대미지를 받은 후의 반응
}