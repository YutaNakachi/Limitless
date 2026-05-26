using System.Collections;
using UnityEngine;

public abstract class BallAbility : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int attackDamage = 10; // 通常時のボールの攻撃力
    [SerializeField] private int smashAttackDamage = 20; // 💥 スマッシュ時のボールの攻撃力（追加）
    [SerializeField] private float ballLifeTime = 2f; // ボールX方向の速度が1以下になってから消滅するまでの秒数

    [Header("Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject kickEffectPrefab;
    [SerializeField] private GameObject smashKickEffectPrefab; // 💥 スマッシュ用の派手なエフェクト（追加）
    [SerializeField] private GameObject spawnEffectPrefab;

    private Rigidbody2D _rigidbody;
    private Collider2D _collider;

    private int _currentDamage; // 💡 実際に適用される今回のダメージ
    protected bool _isSmashFired = false; // 💡 スマッシュで発射されたかどうかの内部フラグ

    public bool isKicked { get; private set; } = false;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        // 初期の攻撃力を設定
        _currentDamage = attackDamage;

        // 💡 自分が生成された（Startが走った）瞬間に、自分の位置にエフェクトを生成する！
        if (spawnEffectPrefab != null)
        {
            GameObject spawnEffect = Instantiate(spawnEffectPrefab, transform.position, Quaternion.identity);
            spawnEffect.transform.SetParent(transform);
        }
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

            // 敵であればダメージを与える（💡 決定されたダメージを適用）
            target.TakeDamage(_currentDamage, transform.position);

            Debug.Log($"{collider.gameObject.name} に {_currentDamage} ダメージ！");
        }
    }

    // プレイヤーにキックされた時に呼び出される
    // 💡 引数に isSmash を追加
    public virtual void Fire(Vector2 direction, float force, bool isSmash)
    {
        isKicked = true;
        _isSmashFired = isSmash;
        _collider.isTrigger = false;

        // 💡 スマッシュか否かで攻撃力（ダメージ）を切り替える
        _currentDamage = isSmash ? smashAttackDamage : attackDamage;

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.AddForce(direction * force, ForceMode2D.Impulse);

        // 💡 エフェクトの再生処理にフラグを渡すように変更
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

    // 各Ball固有のKick Effectを仕込む、Fire()で呼び出す
    protected virtual void PlayKickEffect()
    {
        // 💡 スマッシュフラグに応じて生成するプレハブを切り替える
        if (_isSmashFired && smashKickEffectPrefab != null)
        {
            Instantiate(smashKickEffectPrefab, transform.position, Quaternion.identity);
            Instantiate(kickEffectPrefab, transform.position, Quaternion.identity);
        }
        else if (kickEffectPrefab != null)
        {
            Instantiate(kickEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    // 各Ball固有の動きや演出ををここに記述して、Fire()で呼び出す
    protected abstract void OnFire();
}