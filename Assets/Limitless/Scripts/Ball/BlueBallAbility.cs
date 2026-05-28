using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlueBallAbility : BallAbility
{
    [Header("ーー 「蒼」 固有設定 ーー")]
    [SerializeField] private GameObject blueExplosionEffectPrefab; // 蒼の展開エフェクト（子要素）
    [SerializeField] private GameObject blueCenterEffectPrefab;    // 蒼の中心部分のエフェクト
    [SerializeField] private GameObject blueHitEffectPrefab;       // 発動時のヒット演出用

    [Space(10)]
    [SerializeField] private float normalRadiusScale = 3.0f;       // 通常キック時のサイズ（スケール値）
    [SerializeField] private float smashRadiusScale = 6.0f;        // スマッシュキック時のサイズ（スケール値）

    [Space(10)]
    [SerializeField] private float normalPullSpeed = 5.0f;         // 通常時の引き寄せ速度
    [SerializeField] private float smashPullSpeed = 9.0f;          // スマッシュ時の引き寄せ速度

    [Space(10)]
    [SerializeField] private float normalDuration = 3.0f;          // 通常時の持続時間
    [SerializeField] private float smashDuration = 5.0f;           // スマッシュ時の持続時間

    // インスペクターから「蒼」を展開させるレイヤーを複数選択できるようにする
    [SerializeField] private LayerMask deployTargetLayers;

    [Header("Smoke Effects")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private GameObject kokusenSmokeEffectPrefab;
    [SerializeField] private GameObject kokusenThunderEffectPrefab;

    private bool _isDeployed = false; // 術式が展開（衝突・停止）したかどうかのフラグ
    private bool _hasHitThisAction = false;
    private float _currentDuration = 0f;
    private float _currentPullSpeed = 0f;
    private float _targetScale = 1.0f;

    private GameObject _smokeEffect;
    private GameObject _thunderEffect;

    // 範囲内にいる敵のリスト（Update等での引き寄せ処理用）
    private List<Rigidbody2D> _pullTargets = new List<Rigidbody2D>();

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

        // 2. ボールの子要素としてエフェクトを生成する
        if (_isSmashFired && _isKokusenFired)
        {
            if (kokusenSmokeEffectPrefab != null && kokusenThunderEffectPrefab != null)
            {
                transform.localScale *= 1.5f;
                _smokeEffect = Instantiate(kokusenSmokeEffectPrefab, transform.position, Quaternion.identity);
                _thunderEffect = Instantiate(kokusenThunderEffectPrefab, transform.position, Quaternion.identity);
                _smokeEffect.transform.SetParent(transform);
                _thunderEffect.transform.SetParent(transform);
            }
        }
        else
        {
            if (smokeEffectPrefab != null)
            {
                transform.localScale *= 1.5f;
                _smokeEffect = Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);
                _smokeEffect.transform.SetParent(transform);
            }
        }

        // 💡 蒼の仕様：スマッシュか否かで「持続時間」「引き寄せ速度」「展開サイズ」をキャッシュ
        _currentDuration = _isSmashFired ? smashDuration : normalDuration;
        _currentPullSpeed = _isSmashFired ? smashPullSpeed : normalPullSpeed;
        _targetScale = _isSmashFired ? smashRadiusScale : normalRadiusScale;

        Debug.Log($"🔵 術式「蒼」放たれる！ (Smash: {_isSmashFired})");
    }

    /// <summary>
    /// 基底クラスのOnHitをoverrideして「蒼」の展開トリガーにする
    /// </summary>
    public override void OnHit(Collider2D collider)
    {
        if (_isDeployed || !isKicked) return;
        if (_hasHitThisAction) return;

        _hasHitThisAction = true;

        // 衝突した相手が指定レイヤーなら展開
        if ((deployTargetLayers.value & (1 << collider.gameObject.layer)) != 0)
        {
            DeployBlue();
        }
    }

    /// <summary>
    /// 術式「蒼」をその場に展開する中心ロジック
    /// </summary>
    private void DeployBlue()
    {
        _isDeployed = true;

        FxManager.Instance.Play("BlueBallHit", transform);
        if (blueHitEffectPrefab != null)
        {
            GameObject blueHitEffect = Instantiate(blueHitEffectPrefab, transform.position, Quaternion.identity);
        }

        // 1. その場で完全停止・物理固定
        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _rigidbody.bodyType = RigidbodyType2D.Kinematic;

        // 2. Ball自体のRendererを非アクティブ、古いエフェクトを削除
        if (_renderer != null) _renderer.enabled = false;
        if (_smokeEffect != null) Destroy(_smokeEffect);
        if (_thunderEffect != null) Destroy(_thunderEffect);

        // 3. コライダーをトリガー（吸引範囲用）に切り替える
        _collider.isTrigger = true;


        // 4. 同時に子要素として吸い込み演出エフェクトを生成
        if (blueExplosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(blueExplosionEffectPrefab, transform.position, Quaternion.identity);
            GameObject centerEffect = Instantiate(blueCenterEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.SetParent(transform);
            centerEffect.transform.SetParent(transform);
        }

        // 5. 【蒼の仕様】いきなり大きくなる（一瞬で吸引範囲を展開）
        transform.localScale = Vector3.one * _targetScale;

        Debug.Log($"🧲 術式「蒼」展開！！ サイズ: {_targetScale}");

        // 6. 制限時間後に消滅させるカウントダウンを開始
        StartCoroutine(DurationCoroutine());
    }

    /// <summary>
    /// 毎フレーム、範囲内にいる敵を中心に向かってゆっくり引き寄せる物理処理
    /// </summary>
    private void FixedUpdate()
    {
        if (!_isDeployed) return;

        // 登録された吸い込み対象の敵を1体ずつ中心へ引っ張る
        for (int i = _pullTargets.Count - 1; i >= 0; i--)
        {
            Rigidbody2D enemyRb = _pullTargets[i];

            // 敵が途中でDestroyされた場合のNullチェック
            if (enemyRb == null)
            {
                _pullTargets.RemoveAt(i);
                continue;
            }

            // 敵から「蒼（自分）」の中心へ向かうベクトルを計算
            Vector2 directionToCenter = ((Vector2)transform.position - enemyRb.position).normalized;

            // 中心との距離を計測
            float distance = Vector2.Distance(transform.position, enemyRb.position);

            if (distance > 0.1f) // 中心に完全に重なる手前まで引き寄せる
            {
                // 🛠️ 敵の元々の速度を活かしつつ、引き寄せベクトルを徐々に上書き（吸い込み挙動）
                enemyRb.linearVelocity = Vector2.MoveTowards(enemyRb.linearVelocity, directionToCenter * _currentPullSpeed, _currentPullSpeed * Time.fixedDeltaTime * 10f);
            }
            else
            {
                // 中心にほぼ到達したら、軌道を安定させるために速度を少し減衰
                enemyRb.linearVelocity = Vector2.MoveTowards(enemyRb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 5f);
            }
        }
    }

    /// <summary>
    /// 範囲内に入り続けている物体を検出（CollisionDetectorのインスペクターイベント用メソッド）
    /// ※「赫」のJutsushikiAkaと同様、CollisionDetectorのOnTriggerStayからここに繋げてください。
    /// </summary>
    public void JutsushikiAo(Collider2D collider)
    {
        if (!_isDeployed) return;

        if (collider.CompareTag("Enemy"))
        {
            Rigidbody2D enemyRb = collider.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                // まだリストに入っていなければ引き寄せ対象に追加
                if (!_pullTargets.Contains(enemyRb))
                {
                    _pullTargets.Add(enemyRb);
                }
            }
        }
    }

    /// <summary>
    /// 範囲内から敵が出ていったときの処理
    /// ※必要に応じてCollisionDetectorのOnTriggerExitにこのメソッドを紐付けてください
    /// </summary>
    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            Rigidbody2D enemyRb = collider.GetComponent<Rigidbody2D>();
            if (enemyRb != null && _pullTargets.Contains(enemyRb))
            {
                _pullTargets.Remove(enemyRb);
            }
        }
    }

    /// <summary>
    /// 一定時間経過後にBall（術式）をDestroyするコルーチン
    /// </summary>
    private IEnumerator DurationCoroutine()
    {
        yield return new WaitForSeconds(_currentDuration);

        Debug.Log("🔵 術式「蒼」が制限時間に達したため消滅します");

        // 後処理：引き寄せられていた敵のリストをクリア
        _pullTargets.Clear();

        if (GetComponent<CollisionDetector>() != null) GetComponent<CollisionDetector>().enabled = false;
        _collider.enabled = false;

        Destroy(gameObject);
    }

    /// <summary>
    /// 空振り時（何にも当たらずに失速した時）の自動展開ロジック
    /// </summary>
    protected override IEnumerator DestroyABall()
    {
        yield return new WaitUntil(() => _rigidbody != null && _rigidbody.linearVelocity.magnitude <= 2f);

        if (_isDeployed || this == null) yield break;

        Debug.Log("🎯 空間起爆：何にも当たらなかったため、最大到達点で「蒼」を自動展開します。");
        yield return new WaitForSeconds(ballLifeTime);

        DeployBlue();
    }
}