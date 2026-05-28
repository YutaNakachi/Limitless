using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RedBallAbility : BallAbility
{
    [Header("ーー 「赫」 固有設定 ーー")]
    [SerializeField] private GameObject redExplosionEffectPrefab; // 赫の展開エフェクト（子要素になる）
    [SerializeField] private GameObject redCenterEffectPrefab; // 赫の中心部分のエフェクト
    [SerializeField] private float normalExpandSpeed = 2f;       // 通常時の拡大速度
    [SerializeField] private float smashExpandSpeed = 3.5f;     // スマッシュ時の拡大速度（より素早く広がる）

    [SerializeField] private float normalDuration = 3.0f;       // 通常時の持続時間
    [SerializeField] private float smashDuration = 5.0f;        // スマッシュ時の持続時間（仕様：持続が伸びる）

    [SerializeField] private float damageInterval = 0.5f;       // 多段ヒットの間隔（秒）

    // 🛠️【追加】インスペクターから「赫」を展開させるレイヤーを複数選択できるようにする
    [SerializeField] private LayerMask deployTargetLayers;

    [Header("Smoke Effects")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private GameObject kokusenSmokeEffectPrefab;
    [SerializeField] private GameObject kokusenThunderEffectPrefab;

    private bool _isDeployed = false; // 術式が展開（衝突・停止）したかどうかのフラグ
    private float _currentDuration = 0f;
    private float _currentExpandSpeed = 0f;
    private int _finalDamage = 0;
    private GameObject _smokeEffect;
    private GameObject _thunderEffect;

    // 敵ごとの無敵時間を管理する辞書（連続ダメージの間隔制御用）
    private Dictionary<EnemyStatus, float> _enemyDamageTimers = new Dictionary<EnemyStatus, float>();

    /// <summary>
    /// プレイヤーに蹴られて飛んでいく瞬間の処理
    /// </summary>
    protected override void OnFire()
    {
        // 1. 画面揺れやヒットストップ演出
        if (_isSmashFired && _isKokusenFired)
        {
            FxManager.Instance.Play("KokusenBallKick", transform);
        }
        else if (_isSmashFired)
        {
            FxManager.Instance.Play("SmashBallKick", transform);
        }
        else
        {
            FxManager.Instance.Play("NormalBallKick", transform);
        }

        // 2. 🔥 【ボールの子要素としてエフェクトを生成する】
        if (_isSmashFired && _isKokusenFired)
        {
            if (kokusenSmokeEffectPrefab != null && kokusenThunderEffectPrefab != null)
            {
                transform.localScale *= 1.5f;

                // ボールと同じ位置に生成
                _smokeEffect = Instantiate(kokusenSmokeEffectPrefab, transform.position, Quaternion.identity);
                _thunderEffect = Instantiate(kokusenThunderEffectPrefab, transform.position, Quaternion.identity);

                // 💡 ここが核心！ボールの子供にすることで、煙の「噴射口」をボールに追従させる
                _smokeEffect.transform.SetParent(transform);
                _thunderEffect.transform.SetParent(transform);
            }
        }
        else
        {
            if (smokeEffectPrefab != null)
            {
                transform.localScale *= 1.5f;

                // ボールと同じ位置に生成
                _smokeEffect = Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);

                // 💡 ここが核心！ボールの子供にすることで、煙の「噴射口」をボールに追従させる
                _smokeEffect.transform.SetParent(transform);
            }
        }

        // 発射された時点のフラグ状態から、今回のダメージ・持続時間・拡大速度をキャッシュする
        _currentDuration = _isSmashFired ? smashDuration : normalDuration;
        _currentExpandSpeed = _isSmashFired ? smashExpandSpeed : normalExpandSpeed;

        // 元のインスペクター設定値（attackDamage等）は基底クラスのプライベート変数なので、
        // 擬似的にここでスマッシュか否かの判定を行い赫用のダメージを決定します
        // （※必要に応じて元の設定値（1回のダメージ量）を調整してください）
        _finalDamage = _isSmashFired ? smashAttackDamage : attackDamage;

        Debug.Log($"🔴 術式「赫」放たれる！ (Smash: {_isSmashFired})");
    }

    /// <summary>
    /// 🛠️ 基底クラスのOnHitをoverrideして「赫」の展開トリガーにする
    /// CollisionDetector から衝突（またはトリガー）検知時に呼び出されます
    /// </summary>
    /// <param name="collider">衝突した相手のCollider2D</param>
    public override void OnHit(Collider2D collider)
    {
        // すでに展開している、またはまだ蹴られていないなら無視
        if (_isDeployed || !isKicked) return;

        // 🛠️ ぶつかった相手のレイヤーが、インスペクターで指定した LayerMask に含まれているか判定
        // (1 << collider.gameObject.layer) でビット演算を行い、deployTargetLayers と重なっているかチェックします
        if ((deployTargetLayers.value & (1 << collider.gameObject.layer)) != 0)
        {
            DeployRed();
        }
    }

    /// <summary>
    /// 術式「赫」をその場に展開する中心ロジック
    /// </summary>
    private void DeployRed()
    {
        _isDeployed = true;

        // 1. その場で完全停止
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _rigidbody.bodyType = RigidbodyType2D.Kinematic; // 展開中に他の物理で動かされないように固定

        // 2. Ball自体のRendererを非アクティブにする
        if (_renderer != null) _renderer.enabled = false;

        if (_smokeEffect != null) Destroy(_smokeEffect);
        if (_thunderEffect != null) Destroy(_thunderEffect);

        // 3. コライダーをトリガー（すり抜け・多段判定用）に切り替える
        _collider.isTrigger = true;

        // 4. 同時にBallの子要素としてエフェクトを生成
        if (redExplosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(redExplosionEffectPrefab, transform.position, Quaternion.identity);
            GameObject centerEffect = Instantiate(redCenterEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.SetParent(transform); // 子要素にする
            centerEffect.transform.SetParent(transform); // 子要素にする
        }

        Debug.Log("💥 術式「赫」展開！！");

        // 5. 制限時間後に消滅させるカウントダウンを開始
        StartCoroutine(DurationCoroutine());
    }

    private void Update()
    {
        if (!_isDeployed) return;

        // 6. 徐々にBallのサイズを大きくする（コライダーが大きくなる）
        transform.localScale += Vector3.one * _currentExpandSpeed * Time.deltaTime;

        // 敵ごとのダメージ内部タイマーを進める（辞書の更新）
        List<EnemyStatus> keys = new List<EnemyStatus>(_enemyDamageTimers.Keys);
        foreach (var enemy in keys)
        {
            if (_enemyDamageTimers[enemy] > 0)
            {
                _enemyDamageTimers[enemy] -= Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// 巨大化する範囲に入り続けている敵への多段ヒット処理
    /// </summary>
    public void JutsushikiAka(Collider2D collider)
    {
        if (!_isDeployed) return;

        if (collider.CompareTag("Enemy"))
        {
            EnemyStatus target = collider.GetComponent<EnemyStatus>();
            if (target != null)
            {
                if (target.IsInvincible) return;

                // 辞書に対象の敵が登録されていなければ初期化登録
                if (!_enemyDamageTimers.ContainsKey(target))
                {
                    _enemyDamageTimers[target] = 0f;
                }

                // 前回のダメージから0.5秒（damageInterval）経過しているかチェック
                if (_enemyDamageTimers[target] <= 0f)
                {
                    // ダメージ付与
                    target.TakeDamage(_finalDamage, transform.position);

                    // ベースクラスのエフェクト再生メソッドを流用（接点にエフェクトを出す）
                    PlayHitEffect(collider);

                    Debug.Log($"🩸 「赫」持続ダメージ: {collider.gameObject.name} に {_finalDamage} ダメージ！");

                    // タイマーをリセットして再セット（0.5秒間ダメージをロック）
                    _enemyDamageTimers[target] = damageInterval;
                }
            }
        }
    }

    /// <summary>
    /// 8. 一定時間経過後にBallをDestroyするコルーチン
    /// </summary>
    private IEnumerator DurationCoroutine()
    {
        yield return new WaitForSeconds(_currentDuration);

        Debug.Log("🔴 術式「赫」が制限時間に達したため消滅します");

        // 多重判定を防ぐ安全処理
        if (GetComponent<CollisionDetector>() != null) GetComponent<CollisionDetector>().enabled = false;
        _collider.enabled = false;

        Destroy(gameObject);
    }

    /// <summary>
    /// 基底クラスの自然消滅コルーチンをシャットダウン（赫の寿命はDurationCoroutineで完全管理するため）
    /// </summary>
    protected override IEnumerator DestroyABall()
    {
        // ボールの速度が一定以下（例：2f以下）になるまで待機する（基底クラスの条件を流用）
        yield return new WaitUntil(() => _rigidbody != null && _rigidbody.linearVelocity.magnitude <= 2f);

        // すでに何かにぶつかって展開（DeployRed）済みなら、このコルーチンは何もせず終了
        if (_isDeployed || this == null) yield break;

        Debug.Log("🎯 空間起爆：何にも当たらなかったため、最大到達点で「赫」を自動展開します。");

        yield return new WaitForSeconds(ballLifeTime);

        // 何にも当たらなかったので、その場で勝手に展開させる！
        DeployRed();
    }
}