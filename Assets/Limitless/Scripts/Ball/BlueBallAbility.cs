using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlueBallAbility : BallAbility
{
    [Header("ーー 「蒼」 固有設定 ーー")]
    [SerializeField] private GameObject blueExplosionEffectPrefab; // 蒼の展開エフェクト（子要素）
    [SerializeField] private GameObject blueCenterEffectPrefab;    // 蒼の中心部分のエフェクト
    [SerializeField] private GameObject blueHitEffectPrefab;       // 発動時のヒット演出用

    [Header("ーー 吸引レイヤー設定 ーー")]
    [SerializeField] private LayerMask enemyLayer; // 👈 インスペクターで「Enemy」レイヤーを指定してください

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


        // 衝突した相手が指定レイヤーなら展開
        if ((deployTargetLayers.value & (1 << collider.gameObject.layer)) != 0)
        {
            _hasHitThisAction = true;
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

    private void FixedUpdate()
    {
        if (!_isDeployed) return;

        // 💡 罠1への対策：半径の計算を大きくする
        // 見た目のエフェクトに対して、実際の数学的なサーチ円が小さすぎた可能性が高いです。
        // * 0.5f を外して、確実に広い範囲（スケール値そのままの半径）でサーチさせます。
        float pullRadius = _targetScale;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, pullRadius, enemyLayer);

        _pullTargets.Clear();

        foreach (var col in hitEnemies)
        {
            // 💡 罠2への対策：コライダーが子要素にあるとGetComponentは失敗する！
            // Unityの超有能機能「attachedRigidbody」を使います。
            // これなら、当たり判定（Collider）が子要素の「HitBox」にあっても、親のRigidbodyを確実に見つけ出します。
            Rigidbody2D enemyRb = col.attachedRigidbody;
            if (enemyRb == null) continue;

            // 敵のステータス取得も、大元のRigidbodyがあるオブジェクトから取得する
            MobStatus enemyStatus = enemyRb.GetComponent<MobStatus>();
            if (enemyStatus != null && enemyStatus.IsDead) continue;

            if (!_pullTargets.Contains(enemyRb))
            {
                if (enemyRb.IsSleeping()) enemyRb.WakeUp();
                _pullTargets.Add(enemyRb);
            }
        }

        // --- 吸引物理ループ ---
        for (int i = 0; i < _pullTargets.Count; i++)
        {
            Rigidbody2D enemyRb = _pullTargets[i];
            Vector2 directionToCenter = ((Vector2)transform.position - enemyRb.position).normalized;
            float distance = Vector2.Distance(transform.position, enemyRb.position);

            if (distance > 0.1f)
            {
                // 💡 罠3への対策：敵自身の「歩行スクリプト」に引力が負けているのを防ぐ！
                // MoveTowards（徐々に変化）をやめて、有無を言わさず速度ベクトルを【完全上書き】します。
                enemyRb.linearVelocity = directionToCenter * _currentPullSpeed;
            }
            else
            {
                // 中心に到達したら完全に固定し、ガタガタ震えるのを防ぐ
                enemyRb.linearVelocity = Vector2.zero;
                enemyRb.position = transform.position;
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
    /// 🛠️【修正】空振り時（何にも当たらずに失速、または最大寿命に達した時）の空間起爆ロジック
    /// </summary>
    protected override IEnumerator DestroyABall()
    {
        // 蹴り出されてから、純粋に失速するか、または最大寿命に達するまでカウントするタイマー
        float timer = 0f;

        // 「すでに展開済み」または「ボールが消滅」しない限りループ
        while (!_isDeployed && this != null)
        {
            timer += Time.deltaTime;

            // 💡 判定をすり抜けないための安全策：
            // 蹴り出されて少し時間が経ち（0.1秒以上）、かつ速度が閾値（0.5f）以下になった場合
            if (timer > 0.5f && _rigidbody != null && _rigidbody.linearVelocity.magnitude <= 0.5f)
            {
                Debug.Log("🎯 空間起爆：ボールが失速したため「赫」を自動展開します。");
                DeployBlue();
                yield break;
            }

            // 💡 速度が落ちなくても、設定された最大寿命（例: 2〜3秒）に達した場合
            if (timer >= ballLifeTime)
            {
                Debug.Log("🕒 空間起爆：最大寿命（タイムアウト）に達したため「赫」を自動展開します。");
                DeployBlue();
                yield break;
            }

            yield return null; // 毎フレーム監視
        }
    }

    // 🛠️【新規追加】Unityエディタの画面に「実際の吸引判定の円」を描画する魔法のメソッド
    // これをクラスの一番下（} の手前）に貼り付けてください。
    private void OnDrawGizmos()
    {
        // エディタのSceneビューで、蒼の「見えない吸引範囲」が青い半透明の円で表示されるようになります！
        Gizmos.color = new Color(0f, 0f, 1f, 0.3f);

        // 実行中は現在の _targetScale を、実行前はインスペクターの normalRadiusScale を使って描画
        float radius = Application.isPlaying ? _targetScale : normalRadiusScale;
        Gizmos.DrawSphere(transform.position, radius);
    }
}