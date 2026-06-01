using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PurpleBallAbility : BallAbility
{
    [Header("ーー 「茈」 固有設定 ーー")]
    [Tooltip("あらかじめ右向きにコライダーと【エフェクトの見た目】を両方仕込んだ子オブジェクト")]
    [SerializeField] private GameObject laserChildObject;
    [SerializeField] private GameObject purpleKickEffectPrefab;   // キックした瞬間の爆発Effect
    [SerializeField] private GameObject purpleThunderEffectPrefab;

    [Header("ーー レーザー性能設定（太さ倍率） ーー")]
    [Tooltip("通常キック時のレーザーの太さ倍率（1.0で等倍＝プレハブ本来の太さ）")]
    [SerializeField] private float normalLaserScaleY = 1.0f;
    [Tooltip("スマッシュキック時のレーザーの太さ倍率（2.0でプレハブの2倍の極太化）")]
    [SerializeField] private float smashLaserScaleY = 2.0f;

    [Space(10)]
    [SerializeField] private float normalDuration = 2.5f;       // 通常時のレーザー持続時間
    [SerializeField] private float smashDuration = 4.5f;        // スマッシュ時のレーザー持続時間

    [Space(10)]
    [Tooltip("超高速多段ヒットの間隔（秒）。0.05f〜0.1fで凄まじいヒット数になります")]
    [SerializeField] private float damageInterval = 0.08f;

    private bool _isLaserFired = false; // レーザーが放出されたかどうかのフラグ
    private float _currentDuration = 0f;
    private float _currentLaserScaleY = 0f;
    private int _finalDamage = 0;

    // プレイヤーの固定・無敵解除用に、キックしたプレイヤーの参照を保持する
    private GameObject _kickerPlayer;
    private Rigidbody2D _playerRb;
    private PlayerStatus _playerStatus;

    // 敵ごとの高速無敵時間を管理する辞書（多段ヒット用）
    private Dictionary<EnemyStatus, float> _enemyDamageTimers = new Dictionary<EnemyStatus, float>();

    // ⚽ プレイヤーが蹴り出した方向を一時的に保持する変数
    private Vector2 _launchDirection = Vector2.right;

    private GameObject _purpleThunderEffect;

    private void Start()
    {
        // 自分が生成された（Startが走った）瞬間に、自分の位置にエフェクトを生成する！
        if (purpleThunderEffectPrefab != null)
        {
            _purpleThunderEffect = Instantiate(purpleThunderEffectPrefab, transform.position, Quaternion.identity);
            _purpleThunderEffect.transform.SetParent(transform);
        }
    }

    public override void Fire(Vector2 direction, float force, bool isSmash, float gapY)
    {
        if (isKicked) return;

        isKicked = true;
        _isSmashFired = isSmash;
        _collider.isTrigger = false;
        if (Mathf.Abs(gapY) <= 0.01) _isKokusenFired = true;

        _rigidbody.linearVelocity = Vector2.zero;

        if (_purpleThunderEffect != null) Destroy(_purpleThunderEffect);

        // ⭕ 引数の方向をキャッシュし、正規化しておく
        _launchDirection = direction.normalized;

        // エフェクトの再生処理にフラグを渡すように変更
        PlayKickEffect();

        OnFire();
    }

    /// <summary>
    /// 🔮 茈 the ボールをプレイヤーが足で蹴った瞬間に呼び出される中心トリガー
    /// </summary>
    protected override void OnFire()
    {
        // 1. 👤 まず最初にプレイヤーの物理・ステータス・アニメーションを「茈モード」に完全ロック！
        // (この中で isOnMurasaki = true や UnscaledTime への切り替え、アニメ再生が行われます)
        ApplyPlayerRestrictions();

        // 特別カットインなどの演出トリガー
        FxManager.Instance.Play("PurpleBallKick", transform);
        SoundManager.Instance.PlaySEAtPosition("MurasakiLaunch", _playerStatus.transform.position);

        if (purpleKickEffectPrefab != null)
        {
            Instantiate(purpleKickEffectPrefab, transform.position, Quaternion.identity);
        }

        // 通常/スマッシュに応じた性能・太さ倍率のキャッシュ
        _currentDuration = _isSmashFired ? smashDuration : normalDuration;
        _currentLaserScaleY = _isSmashFired ? smashLaserScaleY : normalLaserScaleY;
        _finalDamage = _isSmashFired ? smashAttackDamage : attackDamage;

        // ⚽ ボールそのものを巨大なレーザービームへと変貌させる
        FirePurpleLaser().Forget();
    }

    /// <summary>
    /// ボールを静止させ、前方へ巨大なレーザー判定を展開する
    /// </summary>
    private async UniTaskVoid FirePurpleLaser()
    {
        _isLaserFired = true;

        // 1. 物理挙動を完全に停止させてその場に固定
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _rigidbody.bodyType = RigidbodyType2D.Kinematic;

        _collider.enabled = false;
        if (_renderer != null) _renderer.enabled = false;

        // 🚀【最重要】展開した瞬間、自身のタグを「Untagged（無所属）」に変更する！
        // これにより、PlayerShootの「if (!collider.CompareTag("Ball")) return;」のチェックをすり抜けるようになります。
        gameObject.tag = "Untagged";

        // 2. 📐【回転処理】
        float angle = Mathf.Atan2(_launchDirection.y, _launchDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 3. 🔮【判定子オブジェクトの有効化】
        if (laserChildObject != null)
        {
            laserChildObject.SetActive(true);
            laserChildObject.transform.localScale = new Vector3(1f, _currentLaserScaleY, 1f);
        }

        await UniTask.WaitUntil(() => Time.timeScale >= 1.0f, PlayerLoopTiming.Update);

        SoundManager.Instance.PlaySEAtPosition("Murasaki1", transform.position);
        SoundManager.Instance.PlaySEAtPosition("Murasaki2", transform.position);

        // ❌ ここにあった ApplyPlayerRestrictions(); は OnFire の最上部に移動しました！

        Debug.Log($"🔮 虚式「茈」最大解放！！ 方向: {_launchDirection} / 太さ倍率: {_currentLaserScaleY}x");

        // 制限時間カウントダウン開始
        StartCoroutine(DurationCoroutine());
    }

    /// <summary>
    /// キックしたプレイヤーを探し出し、無敵・空中固定・ステータス固定を適用する
    /// </summary>
    private void ApplyPlayerRestrictions()
    {
        _kickerPlayer = GameObject.FindGameObjectWithTag("Player");
        if (_kickerPlayer != null)
        {
            _playerRb = _kickerPlayer.GetComponent<Rigidbody2D>();
            _playerStatus = _kickerPlayer.GetComponent<PlayerStatus>();

            if (_playerRb != null)
            {
                // 移動ベクトルを強制リセットして空中完全固定
                _playerRb.linearVelocity = Vector2.zero;
                _playerRb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (_playerStatus != null)
            {
                // 1. スクリプト側の防護フラグをONにする
                _playerStatus.isOnMurasaki = true;

                // 2. 🎬 既存のフラグの状態（true）をそのままAnimatorへ流し込んで同期！
                Animator animator = _kickerPlayer.GetComponent<Animator>();
                if (animator != null)
                {
                    // フラグをONにする
                    animator.SetBool("isOnMurasaki", _playerStatus.isOnMurasaki);
                    animator.SetTrigger("MurasakiFire");

                    // まずモードをUnscaledTimeにする
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;

                    // 🔥【追加】Animatorの設定変更をこの瞬間に強制リフレッシュする！
                    animator.Update(0f);
                }

                _playerStatus.SetInvicible();
                _playerStatus.SetIntroState();

                Debug.Log("👤 [茈] プレイヤーをMurasakiアニメーション状態にロックしました。");
            }
        }
    }

    /// <summary>
    /// プレイヤーの固定と無敵、ステータスを解除し、元の状態に戻す
    /// </summary>
    private void ReleasePlayerRestrictions()
    {
        if (_kickerPlayer == null) return;

        if (_playerRb != null)
        {
            // 物理挙動を通常に戻して重力を再適用
            _playerRb.bodyType = RigidbodyType2D.Dynamic;
        }

        if (_playerStatus != null)
        {
            _playerStatus.CancelInvicible();

            // 1. スクリプト側の防護フラグをOFFにする
            _playerStatus.isOnMurasaki = false;

            // 2. 🎬 解除時も、変更後の状態（false）をそのままAnimatorに流し込んで同期！
            Animator animator = _kickerPlayer.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("isOnMurasaki", _playerStatus.isOnMurasaki); // 👈 ここも自動でfalseが渡る
                animator.updateMode = AnimatorUpdateMode.Normal;
            }

            // 安全ガード付きのパブリックメソッドで通常状態への復帰を試みる
            _playerStatus.GoToNormalStateIfPossible();

            Debug.Log("👤 [茈] プレイヤーのMurasakiアニメーション状態を解除しました。");
        }
    }

    private void Update()
    {
        if (!_isLaserFired) return;

        // 敵ごとの多段ヒット用内部タイマーを進める
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
    /// 子要素（HitDetectorなど）から呼び出される多段判定
    /// </summary>
    public void JutsushikiMurasaki(Collider2D collider)
    {
        if (!_isLaserFired) return;

        if (collider.CompareTag("Enemy"))
        {
            EnemyStatus target = collider.GetComponent<EnemyStatus>();
            if (target != null)
            {
                if (target.IsDead || target.IsInvincible) return;

                if (!_enemyDamageTimers.ContainsKey(target))
                {
                    _enemyDamageTimers[target] = 0f;
                }

                if (_enemyDamageTimers[target] <= 0f)
                {
                    Vector2 forwardDirection = transform.right;
                    Vector2 dummyAttackerPosition = (Vector2)collider.transform.position - (forwardDirection * 2f);

                    target.TakeDamage(_finalDamage, dummyAttackerPosition);
                    PlayHitEffect(collider);

                    _enemyDamageTimers[target] = damageInterval;
                }
            }
        }
    }

    // 各Ball固有のHit Effectを仕込む、OnHit()で呼び出す
    protected override void PlayHitEffect(Collider2D collider)
    {
        Vector2 myCenter = transform.position;
        Vector3 exactHitPoint = collider.ClosestPoint(myCenter);

        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, exactHitPoint, Quaternion.identity);
        }
    }

    /// <summary>
    /// レーザーの照射時間が終了した際のクリーンアップ
    /// </summary>
    private IEnumerator DurationCoroutine()
    {
        yield return new WaitForSeconds(_currentDuration);

        Debug.Log("🟣 虚式「茈」が規定時間に達したため終了します");

        // 1. プレイヤーの行動制限・無敵・ステータスを完全に解除
        ReleasePlayerRestrictions();

        // 2. 二重判定防止の安全処理
        if (laserChildObject != null)
        {
            var childCollider = laserChildObject.GetComponent<Collider2D>();
            if (childCollider != null) childCollider.enabled = false;
        }

        SoundManager.Instance.StopLoopSE("Murasaki1");
        SoundManager.Instance.StopLoopSE("Murasaki2");

        // 3. 茈オブジェクト自体の消滅
        Destroy(gameObject);
    }

    protected override IEnumerator DestroyABall()
    {
        yield break;
    }
}