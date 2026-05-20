using System.Collections;
using UnityEngine;

public class FxManager : MonoBehaviour
{
    // どこからでも FxManager.Instance でアクセス可能にする（シングルトン）
    public static FxManager Instance { get; private set; }

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform; // メインカメラのTransformをアサイン
    private Vector3 originalCameraPos;

    private bool isShaking = false;
    private bool isHitStopping = false;

    void Awake()
    {
        // シングルトンの安全な初期化（インターロック）
        if (Instance == null)
        {
            Instance = this;
            // シーンを跨いでも破棄したくない場合はコメント解除
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    // ================================================================
    // 🔥 1. ヒットストップ（Time.timeScale を操作）
    // ================================================================
    /// <summary>
    /// ゲームの時間を一瞬だけ停止、またはスローにする
    /// </summary>
    /// <param name="duration">停止する時間（秒）</param>
    /// <param name="timeScale">時間の進み方（0fで完全停止、0.1fで超スロー）</param>
    public void PlayHitStop(float duration, float timeScale = 0f)
    {
        // すでにヒットストップ中なら重ねて実行しない（バグ防止）
        if (isHitStopping) return;

        StartCoroutine(HitStopCoroutine(duration, timeScale));
    }

    private IEnumerator HitStopCoroutine(float duration, float timeScale)
    {
        isHitStopping = true;
        Time.timeScale = timeScale;

        // Time.timeScaleが0のとき、通常の WaitForSeconds は進まなくなるため、
        // 現実世界の絶対時間を計測する「WaitForSecondsRealtime」を使うのが超重要！
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1.0f; // 時間を元に戻す
        isHitStopping = false;
    }

    // ================================================================
    // 🔥 2. 画面シェイク（カメラのローカル座標をランダムに揺らす）
    // ================================================================
    /// <summary>
    /// 画面（カメラ）を激しく揺らす
    /// </summary>
    /// <param name="duration">揺れる時間（秒）</param>
    /// <param name="magnitude">揺れの強さ（振幅）</param>
    public void PlayCameraShake(float duration, float magnitude)
    {
        // 前回のシェイクが残っていれば位置をリセットして上書き
        if (isShaking)
        {
            StopAllCoroutines(); // 走っているシェイクコルーチンを止める
            cameraTransform.localPosition = originalCameraPos;
        }

        StartCoroutine(CameraShakeCoroutine(duration, magnitude));
    }

    private IEnumerator CameraShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        originalCameraPos = cameraTransform.localPosition; // 元のカメラ位置を記憶

        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // ヒットストップ中（Time.timeScale=0）でもカメラを滑らかに揺らすため、
            // Time.deltaTime ではなく Time.unscaledDeltaTime を使うのがプロの技！
            elapsed += Time.unscaledDeltaTime;

            // ランダムなノイズ（-1f 〜 1f）に強さを掛け算して、新しい座標を作る
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            cameraTransform.localPosition = new Vector3(originalCameraPos.x + x, originalCameraPos.y + y, originalCameraPos.z);

            yield return null; // 1フレーム待機
        }

        // 揺れが終わったら、必ず元の正しい位置に寸分の狂いなく戻す（重要）
        cameraTransform.localPosition = originalCameraPos;
        isShaking = false;
    }
}