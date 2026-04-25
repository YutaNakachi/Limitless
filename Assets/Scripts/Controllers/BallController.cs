using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int attackDamage = 10; // ボールの攻撃力

    /// <summary>
    /// CollisionDetector（イベント）から呼び出されるメソッド
    /// </summary>
    /// <param name="collider">衝突した相手のCollider2D</param>
    public void OnHit(Collider2D collider)
    {
        // 相手が「EnemyStatus」を持っているか確認
        EnemyStatus targetEnemy = collider.GetComponent<EnemyStatus>();

        if (targetEnemy != null)
        {
            // 敵であればダメージを与える
            targetEnemy.TakeDamage(attackDamage);

            Debug.Log($"敵 {collider.gameObject.name} にダメージ！");
        }
        else
        {
            // 敵以外（プレイヤー自身や壁など）に当たった時の処理が必要なら
        }
    }
}