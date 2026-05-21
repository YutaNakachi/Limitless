using System.Collections;
using UnityEngine;

public class FxManager : MonoBehaviour
{
    public static FxManager Instance { get; private set; }

    [Header("データ一元管理アセット")]
    [SerializeField] private FxPresetData fxPresetData;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;
    private Vector3 originalCameraPos;

    private bool isShaking = false;
    private bool isHitStopping = false;

    // 複数同時発生時の制御用変数
    private float hitStopRemainingTime = 0f; // 残りのヒットストップ時間
    private Coroutine currentHitStopCoroutine; // 現在走っているコルーチンの参照

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    /// <summary>
    /// プリセット名を指定して演出を再生。targetObjectを指定すると、そのキャラが微振動します。
    /// </summary>
    public void Play(string presetLabel, Transform targetObject = null)
    {
        if (fxPresetData == null) return;

        FxPresetData.FxSettings settings = fxPresetData.GetPreset(presetLabel);

        // 1. ヒットストップの実行（排他制御つきメソッド）
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
            StartCoroutine(ObjectShakeCoroutine(targetObject, settings.stopDuration, settings.objectShakeMagnitude, settings.useObjectShakeY));
        }
    }

    // ================================================================
    // 🛡️ 改良版：ヒットストップ（安全タイムアウト搭載）
    // ================================================================
    public void PlayHitStop(float duration, float timeScale = 0f)
    {
        // 新しく要求された時間が、現在残っている時間よりも「長い」場合だけ採用する
        if (duration > hitStopRemainingTime)
        {
            hitStopRemainingTime = duration; // 残り時間を最新の長い方に更新

            // すでにコルーチンが走っているなら、二重起動を防ぐために一旦止める
            if (isHitStopping && currentHitStopCoroutine != null)
            {
                StopCoroutine(currentHitStopCoroutine);
            }

            // 最新のパラメータでコルーチンを新しくスタートし、参照を保持
            currentHitStopCoroutine = StartCoroutine(HitStopCoroutine(timeScale));
        }
    }

    private IEnumerator HitStopCoroutine(float timeScale)
    {
        isHitStopping = true;
        Time.timeScale = timeScale;

        // ⚡ 保険：万が一の無限ループを防止する実時間タイマー（最大5秒）
        float safetyTimeout = 5.0f;

        // 残り時間が 0 になるまで、現実世界の絶対時間（Unscaled）で正確にカウントダウン
        while (hitStopRemainingTime > 0)
        {
            float dt = Time.unscaledDeltaTime;

            // 例外的なフリーズ対策（unscaledDeltaTimeが0以下になった場合の安全弁）
            if (dt <= 0) dt = 0.016f;

            hitStopRemainingTime -= dt;
            safetyTimeout -= dt;

            // ⚠️ 5秒以上ゲームが停止状態のままなら、バグと判断して強制脱出
            if (safetyTimeout <= 0)
            {
                Debug.LogWarning("⚠️ [FxManager] ヒットストップが安全タイムアウト(5秒)により強制解除されました。");
                break;
            }

            yield return null; // 1フレーム待機
        }

        // 完全に時間を使い切った、または強制脱出したら元の正しい時間軸に100%戻す
        hitStopRemainingTime = 0f;
        Time.timeScale = 1.0f;
        isHitStopping = false;
        currentHitStopCoroutine = null;
    }

    // ================================================================
    // 🛡️ 改良版：オブジェクト微振動（毎フレームのNullチェック搭載）
    // ================================================================
    private IEnumerator ObjectShakeCoroutine(Transform target, float duration, float magnitude, bool useY)
    {
        // ⚡ 起動時の生存チェック
        if (target == null) yield break;

        Vector3 originalPos = target.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // ⚡ 毎フレームの生存チェック
            // 空中キックの瞬間にボールがDestroyされたり非アクティブになったら、安全にコルーチンを抜ける
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                yield break;
            }

            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = useY ? Random.Range(-1f, 1f) * magnitude : 0f;

            // 安全が保証されているので位置を代入
            target.localPosition = new Vector3(originalPos.x + offsetX, originalPos.y + offsetY, originalPos.z);

            yield return null;
        }

        // ⚡ 終了時の生存チェック
        if (target != null)
        {
            target.localPosition = originalPos;
        }
    }

    // ================================================================
    // 3. カメラシェイクコルーチン
    // ================================================================
    public void PlayCameraShake(float duration, float magnitude)
    {
        if (isShaking) { StopAllCoroutines(); cameraTransform.localPosition = originalCameraPos; }
        StartCoroutine(CameraShakeCoroutine(duration, magnitude));
    }

    private IEnumerator CameraShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        originalCameraPos = cameraTransform.localPosition;
        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            cameraTransform.localPosition = new Vector3(originalCameraPos.x + x, originalCameraPos.y + y, originalCameraPos.z);
            yield return null;
        }
        cameraTransform.localPosition = originalCameraPos;
        isShaking = false;
    }
}