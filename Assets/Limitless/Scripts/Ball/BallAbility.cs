using System.Collections;
using UnityEngine;

public abstract class BallAbility : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int attackDamage = 10; // ボールの攻撃力
    [SerializeField] private float ballLifeTime = 2f; // ボールX方向の速度が1以下になってから消滅するまでの秒数
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject kickEffectPrefab;

    private Rigidbody2D _rigidbody;
    private Collider2D _collider;

    public bool isKicked { get; private set; } = false;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// CollisionDetector（イベント）から呼び出されるメソッド
    /// </summary>
    /// <param name="collider">衝突した相手のCollider2D</param>
    public virtual void OnHit(Collider2D collider)
    {
        if (!isKicked) return;

        // 相手が「EnemyStatus」を持っているか確認
        EnemyStatus target = collider.GetComponent<EnemyStatus>();

        if (target != null)
        {
            if (target.IsInvincible) return;

            PlayHitEffect(collider);

            // 敵であればダメージを与える
            target.TakeDamage(attackDamage, transform.position);

            Debug.Log($"{collider.gameObject.name} にダメージ！");
        }

    }

    // プレイヤーにキックされた時に呼び出される
    // 基本的には前に飛んでいくだけの動きを記述している
    // 各Ballの動かし方に合わせて書き換える必要がある
    public virtual void Fire(Vector2 direction, float force)
    {
        isKicked = true;
        _collider.isTrigger = false;
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.AddForce(direction * force, ForceMode2D.Impulse);

        PlayKickEffect();

        OnFire();

        StartCoroutine(DestroyABall());
    }

    protected virtual IEnumerator DestroyABall()
    {
        yield return new WaitUntil(() => _rigidbody.linearVelocity.magnitude <= 2f);
        GetComponent<CollisionDetector>().enabled = false;

        yield return new WaitForSeconds(ballLifeTime);
        Destroy(gameObject);
    }


    // 各Ball固有のHit Effectを仕込む、OnHit()で呼び出す
    protected virtual void PlayHitEffect(Collider2D collider)
    {
        Vector2 myCenter = transform.position;
        Vector3 exactHitPoint = collider.ClosestPoint(myCenter);
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, exactHitPoint, Quaternion.identity);
        }
    }

    //各Ball固有のKick Effectを仕込む、Fire()で呼び出す
    protected virtual void PlayKickEffect()
    {
        if (kickEffectPrefab != null)
        {
            Instantiate(kickEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    // 各Ball固有の動きや演出ををここに記述して、Fire()で呼び出す
    protected abstract void OnFire();
}