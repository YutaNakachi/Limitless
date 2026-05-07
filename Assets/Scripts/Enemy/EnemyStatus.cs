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

        Destroy(gameObject);
    }

    public void OnAttack(Collider2D collider)
    {
        // 相手が「PlayerStatus」を持っているか確認
        PlayerStatus target = collider.GetComponent<PlayerStatus>();

        if (target != null)
        {
            // プレイヤーであればダメージを与える
            target.TakeDamage(attackPower);

            Debug.Log($"{collider.gameObject.name} にダメージ！");
        }
        else
        {
            // 敵以外（プレイヤー自身や壁など）に当たった時の処理が必要なら
        }
    }
}