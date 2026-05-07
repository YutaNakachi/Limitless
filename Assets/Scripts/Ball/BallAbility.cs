using UnityEngine;

public abstract class BallAbility : MonoBehaviour
{
    private Rigidbody2D _rigidbody;
    [SerializeField] private float flySpeed = 30f;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.gravityScale = 0;
    }

    // 共通の射撃処理
    public virtual void Fire(Vector2 direction)
    {
        _rigidbody.linearVelocity = direction.normalized * flySpeed;
        OnFire(); // 子クラスで固有の処理があれば呼ぶ
    }

    // 子クラスで必ず実装、または上書きするメソッド
    protected abstract void OnFire(); // 発射時の特殊演出など
    public abstract void OnHit(Collision2D collision); // 敵に当たった時の効果

}
