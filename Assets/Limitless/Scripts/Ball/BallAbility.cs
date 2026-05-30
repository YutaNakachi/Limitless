using System.Collections;
using UnityEngine;

public abstract class BallAbility : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] protected int attackDamage = 10; // 通常時のボールの攻撃力
    [SerializeField] protected int smashAttackDamage = 20; // 💥 スマッシュ時のボールの攻撃力
    [SerializeField] protected int kokusenAttackDamage = 200; // 💥 黒閃時のボールの攻撃力
    [SerializeField] protected float ballLifeTime = 2f; // ボールの速度が一定以下になってから消滅するまでの秒数

    // 💡 【追加】ヒット回数の設定（インスペクターで調整可能）
    [Header("Hit Count Settings")]
    [SerializeField] protected int maxHitCount = 1;       // 通常キック時の最大ヒット回数（初期値: 1）
    [SerializeField] protected int smashMaxHitCount = 5;  // 💥 スマッシュキック時の最大ヒット回数（初期値: 5）
    [SerializeField] protected int kokusenMaxHitCount = 10;  // 💥 黒閃キック時の最大ヒット回数（初期値: 10）

    [Header("Effects")]
    [SerializeField] protected GameObject hitEffectPrefab;
    [SerializeField] protected GameObject hitKokusenEffectPrefab;
    [SerializeField] protected GameObject kickEffectPrefab;
    [SerializeField] protected GameObject smashKickEffectPrefab; // 💥 スマッシュ用の派手なエフェクト
    [SerializeField] protected GameObject kokusenKickEffectPrefab; // 💥 スマッシュ用の派手なエフェクト
    [SerializeField] protected GameObject spawnEffectPrefab;

    // 💡 BallAbility.cs の内部にこれを追記してください
    [HideInInspector] public BallType ballType;

    protected Rigidbody2D _rigidbody;
    protected Collider2D _collider;
    protected SpriteRenderer _renderer;

    private int _currentDamage; // 実際に適用される今回のダメージ
    private int _remainingHitCount; // 💡 今回のボールの残りヒット回数（内部処理用）
    protected bool _isSmashFired = false; // スマッシュで発射されたかどうかの内部フラグ
    protected bool _isKokusenFired = false; // 黒閃が発動したかどうかの内部フラグ

    public bool isKicked { get; protected set; } = false;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _renderer = GetComponent<SpriteRenderer>();

        // 初期の攻撃力を設定
        _currentDamage = attackDamage;

        // 自分が生成された（Startが走った）瞬間に、自分の位置にエフェクトを生成する！
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
        if (_remainingHitCount <= 0) return; // 💡 すでに耐久値がゼロなら処理しない

        // 相手が「EnemyStatus」を持っているか確認
        EnemyStatus target = collider.GetComponent<EnemyStatus>();

        if (target != null)
        {
            if (target.IsInvincible) return;

            PlayHitEffect(collider);

            // 敵であればダメージを与える
            target.TakeDamage(_currentDamage, transform.position);

            Debug.Log($"{collider.gameObject.name} に {_currentDamage} ダメージ！");

            // 💡 【追加】敵に当たったのでヒット回数を減らす
            _remainingHitCount--;

            // 💡 残り回数が0になったら、その場で即座にボールを消滅させる
            if (_remainingHitCount <= 0)
            {
                Debug.Log("💥 ボールの最大ヒット回数に達したため消滅します");

                // 多重衝突を防ぐために、当たり判定とコライダーのスクリプトを即無効化
                if (GetComponent<CollisionDetector>() != null) GetComponent<CollisionDetector>().enabled = false;
                _collider.enabled = false;

                Destroy(gameObject);
            }
        }
    }

    // プレイヤーにキックされた時に呼び出される
    public virtual void Fire(Vector2 direction, float force, bool isSmash, float gapY)
    {
        if (isKicked) return;

        isKicked = true;
        _isSmashFired = isSmash;
        _collider.isTrigger = false;
        if (Mathf.Abs(gapY) <= 0.01) _isKokusenFired = true;

        // 💡 スマッシュか否か黒閃か否かで攻撃力と「ヒット回数」を切り替える
        if (_isSmashFired && _isKokusenFired)
        {
            _currentDamage = kokusenAttackDamage;
            _remainingHitCount = kokusenMaxHitCount;
        }
        else if (_isSmashFired)
        {
            _currentDamage = smashAttackDamage;
            _remainingHitCount = smashMaxHitCount;
        }
        else
        {
            _currentDamage = attackDamage;
            _remainingHitCount = maxHitCount;
        }



        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.AddForce(direction * force, ForceMode2D.Impulse);

        // エフェクトの再生処理にフラグを渡すように変更
        PlayKickEffect();

        OnFire();

        StartCoroutine(DestroyABall());
    }

    protected virtual IEnumerator DestroyABall()
    {
        float timer = 0f;
        bool isStopped = false;

        // 1. ボールが失速するのを監視するループ
        while (this != null)
        {
            timer += Time.deltaTime;

            // 💡 蹴り出されてから少し時間が経ち、かつ速度が2f以下に失速した場合
            if (timer > 0.5f && _rigidbody != null && _rigidbody.linearVelocity.magnitude <= 2f)
            {
                isStopped = true;
                break; // 🛑 条件を満たしたので、毎フレーム監視ループを即座に脱出！
            }

            yield return null; // 毎フレーム監視
        }

        // 2. 🛑 ループを抜けた後の処理（失速を検知した時の処理）
        if (this != null && isStopped)
        {

            if (GetComponent<CollisionDetector>() != null)
            {
                GetComponent<CollisionDetector>().enabled = false;
            }


            // 🕒 ここで指定された秒数（ballLifeTime）だけ、オブジェクトを残して待機する
            Debug.Log($"🕒 あと {ballLifeTime} 秒後にオブジェクトを完全に消滅させます。");
            yield return new WaitForSeconds(ballLifeTime);
        }

        // 3. ❌ 最終的な消滅処理
        if (this != null)
        {
            Destroy(gameObject);
        }
    }

    // 各Ball固有のHit Effectを仕込む、OnHit()で呼び出す
    protected virtual void PlayHitEffect(Collider2D collider)
    {
        Vector2 myCenter = transform.position;
        Vector3 exactHitPoint = collider.ClosestPoint(myCenter);

        if (_isSmashFired && _isKokusenFired && kokusenKickEffectPrefab != null)
        {
            Instantiate(hitKokusenEffectPrefab, exactHitPoint, Quaternion.identity);
        }
        else if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, exactHitPoint, Quaternion.identity);
        }
    }

    // 各Ball固有のKick Effectを仕込む、Fire()で呼び出す
    protected virtual void PlayKickEffect()
    {
        // スマッシュフラグに応じて生成するプレハブを切り替える
        if (_isSmashFired && smashKickEffectPrefab != null)
        {
            Instantiate(smashKickEffectPrefab, transform.position, Quaternion.identity);
            Instantiate(kickEffectPrefab, transform.position, Quaternion.identity);

            if (_isKokusenFired && kokusenKickEffectPrefab != null)
            {
                Instantiate(kokusenKickEffectPrefab, transform.position, Quaternion.identity);
            }
        }
        else if (kickEffectPrefab != null)
        {
            Instantiate(kickEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    // 各Ball固有の動きや演出ををここに記述して、Fire()で呼び出す
    protected abstract void OnFire();
}