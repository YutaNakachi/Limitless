using System.Collections;
using UnityEngine;

public class AquaBallAbility : BallAbility
{
    [Header("ーー 🌊 バリア持続時間設定（秒） ーー")]
    [SerializeField] private float normalBarrierDuration = 3.0f;  // 通常キック時 (例: 3秒)
    [SerializeField] private float smashBarrierDuration = 6.0f;   // 💥 スマッシュ時 (例: 6秒)
    [SerializeField] private float kokusenBarrierDuration = 12.0f; // ✨ 黒閃時 (例: 12秒)

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

        // 2. キック時の共通エフェクト・SEの再生
        PlayKickEffect();

        // 3. プレイヤーを特定し、バリア展開コルーチンを始動
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            MobStatus playerStatus = playerObj.GetComponent<MobStatus>();

            // 💡 プレイヤーの子要素にあるバリア用のGameObjectを探す
            // ここでは、プレイヤーの子要素から「Barrier」という名前のオブジェクトを自動探索します
            Transform barrierTransform = playerObj.transform.Find("Barrier");

            if (playerStatus != null && barrierTransform != null)
            {
                // プレイヤーのMonoBehaviour（ゲーム内で破壊されにくい実体）を利用してコルーチンを起動
                // ※ボール自身（this）は直後にDestroyされてしまうため、プレイヤー側にコルーチンを実行させます
                playerStatus.StartCoroutine(BarrierRoutine(playerStatus, barrierTransform.gameObject));
            }
            else if (barrierTransform == null)
            {
                Debug.LogError("⚠️ AquaBall: プレイヤーの子要素に 'Barrier' という名前のGameObjectが見つかりません！");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ AquaBall: 'Player' タグのオブジェクトが見つかりませんでした。");
        }

        // 4. 固有の追加演出があれば実行
        OnFire();

        // 5. 即座にオブジェクトを消滅させる
        Destroy(gameObject);
    }

    /// <summary>
    /// バリアの展開・無敵化・時間監視・解除を行うコルーチン
    /// </summary>
    private IEnumerator BarrierRoutine(MobStatus playerStatus, GameObject barrierObject)
    {
        // 死亡している場合は起動しない
        if (playerStatus.IsDead) yield break;

        // キックの状態に応じて持続時間を分岐
        float duration = normalBarrierDuration;
        string barrierTypeStr = "通常バリア";

        if (_isSmashFired && _isKokusenFired)
        {
            duration = kokusenBarrierDuration;
            barrierTypeStr = "✨絶対結界（黒閃）✨";
        }
        else if (_isSmashFired)
        {
            duration = smashBarrierDuration;
            barrierTypeStr = "💥大バリア（スマッシュ）💥";
        }

        Debug.Log($"🌊 AquaBall [{barrierTypeStr}]: {duration}秒間展開します。");

        // 🛡️ 1. バリアの開始処理
        barrierObject.SetActive(true);      // ビジュアルをアクティブに
        playerStatus.SetInvicible();        // MobStatusの無敵化メソッドを呼び出し

        // 水系の専用追加SEがあれば鳴らす（例: びしゃん、シュワァァなど）
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySEAtPosition("BarrierUp", playerStatus.transform.position);
        }

        // ⏳ 2. 指定された時間（秒）だけポーズ画面などのTime.timeScaleを考慮して待機
        yield return new WaitForSeconds(duration);

        // 🔓 3. バリアの終了処理
        if (playerStatus != null)
        {
            playerStatus.CancelInvicible(); // 無敵解除

            // プレイヤーがまだ生存している場合のみ、SEを鳴らす
            if (!playerStatus.IsDead && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySEAtPosition("BarrierDown", playerStatus.transform.position);
            }
        }

        if (barrierObject != null)
        {
            barrierObject.SetActive(false); // ビジュアルを非アクティブに
        }

        Debug.Log($"🍃 AquaBall: バリアの持続時間が終了しました。");
    }

    protected override void OnFire()
    {
    }
}