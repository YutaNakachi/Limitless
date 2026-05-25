using UnityEngine;

public class NormalBallAbility : BallAbility
{
    [Header("ーー エフェクト設定 ーー")]
    [SerializeField] private GameObject smokeEffectPrefab;
    [SerializeField] private float effectDestroyTime = 2.0f; // ボールが飛んでいる間、煙を出し続ける時間

    protected override void OnFire()
    {
        // 1. 画面揺れやヒットストップ演出
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