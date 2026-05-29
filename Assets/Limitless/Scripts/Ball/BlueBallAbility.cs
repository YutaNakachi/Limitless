using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlueBallAbility : BallAbility
{
    [Header("ーー 「蒼」 固有設定 ーー")]
    [SerializeField] private GameObject blueExplosionEffectPrefab;
    [SerializeField] private GameObject blueCenterEffectPrefab;
    [SerializeField] private GameObject blueHitEffectPrefab;

    [Header("ーー 吸引・回転設定 ーー")]
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("吸い込み中に敵を回転させる速度（度/秒）")]
    [SerializeField] private float rotationSpeed = 360f; // 👈 1秒間に1回転する速度（インスペクターで調整してね！）

    [Space(10)]
    [SerializeField] private float normalRadiusScale = 3.0f;
    [SerializeField] private float smashRadiusScale = 6.0f;

    [Space(10)]
    [SerializeField] private float normalPullSpeed = 5.0f;
    [SerializeField] private float smashPullSpeed = 9.0f;

    [Space(10)]
    [SerializeField] private float normalDuration = 3.0f;
    [SerializeField] private float smashDuration = 5.0f;

    [SerializeField] private LayerMask deployTargetLayers;

    [Header("Smoke Effects")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private GameObject kokusenSmokeEffectPrefab;
    [SerializeField] private GameObject kokusenThunderEffectPrefab;

    private bool _isDeployed = false;
    private bool _hasHitThisAction = false;
    private float _currentDuration = 0f;
    private float _currentPullSpeed = 0f;
    private float _targetScale = 1.0f;

    private GameObject _smokeEffect;
    private GameObject _thunderEffect;

    // 現在吸い込み中の敵を記憶しておくリスト
    private List<MobStatus> _currentlyPulledEnemies = new List<MobStatus>();


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
        _currentPullSpeed = _isSmashFired ? smashPullSpeed : normalPullSpeed;
        _targetScale = _isSmashFired ? smashRadiusScale : normalRadiusScale;

        Debug.Log($"🔵 術式「蒼」放たれる！ (Smash: {_isSmashFired})");
    }

    public override void OnHit(Collider2D collider)
    {
        if (_isDeployed || !isKicked) return;
        if (_hasHitThisAction) return;

        if ((deployTargetLayers.value & (1 << collider.gameObject.layer)) != 0)
        {
            _hasHitThisAction = true;
            DeployBlue();
        }
    }

    private void DeployBlue()
    {
        _isDeployed = true;

        FxManager.Instance.Play("BlueBallHit", transform);
        if (blueHitEffectPrefab != null)
        {
            GameObject blueHitEffect = Instantiate(blueHitEffectPrefab, transform.position, Quaternion.identity);
        }

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.angularVelocity = 0f;
        _rigidbody.bodyType = RigidbodyType2D.Kinematic;

        if (_renderer != null) _renderer.enabled = false;
        if (_smokeEffect != null) Destroy(_smokeEffect);
        if (_thunderEffect != null) Destroy(_thunderEffect);

        _collider.isTrigger = true;

        if (blueExplosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(blueExplosionEffectPrefab, transform.position, Quaternion.identity);
            GameObject centerEffect = Instantiate(blueCenterEffectPrefab, transform.position, Quaternion.identity);
            effect.transform.SetParent(transform);
            centerEffect.transform.SetParent(transform);
        }

        transform.localScale = Vector3.one * _targetScale;

        Debug.Log($"🧲 術式「蒼」展開！！ サイズ: {_targetScale}");

        StartCoroutine(DurationCoroutine());
    }

    private void FixedUpdate()
    {
        if (!_isDeployed) return;

        MobStatus[] allEnemies = FindObjectsByType<MobStatus>(FindObjectsSortMode.None);

        _currentlyPulledEnemies.Clear();

        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            if (enemy.CompareTag("Player")) continue;

            Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
            if (enemyRb == null) continue;

            Vector2 offset = (Vector2)transform.position - enemyRb.position;
            float distance = offset.magnitude;

            if (distance < _targetScale)
            {
                if (enemyRb.IsSleeping()) enemyRb.WakeUp();

                _currentlyPulledEnemies.Add(enemy);

                // 🌀【位置変更】引き寄せ中だろうが、中心に到達していようが、
                // 範囲内にいる間は「毎フレーム絶え間なく」くるくる回転させ続ける！
                enemy.transform.Rotate(0, 0, rotationSpeed * Time.fixedDeltaTime);

                if (distance > 0.1f)
                {
                    enemy.ForceApplyPullState();

                    Vector2 pullVector = (offset / distance) * _currentPullSpeed;
                    enemyRb.linearVelocity += pullVector * Time.fixedDeltaTime;
                }
                else
                {
                    // 中心に到達した状態
                    enemyRb.linearVelocity = Vector2.zero;

                    // 💡 座標を中心点に完全に重ねて吸い込み完了を維持（ガタつき防止）
                    enemyRb.position = transform.position;

                    enemy.ForceApplyPullState();
                    // （ここで Quaternion.identity に戻す処理を削除したため、中心でも回り続けます！）
                }
            }
            else
            {
                // 吸引範囲から外れた（吹き飛ばされた等）敵は、角度とステートをここでリセット
                if (enemy.IsKnockbacking)
                {
                    enemy.transform.rotation = Quaternion.identity;
                    enemy.ForceResetToNormalState();
                }
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

        // 🛠️【解放＆回転リセット】術式が消えた瞬間に、中心で回っていた敵全員を一斉に正面に戻す！
        foreach (var enemy in _currentlyPulledEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            // 💡 ここで角度をぴったり正面（回転なし）に戻す！
            enemy.transform.rotation = Quaternion.identity;

            if (enemy.IsKnockbacking)
            {
                enemy.ForceResetToNormalState();
            }
        }

        _currentlyPulledEnemies.Clear();

        if (GetComponent<CollisionDetector>() != null) GetComponent<CollisionDetector>().enabled = false;
        _collider.enabled = false;

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
                Debug.Log("🎯 空間起爆：ボールが失速したため「蒼」を自動展開します。");
                DeployBlue();
                yield break;
            }

            if (timer >= ballLifeTime)
            {
                Debug.Log("🕒 空間起爆：最大寿命に達したため「蒼」を自動展開します。");
                DeployBlue();
                yield break;
            }

            yield return null;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.25f);
        float radius = Application.isPlaying ? _targetScale : normalRadiusScale;
        Gizmos.DrawSphere(transform.position, radius);
    }
}