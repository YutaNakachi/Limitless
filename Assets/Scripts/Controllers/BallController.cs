using System.Collections;
using UnityEngine;

public class BallController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int attackDamage = 10; // ボールの攻撃力

    private Rigidbody2D _rigidbody;

    void Awake() => _rigidbody = GetComponent<Rigidbody2D>();

    /// <summary>
    /// CollisionDetector（イベント）から呼び出されるメソッド
    /// </summary>
    /// <param name="collider">衝突した相手のCollider2D</param>
    public void OnHitEnemy(Collider2D collider)
    {
        // 相手が「EnemyStatus」を持っているか確認
        EnemyStatus target = collider.GetComponent<EnemyStatus>();

        if (target != null)
        {
            // 敵であればダメージを与える
            target.TakeDamage(attackDamage);

            Debug.Log($"{collider.gameObject.name} にダメージ！");
        }
        else
        {
            // 敵以外（プレイヤー自身や壁など）に当たった時の処理が必要なら
        }
    }

    public void ShotBall(Vector2 direction, float force)
    {
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.AddForce(direction * force, ForceMode2D.Impulse);

        StartCoroutine(DestroyABall());
    }

    private IEnumerator DestroyABall()
    {
        yield return new WaitUntil(() => _rigidbody.linearVelocity.magnitude <= 0.1f);
        _rigidbody.linearVelocity = Vector3.zero;

        yield return new WaitForSeconds(1);
        Destroy(gameObject);
    }
}