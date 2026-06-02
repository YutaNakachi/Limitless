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

    [Header("ーー ✨各モードの背景演出用画像 ーー")]
    [SerializeField] private GameObject survivalHoverImage;  // サバイバル選択時に表示する画像
    [SerializeField] private GameObject trainingHoverImage;  // トレーニング選択時に表示する画像

    [Header("ーー 演出タイマー設定 ーー")]
    [SerializeField] private float movieLengthSeconds = 5.0f;
    [SerializeField] private float fadeDuration = 1.2f;

    private VideoPlayer _videoPlayer;
    private bool _isWaitingForAnyKey = false;
    private System.IDisposable _anyRawInputListener;

    void Start()
    {
        _videoPlayer = GetComponentInChildren<VideoPlayer>();

        if (videoPlayerObject != null) videoPlayerObject.SetActive(true);
        if (pressAnyRoot != null) pressAnyRoot.SetActive(false);
        if (modeSelectRoot != null) modeSelectRoot.SetActive(false);

        // 背景演出画像も最初はすべて隠しておく
        if (survivalHoverImage != null) survivalHoverImage.SetActive(false);
        if (trainingHoverImage != null) trainingHoverImage.SetActive(false);

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

        StartCoroutine(FixedTimerSequence());
    }

    // ✨ ESCキーの入力を毎フレーム安全に監視
    void Update()
    {
        // キーボードが接続されており、かつESCキーが押された瞬間であるかを確認
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            QuitGame();
        }
    }

    /// <summary>
    /// ゲームを安全に終了するメソッド
    /// </summary>
    private void QuitGame()
    {
        Debug.Log("Game Quitting...");

#if UNITY_EDITOR
        // Unityエディタ上では再生を停止する
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 実機ビルドではアプリケーションを終了する
        Application.Quit();
#endif
    }

    private IEnumerator FixedTimerSequence()
    {
        yield return new WaitForSeconds(movieLengthSeconds);

        yield return StartCoroutine(FadeVisual(0f, 1f));

        if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
        if (videoPlayerObject != null) videoPlayerObject.SetActive(false);

        yield return StartCoroutine(RevealTitleScreen());
    }

    private IEnumerator RevealTitleScreen()
    {
        if (pressAnyRoot != null) pressAnyRoot.SetActive(true);

        yield return StartCoroutine(FadeVisual(1f, 0f));

        // 💡 ボタンを触れるようにFadeOverlayを非表示化
        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);

        _isWaitingForAnyKey = true;
        _anyRawInputListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressTriggered);
    }

    private void OnAnyButtonPressTriggered(InputControl control)
    {
        if (!_isWaitingForAnyKey) return;

        // 💡【修正】もし押されたのが「ESCキー」だった場合は、ゲーム終了処理（Update側）に任せるため、
        // Press Any画面を通過させる処理（以降のロジック）を無視して弾く
        if (Keyboard.current != null && control == Keyboard.current.escapeKey)
        {
            return;
        }

        _isWaitingForAnyKey = false;

        if (_anyRawInputListener != null)
        {
            _anyRawInputListener.Dispose();
        }

        if (pressAnyRoot != null) pressAnyRoot.SetActive(false);
        if (modeSelectRoot != null) modeSelectRoot.SetActive(true);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("Title");
        }

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
    // ✨ ボタンの選択状態に連動するメソッド
    // ==========================================

    /// <summary>
    /// サバイバルボタンが選ばれた（フォーカス・ホバーされた）とき
    /// </summary>
    public void OnSurvivalButtonHighlighted(bool isHighlighted)
    {
        if (survivalHoverImage != null)
        {
            survivalHoverImage.SetActive(isHighlighted);
        }
    }

    /// <summary>
    /// トレーニングボタンが選ばれた（フォーカス・ホバーされた）とき
    /// </summary>
    public void OnTrainingButtonHighlighted(bool isHighlighted)
    {
        if (trainingHoverImage != null)
        {
            trainingHoverImage.SetActive(isHighlighted);
        }
    }

    // ==========================================
    // 🌫️ 汎用フェードコルーチン
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

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
    }
}