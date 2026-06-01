using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RedBallAbility : BallAbility
{
    [Header("ーー 「赫」 固有設定 ーー")]
    [SerializeField] private GameObject redExplosionEffectPrefab; // 赫の展開エフェクト（子要素になる）
    [SerializeField] private GameObject redCenterEffectPrefab; // 赫の中心部分のエフェクト
    [SerializeField] private GameObject redThunderEffectPrefab;
    [SerializeField] private GameObject redHitEffectPrefab;

    [Header("ーー ノックバック・吸引設定 ーー")]
    [Tooltip("チェックを入れると『中心へ吸引（反転）』、外すと『外側へ吹き飛ばし（通常）』になります")]
    [SerializeField] private bool isPullMode = true; // 👈 インスペクターで切り替え可能！

    [Space(10)]
    [SerializeField] private float normalExpandSpeed = 2f;       // 通常時の拡大速度
    [SerializeField] private float smashExpandSpeed = 3.5f;     // スマッシュ時の拡大速度（より素早く広がる）

    [Space(10)]
    [SerializeField] private float normalDuration = 3.0f;       // 通常時の持続時間
    [SerializeField] private float smashDuration = 5.0f;        // スマッシュ時の持続時間（仕様：持続が伸びる）

    [Space(10)]
    [SerializeField] private float damageInterval = 0.5f;       // 多段ヒットの間隔（秒）

    [SerializeField] private LayerMask deployTargetLayers;

    [Header("Smoke Effects")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private GameObject kokusenSmokeEffectPrefab;
    [SerializeField] private GameObject kokusenThunderEffectPrefab;

    private bool _isDeployed = false; // 術式が展開（衝突・停止）したかどうかのフラグ
    private bool _hasHitThisAction = false;
    private float _currentDuration = 0f;
    private float _currentExpandSpeed = 0f;
    private int _finalDamage = 0;
    private GameObject _smokeEffect;
    private GameObject _thunderEffect;

    // 敵ごとの無敵時間を管理する辞書（連続ダメージの間隔制御用）
    private Dictionary<EnemyStatus, float> _enemyDamageTimers = new Dictionary<EnemyStatus, float>();


    protected override void OnFire()
    {
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

        _currentDuration = _isSmashFired ? smashDuration : normalDuration;
        _currentExpandSpeed = _isSmashFired ? smashExpandSpeed : normalExpandSpeed;
        _finalDamage = _isSmashFired ? smashAttackDamage : attackDamage;

        Debug.Log($"🔴 術式「赫」放たれる！ (Smash: {_isSmashFired})");
    }

    public override void OnHit(Collider2D collider)
    {
        if (_isDeployed || !isKicked) return;
        if (_hasHitThisAction) return;

        if ((deployTargetLayers.value & (1 << collider.gameObject.layer)) != 0)
        {
            _hasHitThisAction = true;
            DeployRed().Forget();
        }
    }

    private async UniTaskVoid DeployRed()
    {
        _isDeployed = true;

        FxManager.Instance.Play("RedBallHit", transform);
        SoundManager.Instance.PlaySEAtPosition("JutsushikiFire", transform.position);


        if (redHitEffectPrefab != null)
        {
            GameObject redHitEffect = Instantiate(redHitEffectPrefab, transform.position, Quaternion.identity);
        }

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _rigidbody.bodyType = RigidbodyType2D.Kinematic;

        if (_renderer != null) _renderer.enabled = false;

        if (_smokeEffect != null) Destroy(_smokeEffect);
        if (_thunderEffect != null) Destroy(_thunderEffect);

        _collider.isTrigger = true;

        // 🚀【最重要】展開した瞬間、自身のタグを「Untagged（無所属）」に変更する！
        // これにより、PlayerShootの「if (!collider.CompareTag("Ball")) return;」のチェックをすり抜けるようになります。
        gameObject.tag = "Untagged";

        await UniTask.WaitUntil(() => Time.timeScale >= 1.0f, PlayerLoopTiming.Update);

        SoundManager.Instance.PlaySEAtPosition("Aka", transform.position);

        if (redExplosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(redExplosionEffectPrefab, transform.position, Quaternion.identity);
            GameObject centerEffect = Instantiate(redCenterEffectPrefab, transform.position, Quaternion.identity);
            GameObject thunderEffect = Instantiate(redThunderEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.SetParent(transform);
            centerEffect.transform.SetParent(transform);
            thunderEffect.transform.SetParent(transform);
        }

        Debug.Log("💥 術式「赫」展開！！");

        StartCoroutine(DurationCoroutine());
    }

    private void Update()
    {
        if (!_isDeployed) return;

        transform.localScale += Vector3.one * _currentExpandSpeed * Time.deltaTime;

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

                if (!_enemyDamageTimers.ContainsKey(target))
                {
                    _enemyDamageTimers[target] = 0f;
                }

                if (_enemyDamageTimers[target] <= 0f)
                {
                    // 1. 赫の中心（自分）から敵への方向ベクトルを計算
                    Vector2 pushDirection = ((Vector2)collider.transform.position - (Vector2)transform.position).normalized;

                    // 2. フラグによって偽装する攻撃者座標の生成位置をスイッチ
                    Vector2 finalAttackerPosition;

                    if (isPullMode)
                    {
                        // 【吸引モード】敵のさらに外側にダミーの座標を置く（敵は中心に吸い寄せられる）
                        finalAttackerPosition = (Vector2)collider.transform.position + (pushDirection * 2f);
                        Debug.Log($"🩸 「赫」引き込み持続ダメージ: {collider.gameObject.name} (中心へ吸引)");
                    }
                    else
                    {
                        // 【吹き飛ばしモード】赫の「中心点そのもの」を攻撃者座標にする（敵は外側にドンッ！と弾き飛ぶ）
                        finalAttackerPosition = (Vector2)transform.position;
                        Debug.Log($"💥 「赫」弾き飛ばし持続ダメージ: {collider.gameObject.name} (外側へ吹き飛ばし)");
                    }

                    // 3. 決定した座標を渡してダメージ処理を実行
                    target.TakeDamage(_finalDamage, finalAttackerPosition);

                    PlayHitEffect(collider);

                    // タイマーをリセット
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

    private IEnumerator DurationCoroutine()
    {
        yield return new WaitForSeconds(_currentDuration);

        Debug.Log("🔴 術式「赫」が制限時間に達したため消滅します");

        if (GetComponent<CollisionDetector>() != null) GetComponent<CollisionDetector>().enabled = false;
        _collider.enabled = false;

        SoundManager.Instance.StopLoopSE("Aka");
        Destroy(gameObject);
    }

    protected override IEnumerator DestroyABall()
    {
        float timer = 0f;

        while (!_isDeployed && this != null)
        {
            timer += Time.deltaTime;

            if (timer > 0.5f && _rigidbody != null && _rigidbody.linearVelocity.magnitude <= 0.5f)
            {
                Debug.Log("🎯 空間起爆：ボールが失速したため「赫」を自動展開します。");
                DeployRed().Forget();
                yield break;
            }

            if (timer >= ballLifeTime)
            {
                Debug.Log("🕒 空間起爆：最大寿命に達したため「赫」を自動展開します。");
                DeployRed().Forget();
                yield break;
            }

            yield return null;
        }
    }
}