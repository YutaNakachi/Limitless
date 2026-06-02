using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class TitleSceneManager : MonoBehaviour
{
    [Header("ーー シーン名設定 ーー")]
    [SerializeField] private string survivalSceneName = "SurvivalModeScene";
    [SerializeField] private string trainingSceneName = "TrainingModeScene";

    [Header("ーー UIルートオブジェクトの参照 ーー")]
    [SerializeField] private GameObject videoPlayerObject; // 動画再生オブジェクト
    [SerializeField] private Image fadeOverlay;            // ホワイトアウト用の純白Image
    [SerializeField] private GameObject pressAnyRoot;      // タイトル＆Press Anyの親
    [SerializeField] private GameObject modeSelectRoot;    // モード選択ボタンの親

    [Header("ーー ゲームパッド用の初期選択ボタン ーー")]
    [SerializeField] private Button survivalButton;        // モード選択時に最初にフォーカスするボタン

    [Header("ーー 演出タイマー設定（インスペクターで調整） ーー")]
    [Tooltip("動画の実際の長さを秒単位で入力してください（例: 5秒の動画なら 5.0）")]
    [SerializeField] private float movieLengthSeconds = 5.0f;

    [Tooltip("ホワイトアウト（画面が白く染まる・明ける）にかける時間")]
    [SerializeField] private float fadeDuration = 1.2f;

    private VideoPlayer _videoPlayer;
    private bool _isWaitingForAnyKey = false;
    private System.IDisposable _anyRawInputListener;

    void Start()
    {
        _videoPlayer = GetComponentInChildren<VideoPlayer>();

        // 各オブジェクトの初期状態を確実に設定
        if (videoPlayerObject != null) videoPlayerObject.SetActive(true);
        if (pressAnyRoot != null) pressAnyRoot.SetActive(false);
        if (modeSelectRoot != null) modeSelectRoot.SetActive(false);

        // 最初は絶対に「完全に透明（アルファ値 0）」
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            Color c = fadeOverlay.color;
            c.a = 0f;
            fadeOverlay.color = c;
        }

        if (_videoPlayer != null)
        {
            _videoPlayer.Play();
        }

        // 💡 動画の状態に関係なく、指定秒数後にホワイトアウトを開始するコルーチンを走らせる
        StartCoroutine(FixedTimerSequence());
    }

    /// <summary>
    /// 指定された秒数を正確に待ってからホワイトアウトを行うコルーチン
    /// </summary>
    private IEnumerator FixedTimerSequence()
    {
        // 1. インスペクターで設定した「動画の長さ」のぶんだけ、透明のままじっと待つ
        yield return new WaitForSeconds(movieLengthSeconds);

        // 2. 時間が来たら、画面を透明(0f) から 「真っ白(1f)」 へジワ〜っと染める（ホワイトアウト開始）
        yield return StartCoroutine(FadeVisual(0f, 1f));

        // 完全に画面が白に染まったので、裏で動画オブジェクトを非表示にする
        if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
        if (videoPlayerObject != null) videoPlayerObject.SetActive(false);

        // 3. タイトル画面（Press Any）を表示して、ホワイトアウトを明ける
        yield return StartCoroutine(RevealTitleScreen());
    }

    /// <summary>
    /// ホワイトアウトを明けさせてタイトル画面を表示し、任意入力を待つ
    /// </summary>
    private IEnumerator RevealTitleScreen()
    {
        // 白の壁の裏側でタイトルロゴとPressAnyを出現させる
        if (pressAnyRoot != null) pressAnyRoot.SetActive(true);

        // 今度は 「真っ白（1f）」 から 「透明（0f）」 へ戻して背景とロゴを見せる
        yield return StartCoroutine(FadeVisual(1f, 0f));

        // 任意のボタン入力を受け付け開始
        _isWaitingForAnyKey = true;
        _anyRawInputListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressTriggered);
    }

    private void OnAnyButtonPressTriggered(InputControl control)
    {
        if (!_isWaitingForAnyKey) return;
        _isWaitingForAnyKey = false;

        if (_anyRawInputListener != null)
        {
            _anyRawInputListener.Dispose();
        }

        // Press Anyを消してモード選択へ
        if (pressAnyRoot != null) pressAnyRoot.SetActive(false);
        if (modeSelectRoot != null) modeSelectRoot.SetActive(true);

        SetSelectedButton(survivalButton);
    }

    private void SetSelectedButton(Button targetButton)
    {
        if (targetButton == null || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        targetButton.Select();
    }

    public void SelectSurvivalMode()
    {
        SceneManager.LoadScene(survivalSceneName);
    }

    public void SelectTrainingMode()
    {
        SceneManager.LoadScene(trainingSceneName);
    }

    // ==========================================
    // 🌫️ 汎用フェードコルーチン（数値をLerpするだけ）
    // ==========================================
    private IEnumerator FadeVisual(float startAlpha, float endAlpha)
    {
        if (fadeOverlay == null) yield break;

        float elapsed = 0f;
        Color c = fadeOverlay.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            fadeOverlay.color = c;
            yield return null;
        }

        c.a = endAlpha;
        fadeOverlay.color = c;
    }

    private void OnDestroy()
    {
        if (_anyRawInputListener != null)
        {
            _anyRawInputListener.Dispose();
        }
    }
}