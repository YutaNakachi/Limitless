using UnityEngine;

public abstract class EnemyStatus : MobStatus
{
    [Header("Enemy Common Settings")]
    [SerializeField] protected int scoreValue = 10;
    [SerializeField] protected int attackPower = 10;
    [SerializeField] protected GameObject deathEffectPrefab;

    // Enemy共通の死亡時処理
    protected override void OnDie()
    {
        // 1. スコア加算（ScoreManagerなどは別途用意）
        // ScoreManager.Instance.AddScore(scoreValue);

        // 2. 共通の爆発エフェクト生成
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // 3. 個別の死亡演出を呼び出す
        PlayDeathAnimation();
    }

    // 各敵キャラ（Ghostなど）に、自分専用の消え方を書かせるためのabstract
    protected abstract void PlayDeathAnimation();
}