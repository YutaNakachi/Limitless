using UnityEngine;

public class NormalBallAbility : BallAbility
{
    [Header("Smoke Effects")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private GameObject kokusenSmokeEffectPrefab;
    [SerializeField] private GameObject kokusenThunderEffectPrefab;
    [SerializeField] private float effectDestroyTime = 2.0f; // ボールが飛んでいる間、煙を出し続ける時間


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
                transform.localScale *= 2f;

                GetComponent<Renderer>().enabled = false;

                // ボールと同じ位置に生成
                GameObject kokusenSmoke = Instantiate(kokusenSmokeEffectPrefab, transform.position, Quaternion.identity);
                GameObject thunder = Instantiate(kokusenThunderEffectPrefab, transform.position, Quaternion.identity);

                // 💡 ここが核心！ボールの子供にすることで、煙の「噴射口」をボールに追従させる
                kokusenSmoke.transform.SetParent(transform);
                thunder.transform.SetParent(transform);

                // ボールが飛んでいる想定の時間（例: 2秒後）が来たら、噴射口ごと綺麗に消去する
                Destroy(kokusenSmoke, effectDestroyTime);
                Destroy(thunder, effectDestroyTime);
            }
        }
        else
        {
            if (smokeEffectPrefab != null)
            {
                transform.localScale *= 1.5f;

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