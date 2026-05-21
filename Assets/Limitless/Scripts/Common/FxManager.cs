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

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    // ================================================================
    // 🔥 拡張：対象のTransformを引数に追加（デフォルト値は null）
    // ================================================================
    /// <summary>
    /// プリセット名を指定して演出を再生。targetObjectを指定すると、そのキャラが微振動します。
    /// </summary>
    public void Play(string presetLabel, Transform targetObject = null)
    {
        if (fxPresetData == null) return;

        FxPresetData.FxSettings settings = fxPresetData.GetPreset(presetLabel);

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
            StartCoroutine(ObjectShakeCoroutine(targetObject, settings.stopDuration, settings.objectShakeMagnitude, settings.useObjectShakeY));
        }
    }

    // --- 対象を微振動させるコルーチン ---
    private IEnumerator ObjectShakeCoroutine(Transform target, float duration, float magnitude, bool useY)
    {
        Vector3 originalPos = target.localPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            // X方向は常に揺らす
            float offsetX = Random.Range(-1f, 1f) * magnitude;

            // useY が true の時だけランダム値を計算し、false の時は 0f（振動なし）にする
            float offsetY = useY ? Random.Range(-1f, 1f) * magnitude : 0f;

            target.localPosition = new Vector3(originalPos.x + offsetX, originalPos.y + offsetY, originalPos.z);

            yield return null;
        }

        if (target != null)
        {
            target.localPosition = originalPos;
        }
    }

    // --- 既存の「PlayHitStop」「PlayCameraShake」コルーチン（省略せずそのまま残す） ---
    public void PlayHitStop(float duration, float timeScale = 0f)
    {
        if (isHitStopping) return;
        StartCoroutine(HitStopCoroutine(duration, timeScale));
    }

    private IEnumerator HitStopCoroutine(float duration, float timeScale)
    {
        isHitStopping = true;
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
        isHitStopping = false;
    }

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