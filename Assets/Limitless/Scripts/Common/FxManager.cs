using Cysharp.Threading.Tasks; // 💡 UniTaskを有効化
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem; // 🎮 Input Systemを有効化
using Random = UnityEngine.Random;

public class FxManager : MonoBehaviour
{
    public static FxManager Instance { get; private set; }

    [Header("データ一元管理アセット")]
    [SerializeField] private FxPresetData fxPresetData;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;
    private Vector3 originalCameraPos;

    [Header("ーー 大量撃破時の演出間引き設定 ーー")]
    [Tooltip("連続実行を防供ための共通クールタイム（秒）。0.05f〜0.1fあたりが最適です")]
    [SerializeField] private float fxCoolTime = 0.08f;

    // 複数同時発生時の制御用変数
    private float hitStopRemainingTime = 0f;          // 残りのヒットストップ時間
    private CancellationTokenSource _hitStopCTS;     // ヒットストップ用のキャンセル管理
    private CancellationTokenSource _cameraShakeCTS; // カメラシェイク用のキャンセル管理

    // ⏱️ 連続暴発を防ぐための絶対時間記録用
    private float _lastPlayedTime = -999f;

    /// <summary>
    /// 💡【追加】現在、演出によるヒットストップや時間停止が実行中（タイマー残あり）かどうかを返します
    /// </summary>
    public bool IsPlayingHitStop => hitStopRemainingTime > 0f;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            originalCameraPos = cameraTransform.localPosition;
    }

    private void OnDestroy()
    {
        // マネージャー自体が破棄されるときは、走っているタスクをすべて安全に強制終了する
        CleanUpCTS(ref _hitStopCTS);
        CleanUpCTS(ref _cameraShakeCTS);

        // 🎮 アプリ終了時やシーン遷移時にコントローラーが震えっぱなしになるのを防ぐ
        if (Gamepad.current != null)
        {
            Gamepad.current.ResetHaptics();
        }
    }

    /// <summary>
    /// プリセット名を指定して演出を再生。targetObjectを指定すると、そのキャラが微振動します。
    /// </summary>
    public void Play(string presetLabel, Transform targetObject = null)
    {
        if (fxPresetData == null) return;

        // 1. まず指定されたプリセット設定をアセットから取得
        FxPresetData.FxSettings settings = fxPresetData.GetPreset(presetLabel);

        // 構造体がデフォルト状態（未設定の空っぽ）なら処理を抜ける
        if (settings.Equals(default(FxPresetData.FxSettings))) return;

        // 🔥【useCoolTime対応型・グローバル防壁】
        if (settings.useCoolTime && (Time.unscaledTime - _lastPlayedTime < fxCoolTime))
        {
            // 💡 画面全体が止まるのは防ぎつつ、殴られた敵個別の「微振動（手応え）」だけは実行！
            TriggerObjectShakeOnly(settings, targetObject);
            return;
        }

        // 📝 間引かれずに「実際に全体演出が実行される時」だけ、実行時刻を更新する
        if (settings.useCoolTime)
        {
            _lastPlayedTime = Time.unscaledTime;
        }

        // 2. ヒットストップの実行（設定データをそのまま渡して振動に対応）
        if (settings.stopDuration > 0)
        {
            PlayHitStop(settings);
        }

        // 3. カメラシェイクの実行
        if (settings.shakeDuration > 0)
        {
            PlayCameraShake(settings.shakeDuration, settings.shakeMagnitude);
        }

        // 4. ヒット対象自体の微振動を実行
        if (targetObject != null && settings.objectShakeMagnitude > 0 && settings.stopDuration > 0)
        {
            StartObjectShake(targetObject, settings.stopDuration, settings.objectShakeMagnitude, settings.useObjectShakeY, targetObject.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    /// <summary>
    /// クールタイム中に、すでに取得済みの settings を使ってオブジェクトの揺れだけを適用するサブメソッド
    /// </summary>
    private void TriggerObjectShakeOnly(FxPresetData.FxSettings settings, Transform targetObject)
    {
        if (targetObject == null) return;

        if (settings.objectShakeMagnitude > 0 && settings.stopDuration > 0)
        {
            StartObjectShake(targetObject, settings.stopDuration, settings.objectShakeMagnitude, settings.useObjectShakeY, targetObject.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    // ================================================================
    // 🛡️ ヒットストップ（UniTaskによる絶対時間復帰システム）
    // ================================================================
    public void PlayHitStop(FxPresetData.FxSettings settings)
    {
        float duration = settings.stopDuration;

        // 新しく要求された停止時間が、現在の残り時間より長い場合のみ採用
        if (duration > hitStopRemainingTime)
        {
            hitStopRemainingTime = duration;

            // すでに走っているヒットストップタスクがあれば、安全に「キャンセル」して上書きする
            // 💡 新しいタスクが割り込む瞬間に、古いタスクの「finally（ResetHaptics）」が一瞬走りますが、
            // 直後に新しいタスクのモーター駆動が上書きされるため、途切れることなく滑らかに次の振動へ遷移します。
            CleanUpCTS(ref _hitStopCTS);
            _hitStopCTS = new CancellationTokenSource();

            // 非同期メソッドを非同期のまま投げっぱなし（Forget）で起動
            PlayHitStopAsync(settings, _hitStopCTS.Token).Forget();
        }
    }

    private async UniTaskVoid PlayHitStopAsync(FxPresetData.FxSettings settings, CancellationToken token)
    {
        // 最初のアニメーション速度を設定
        Time.timeScale = settings.timeScale;

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(settings.rumbleLeft, settings.rumbleRight);
        }

        try
        {
            while (hitStopRemainingTime > 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                // 💡 【重要】もしポーズ画面が開いたら、ここでカウントを止めて待機する
                if (PauseMenuManager.IsPaused)
                {
                    // ポーズ中はコントローラーの振動を一旦止める
                    if (gamepad != null) gamepad.ResetHaptics();

                    // ポーズが解除されるまで、このループ内で時間を進めずに待機
                    while (PauseMenuManager.IsPaused)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }

                    // ポーズ解除！タイムスケールと振動を「演出用の値」に復帰させる
                    Time.timeScale = settings.timeScale;
                    if (gamepad != null) gamepad.SetMotorSpeeds(settings.rumbleLeft, settings.rumbleRight);
                }

                // 通常のカウントダウン処理（ポーズ中はここを通らない）
                float dt = Time.unscaledDeltaTime;
                if (dt <= 0) dt = 0.016f;

                hitStopRemainingTime -= dt;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (gamepad != null)
            {
                gamepad.ResetHaptics();
            }

            // 💡 キャンセルされず、かつ「今ポーズ中ではない」場合のみ通常速度に戻す
            if (!token.IsCancellationRequested && !PauseMenuManager.IsPaused)
            {
                hitStopRemainingTime = 0f;
                Time.timeScale = 1.0f;
                Debug.Log("⏱️ 演出が最後まで正常終了。時間軸が復帰しました。");
            }
        }
    }

    // ================================================================
    // 🛡️ オブジェクト微振動（心中バグ完全シャットアウト）
    // ================================================================
    private async UniTaskVoid StartObjectShake(Transform target, float duration, float magnitude, bool useY, CancellationToken objectToken)
    {
        if (target == null) return;

        Vector3 originalPos = target.localPosition;
        float elapsed = 0.0f;

        try
        {
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, objectToken);

                if (target == null || !target.gameObject.activeInHierarchy) return;

                elapsed += Time.unscaledDeltaTime;

                float offsetX = Random.Range(-1f, 1f) * magnitude;
                float offsetY = useY ? Random.Range(-1f, 1f) * magnitude : 0f;

                target.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (target != null)
            {
                target.localPosition = originalPos;
            }
        }
    }

    // ================================================================
    // 🛡️ カメラシェイク（連続被弾時の位置ズレ防止回路）
    // ================================================================
    public void PlayCameraShake(float duration, float magnitude)
    {
        if (cameraTransform == null) return;

        if (_cameraShakeCTS != null)
        {
            CleanUpCTS(ref _cameraShakeCTS);
            cameraTransform.localPosition = originalCameraPos;
        }

        _cameraShakeCTS = new CancellationTokenSource();
        PlayCameraShakeAsync(duration, magnitude, _cameraShakeCTS.Token).Forget();
    }

    private async UniTaskVoid PlayCameraShakeAsync(float duration, float magnitude, CancellationToken token)
    {
        float elapsed = 0.0f;

        try
        {
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.unscaledDeltaTime;

                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;

                cameraTransform.localPosition = originalCameraPos + new Vector3(x, y, 0f);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (cameraTransform != null)
            {
                cameraTransform.localPosition = originalCameraPos;
            }
        }
    }

    private void CleanUpCTS(ref CancellationTokenSource cts)
    {
        if (cts != null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException) { }
            finally
            {
                cts.Dispose();
                cts = null;
            }
        }
    }
}