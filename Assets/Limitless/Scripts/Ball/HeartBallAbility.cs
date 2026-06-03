using UnityEngine;

public class HeartBallAbility : BallAbility
{
    [Header("ーー ❤️ 回復量設定（最大体力の何％か） ーー")]
    [Range(0f, 1f)][SerializeField] private float normalHealRate = 0.1f;    // 通常キック時 (例: 10%)
    [Range(0f, 1f)][SerializeField] private float smashHealRate = 0.25f;  // 💥 スマッシュ時 (例: 25%)
    [Range(0f, 1f)][SerializeField] private float kokusenHealRate = 0.6f;  // ✨ 黒閃時 (例: 60%)

    [Header("ーー ❤️ 回復用の特殊エフェクト (任意) ーー")]
    [SerializeField] private GameObject healEffectPrefab;

    /// <summary>
    /// プレイヤーに蹴られた瞬間に呼び出される処理をオーバーライド
    /// </summary>
    public override void Fire(Vector2 direction, float force, bool isSmash, float gapY)
    {
        if (isKicked) return;

        // 1. 各種キックフラグを確立
        isKicked = true;
        _isSmashFired = isSmash;

        // ジャスト入力判定（黒閃）
        if (Mathf.Abs(gapY) <= 0.01)
        {
            _isKokusenFired = true;
        }

        // 2. キック時の共通エフェクト・SEの再生（親のメソッドを呼び出し）
        PlayKickEffect();

        // 3. 回復対象（プレイヤー）を特定して、新設したHealメソッドを実行
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            MobStatus playerStatus = playerObj.GetComponent<MobStatus>();
            if (playerStatus != null)
            {
                ExecuteHealing(playerStatus);
            }
        }
        else
        {
            Debug.LogWarning("⚠️ HeartBall: 'Player' タグのオブジェクトが見つかりませんでした。");
        }

        // 4. 固有の追加演出があれば実行
        OnFire();

        // 5. 蹴って飛ばす必要はないので、その場で即座にオブジェクトを消滅させる
        Destroy(gameObject);
    }

    /// <summary>
    /// 状態に応じた回復量の計算と、Healメソッドの実行
    /// </summary>
    private void ExecuteHealing(MobStatus targetStatus)
    {
        float healPercent = normalHealRate;

        // キックの状態に応じて回復割合を分岐
        if (_isSmashFired && _isKokusenFired)
        {
            healPercent = kokusenHealRate;
        }
        else if (_isSmashFired)
        {
            healPercent = smashHealRate;
        }

        // 最大体力（LifeMax）をベースに実際の回復数値を計算
        int healAmount = Mathf.RoundToInt(targetStatus.LifeMax * healPercent);

        // ✨ MobStatusに新設したHealメソッドをスマートに呼び出す！
        targetStatus.Heal(healAmount);

        // 回復時の固有ビジュアルエフェクトを生成
        if (healEffectPrefab != null)
        {
            Instantiate(healEffectPrefab, targetStatus.transform.position, Quaternion.identity);
        }

        // 回復専用のSEなどを追加で鳴らす（もしあれば）
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySEAtPosition("Heal", targetStatus.transform.position);
        }
    }

    /// <summary>
    /// 抽象メソッドの義務。即Destroyするため空でOK
    /// </summary>
    protected override void OnFire()
    {
    }
}