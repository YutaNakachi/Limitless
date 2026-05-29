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

    // ⏱️ 連続暴発を防ぐための絶対時間記録用
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

        // 1. まず指定されたプリセット設定をアセットから取得
        FxPresetData.FxSettings settings = fxPresetData.GetPreset(presetLabel);

        // 構造体がデフォルト状態（未設定の空っぽ）なら処理を抜ける
        if (settings.Equals(default(FxPresetData.FxSettings))) return;

        // 🔥【超重要：useCoolTime対応型・グローバル防壁】
        // 要求された演出設定の「useCoolTimeがtrue」であり、かつ前回の全体演出から指定時間（fxCoolTime）が経過していない場合のみ、
        // 画面全体のカメラシェイクや時間停止を「間引き（遮断）」します。
        if (settings.useCoolTime && (Time.unscaledTime - _lastPlayedTime < fxCoolTime))
        {
            // 💡 画面全体が止まるのは防ぎつつ、殴られた敵個別の「微振動（手応え）」だけは実行！
            TriggerObjectShakeOnly(settings, targetObject);
            return; // 🛑 ここで処理を終了し、下の画面全体演出（HitStop等）へは行かせない
        }

        // 📝【記録のタイミング】間引かれずに「実際に全体演出が実行される時」だけ、実行時刻を更新する
        // これにより、useCoolTime = false の重要演出が走っても、雑魚の間引き用タイマーが不当に延長されるのを防ぎます
        if (settings.useCoolTime)
        {
            _lastPlayedTime = Time.unscaledTime;
        }

        // 2. ヒットストップの実行（useCoolTime = false、またはクールタイム明けた演出はここを通る）
        if (settings.stopDuration > 0)
        {
            PlayHitStop(settings.stopDuration, settings.timeScale);
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
    public void PlayHitStop(float duration, float timeScale = 0f)
    {
        // 新しく要求された停止時間が、現在の残り時間より長い場合のみ採用（スマートな排他制御）
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
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                float dt = Time.unscaledDeltaTime;
                if (dt <= 0) dt = 0.016f; // 万が一のノイズ対策

                hitStopRemainingTime -= dt;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            // 正常に終了した（他の時間停止に割り込まれていない）時だけ、時間軸を1.0に戻す
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