using UnityEngine;

public class NormalBallAbility : BallAbility
{
    [Header("Smoke Effects")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private GameObject smashSmokeEffectPrefab;
    [SerializeField] private GameObject smashThunderEffectPrefab;
    [SerializeField] private float effectDestroyTime = 2.0f; // ボールが飛んでいる間、煙を出し続ける時間


    protected override void OnFire()
    {
        // 1. 画面揺れやヒットストップ演出

        if (_isSmashFired)
        {
            FxManager.Instance.Play("SmashBallKick", transform);
            // 2. 🔥 【ボールの子要素として生成する】
            if (smashSmokeEffectPrefab != null && smashThunderEffectPrefab != null)
            {
                // ボールと同じ位置に生成
                GameObject smashSmoke = Instantiate(smashSmokeEffectPrefab, transform.position, Quaternion.identity);
                GameObject thunder = Instantiate(smashThunderEffectPrefab, transform.position, Quaternion.identity);

                // 💡 ここが核心！ボールの子供にすることで、煙の「噴射口」をボールに追従させる
                smashSmoke.transform.SetParent(transform);
                thunder.transform.SetParent(transform);

                // ボールが飛んでいる想定の時間（例: 2秒後）が来たら、噴射口ごと綺麗に消去する
                Destroy(smashSmoke, effectDestroyTime);
                Destroy(thunder, effectDestroyTime);
            }
        }
        else
        {
            FxManager.Instance.Play("NormalBallKick", transform);
            // 2. 🔥 【ボールの子要素として生成する】
            if (smokeEffectPrefab != null)
            {
                // ボールと同じ位置に生成
                GameObject smoke = Instantiate(smokeEffectPrefab, transform.position, Quaternion.identity);

                // 💡 ここが核心！ボールの子供にすることで、煙の「噴射口」をボールに追従させる
                smoke.transform.SetParent(transform);

                // ボールが飛んでいる想定の時間（例: 2秒後）が来たら、噴射口ごと綺麗に消去する
                Destroy(smoke, effectDestroyTime);
            }
        }

    }
}