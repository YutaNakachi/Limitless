using Cysharp.Threading.Tasks; // 💡 UniTaskを有効化
using System;
using System.Threading;
using UnityEngine;
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
    [Tooltip("連続実行を防ぐための共通クールタイム（秒）。0.05f〜0.1fあたりが最適です")]
    [SerializeField] private float fxCoolTime = 0.08f;

    // 複数同時発生時の制御用変数
    private float hitStopRemainingTime = 0f;          // 残りのヒットストップ時間
    private CancellationTokenSource _hitStopCTS;     // ヒットストップ用のキャンセル管理
    private CancellationTokenSource _cameraShakeCTS; // カメラシェイク用のキャンセル管理

    // ⏱️ 連続暴発を防ぐための時間記録用
    private string _lastPlayedPresetLabel = "";
    private float _lastPlayedTime = -999f;

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
    }

    /// <summary>
    /// プリセット名を指定して演出を再生。targetObjectを指定すると、そのキャラが微振動します。
    /// </summary>
    public void Play(string presetLabel, Transform targetObject = null)
    {
        if (fxPresetData == null) return;

        // 先にアセットから設定を取得
        FxPresetData.FxSettings settings = fxPresetData.GetPreset(presetLabel);

        // ⭕【確実な修正】構造体がデフォルト状態（未設定の空っぽ）なら処理を抜ける
        // これなら内部の変数名（Labelなど）が何であっても、100%エラーを回避して安全に弾けます！
        if (settings.Equals(default(FxPresetData.FxSettings))) return;

        // 🛠️【個別制限ロジック】
        // 1. その設定が「useCoolTime = true（制限する）」になっていて、
        // 2. かつ全く同じプリセットが極小時間内（fxCoolTime内）に何度も呼ばれた場合
        if (settings.useCoolTime && presetLabel == _lastPlayedPresetLabel && Time.unscaledTime - _lastPlayedTime < fxCoolTime)
        {
            // 💡 全体演出（画面揺れ・時間停止）は弾き、対象の敵だけの微振動（手応え）を実行
            TriggerObjectShakeOnly(settings, targetObject);
            return;
        }

        // 今回再生した演出情報と時間を記録（Time.timeScale = 0でも進む unscaledTime を使用）
        _lastPlayedPresetLabel = presetLabel;
        _lastPlayedTime = Time.unscaledTime;

        // 1. ヒットストップの実行
        if (settings.stopDuration > 0)
        {
            PlayHitStop(settings.stopDuration, settings.timeScale);
        }

        // 2. カメラシェイクの実行
        if (settings.shakeDuration > 0)
        {
            PlayCameraShake(settings.shakeDuration, settings.shakeMagnitude);
        }

        // 3. ヒット対象自体の微振動を実行
        if (targetObject != null && settings.objectShakeMagnitude > 0 && settings.stopDuration > 0)
        {
            // オブジェクトの微振動は、そのオブジェクト自身の消滅（OnDestroy）に連動させるため、対象の CancellationToken を渡す
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
    public void PlayHitStop(float duration, float timeScale = 0f)
    {
        // 新しく要求された時間が、現在残っている時間よりも「長い」場合だけ採用（排他制御）
        if (duration > hitStopRemainingTime)
        {
            hitStopRemainingTime = duration;

            // すでに走っているヒットストップタスクがあれば、安全に「キャンセル」して上書きする
            CleanUpCTS(ref _hitStopCTS);
            _hitStopCTS = new CancellationTokenSource();

            // 非同期メソッドを非同期のまま投げっぱなし（Forget）で起動
            PlayHitStopAsync(timeScale, _hitStopCTS.Token).Forget();
        }
    }

    private async UniTaskVoid PlayHitStopAsync(float timeScale, CancellationToken token)
    {
        Time.timeScale = timeScale;

        try
        {
            // 残り時間が 0 になるまでループ
            while (hitStopRemainingTime > 0)
            {
                // 実時間（Time.timeScaleの影響を受けないUpdate）タイミングで1フレーム待機
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                float dt = Time.unscaledDeltaTime;
                if (dt <= 0) dt = 0.016f; // 万が一のノイズ対策

                hitStopRemainingTime -= dt;
            }
        }
        catch (OperationCanceledException)
        {
            // 他のヒットストップに上書きキャンセルされた場合はここを通る
            // 新しい次のタスクに処理を譲るため、ここではtimeScaleは戻さない
            return;
        }
        finally
        {
            // 正常終了時はもちろん、エラーが起きようがシーンが切り替わろうが、「絶対に」ここを通る。
            // 割り込みキャンセル（IsCancellationRequested = true）でない時だけ、安全に時間を元に戻す。
            if (!token.IsCancellationRequested)
            {
                hitStopRemainingTime = 0f;
                Time.timeScale = 1.0f;
                Debug.Log("⏱️ [UniTask] ヒットストップが正常に終了、時間軸が復帰しました。");
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
                // 実時間で1フレーム待機。対象オブジェクトがDestroyされたら、objectTokenがそれを検知して自動でループを脱出する
                await UniTask.Yield(PlayerLoopTiming.Update, objectToken);

                // 念のためのNullダブルチェック
                if (target == null || !target.gameObject.activeInHierarchy) return;

                elapsed += Time.unscaledDeltaTime;

                float offsetX = Random.Range(-1f, 1f) * magnitude;
                float offsetY = useY ? Random.Range(-1f, 1f) * magnitude : 0f;

                target.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
            }
        }
        catch (OperationCanceledException)
        {
            // オブジェクトがDestroyされた場合は自動的にここに飛び、エラーを出さずに静かに消滅する
            return;
        }
        finally
        {
            // 終了時にオブジェクトがまだ生きていれば、位置を元に戻す
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

        // 連続でシェイクが呼ばれたら、前のシェイクをキャンセルしてカメラ位置を一瞬でリセット
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
            // キャンセルされた場合も含め、終了時は100%オリジナルのカメラ位置にピタッと戻す
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