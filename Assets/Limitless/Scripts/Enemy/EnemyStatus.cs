using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStatus : MobStatus
{
    [Header("ーー 死亡時ぶっ飛び設定 ーー")]
    [SerializeField] private float deathKnockbackForce = 35f;
    [SerializeField] private float rotationSpeed = 720f; // 1秒間に何度回転するか（720度＝1秒に2回転）
    [SerializeField] private LayerMask groundLayer;

    [Header("ーー エフェクト設定 ーー")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private float smokeEffectDestroyTime = 2.0f; // ボールが飛んでいる間、煙を出し続ける時間
    // 🔥 エフェクトを単数形から、複数のPrefabを格納できる「リスト」に変更
    // インスペクターで好きなだけ（例えば5種類など）エフェクトを登録できるようになります
    [SerializeField] private List<GameObject> deathExplosionEffectPrefabs;

    private bool _isFlyingToDeathWall = false; // 死亡飛行中フラグ

    private void Update()
    {
        // 🔥 死亡飛行中なら、毎フレームクルクル回転させる
        if (_isFlyingToDeathWall)
        {
            // Z軸を中心に回転（Time.deltaTimeをかけることで処理落ちしても一定の速度になります）
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }
    }

    protected override void OnDie()
    {
        base.OnDie();

        // 画面揺れやヒットストップ演出
        FxManager.Instance.Play("EnemyFatalHit", transform);

        // 🔥 【新設】Z軸の回転固定を解除して、物理で回るようにする！
        _rigidbody.freezeRotation = false;

        // 360度完全にランダムな方向
        Vector2 randomDirection = Random.insideUnitCircle.normalized;

        _isFlyingToDeathWall = true;
        //_animator.SetTrigger("Death");

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.AddForce(randomDirection * deathKnockbackForce, ForceMode2D.Impulse);

        if (smokeEffectPrefab != null)
        {
            // ボールと同じ位置に生成
            GameObject smoke = Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);

            // 💡 ここが核心！ボールの子供にすることで、煙の「噴射口」をボールに追従させる
            smoke.transform.SetParent(transform);

            // ボールが飛んでいる想定の時間（例: 2秒後）が来たら、噴射口ごと綺麗に消去する
            Destroy(smoke, smokeEffectDestroyTime);
        }

        StartCoroutine(FallbackDestroyCoroutine());
    }

    /// <summary>
    /// 💡 【超重要】壁に激突した瞬間を検知する
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 死亡飛行中、かつぶつかった相手が「壁や床（groundLayer）」だった場合
        if (_isFlyingToDeathWall && ((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            TriggerWallExplosion(collision);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // トリガーコライダーで壁を検知している場合も同様に処理
        if (_isFlyingToDeathWall && ((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            // トリガーの場合は簡易的に自分の位置で爆発
            TriggerWallExplosion(null);
        }
    }

    /// <summary>
    /// 壁激突時の大爆発演出（リストからランダム生成版）
    /// </summary>
    private void TriggerWallExplosion(Collision2D collision)
    {
        _isFlyingToDeathWall = false;

        // 激突した正確な座標を取得
        Vector3 spawnPos = transform.position;
        if (collision != null && collision.contactCount > 0)
        {
            spawnPos = collision.contacts[0].point;
        }

        // 🔥 【大修正】リストの中からランダムに1つ選んで生成する！
        if (deathExplosionEffectPrefabs != null && deathExplosionEffectPrefabs.Count > 0)
        {
            // 💡 1. リストのサイズ（登録数）から、ランダムな「インデックス（背番号）」を決める
            int randomIndex = Random.Range(0, deathExplosionEffectPrefabs.Count);

            // 💡 2. 決まったインデックスのエフェクトPrefabを取得する
            GameObject selectedEffectPrefab = deathExplosionEffectPrefabs[randomIndex];

            // 💡 3. それを生成する
            if (selectedEffectPrefab != null)
            {
                Instantiate(selectedEffectPrefab, spawnPos, Quaternion.identity);
            }
        }

        // 自身を即座に消滅させる
        Destroy(gameObject);
    }

    private IEnumerator FallbackDestroyCoroutine()
    {
        yield return new WaitForSeconds(3.0f);

        // 🔥 【大修正】リストの中からランダムに1つ選んで生成する！
        if (deathExplosionEffectPrefabs != null && deathExplosionEffectPrefabs.Count > 0)
        {
            // 💡 1. リストのサイズ（登録数）から、ランダムな「インデックス（背番号）」を決める
            int randomIndex = Random.Range(0, deathExplosionEffectPrefabs.Count);

            // 💡 2. 決まったインデックスのエフェクトPrefabを取得する
            GameObject selectedEffectPrefab = deathExplosionEffectPrefabs[randomIndex];

            // 💡 3. それを生成する
            if (selectedEffectPrefab != null)
            {
                Instantiate(selectedEffectPrefab, transform.position, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }
}