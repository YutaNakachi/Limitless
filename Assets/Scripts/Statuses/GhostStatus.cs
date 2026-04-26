public class GhostStatus : EnemyStatus
{
    protected override void OnDie()
    {
        base.OnDie();
    }

    protected override void OnInvincibilityEnd()
    {
        // 無敵が終わったら見た目を変える演出など
    }
}