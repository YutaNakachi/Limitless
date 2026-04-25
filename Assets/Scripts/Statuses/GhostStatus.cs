using UnityEngine;

public class GhostStatus : EnemyStatus
{
    protected override void PlayDeathAnimation()
    {
        // ゴースト特有の「スゥーッと消える」演出ロジックをここに書く
        Debug.Log("お化けが成仏しました");
        Destroy(gameObject);
    }

    protected override void OnInvincibilityEnd()
    {
        // 無敵が終わったら見た目を変える演出など
    }
}