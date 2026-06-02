using System.Collections;
using TMPro;
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

    [Header("ーー ✨各モードのテキスト参照 ーー")]
    [SerializeField] private TextMeshProUGUI survivalButtonText;        // サバイバルボタンの文字
    [SerializeField] private TextMeshProUGUI trainingButtonText;        // トレーニングボタンの文字

    [Header("ーー ✨テキストの拡大率設定 ーー")]
    [SerializeField] private float normalFontSize = 24f;     // 通常時の文字サイズ
    [SerializeField] private float highlightedFontSize = 30f; // 選択（ホバー）時の文字サイズ

    [Header("ーー 演出タイマー設定 ーー")]
    [SerializeField] private float movieLengthSeconds = 5.0f;
    [SerializeField] private float fadeDuration = 1.2f;

    private VideoPlayer _videoPlayer;
    private bool _isWaitingForAnyKey = false;
    private System.IDisposable _anyRawInputListener;

    // ✨【追加】動画スキップ状態管理用の変数
    private Coroutine _sequencerCoroutine;
    private bool _isVideoPlaying = false;
    private System.IDisposable _videoSkipListener;

    void Start()
    {
        _videoPlayer = GetComponentInChildren<VideoPlayer>();

        if (videoPlayerObject != null) videoPlayerObject.SetActive(true);
        if (pressAnyRoot != null) pressAnyRoot.SetActive(false);
        if (modeSelectRoot != null) modeSelectRoot.SetActive(false);

        if (survivalHoverImage != null) survivalHoverImage.SetActive(false);
        if (trainingHoverImage != null) trainingHoverImage.SetActive(false);

        ResetTextSizes();

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

        // ✨【変更】コルーチンをキャンセルできるように変数に格納して開始
        _sequencerCoroutine = StartCoroutine(FixedTimerSequence());

        // ✨【追加】動画再生中のボタン入力を監視開始
        _isVideoPlaying = true;
        _videoSkipListener = InputSystem.onAnyButtonPress.Call(OnVideoSkipTriggered);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            QuitGame();
        }
    }

    private void QuitGame()
    {
        Debug.Log("Game Quitting...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ✨【追加】動画再生中に何かボタンが押されたら呼ばれるメソッド
    private void OnVideoSkipTriggered(InputControl control)
    {
        if (!_isVideoPlaying) return;

        // ESCキーだったらスキップではなくゲーム終了を優先するため弾く
        if (Keyboard.current != null && control == Keyboard.current.escapeKey)
        {
            return;
        }

        _isVideoPlaying = false;

        // リスナーを即座に解除（多重連打対策）
        if (_videoSkipListener != null) _videoSkipListener.Dispose();

        // 通常の流れ（タイマー）を強制ストップしてスキップ処理へ
        if (_sequencerCoroutine != null) StopCoroutine(_sequencerCoroutine);
        StartCoroutine(SkipVideoSequence());
    }

    // ✨【追加】動画を飛ばして一気にタイトルへ繋ぐスキップ演出コルーチン
    private IEnumerator SkipVideoSequence()
    {
        // 1. 綺麗につなぐため、ホワイトアウトフェードをパッと挟む（0.3秒ほどで素早くフェード）
        float prevFadeDuration = fadeDuration;
        fadeDuration = 0.3f;
        yield return StartCoroutine(FadeVisual(fadeOverlay != null ? fadeOverlay.color.a : 0f, 1f));

        // 2. 裏で動画を止めて非表示にする
        if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
        if (videoPlayerObject != null) videoPlayerObject.SetActive(false);

        // 3. タイトル画面を表示させてフェードイン
        fadeDuration = prevFadeDuration; // フェード時間を元に戻す
        yield return StartCoroutine(RevealTitleScreen());
    }

    private IEnumerator FixedTimerSequence()
    {
        yield return new WaitForSeconds(movieLengthSeconds);

        // 最後まで見終えたら動画用の入力監視はもう不要なので解除
        CleanUpVideoListener();

        yield return StartCoroutine(FadeVisual(0f, 1f));

        if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
        if (videoPlayerObject != null) videoPlayerObject.SetActive(false);

        yield return StartCoroutine(RevealTitleScreen());
    }

    private IEnumerator RevealTitleScreen()
    {
        if (pressAnyRoot != null) pressAnyRoot.SetActive(true);

        yield return StartCoroutine(FadeVisual(1f, 0f));

        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("Title");
        }

        _isWaitingForAnyKey = true;
        _anyRawInputListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressTriggered);
    }

    private void OnAnyButtonPressTriggered(InputControl control)
    {
        if (!_isWaitingForAnyKey) return;

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

        StartCoroutine(SelectFirstButtonDelay());
    }

    private IEnumerator SelectFirstButtonDelay()
    {
        yield return null;
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
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmTitle");
        SceneManager.LoadScene(survivalSceneName);
    }

    public void SelectTrainingMode()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmTitle");
        SceneManager.LoadScene(trainingSceneName);
    }

    public void OnSurvivalButtonHighlighted(bool isHighlighted)
    {
        if (survivalHoverImage != null) survivalHoverImage.SetActive(isHighlighted);
        if (survivalButtonText != null)
        {
            survivalButtonText.fontSize = isHighlighted ? highlightedFontSize : normalFontSize;
        }
        if (isHighlighted && SoundManager.Instance != null) SoundManager.Instance.PlaySE("ChangeSelected");
    }

    public void OnTrainingButtonHighlighted(bool isHighlighted)
    {
        if (trainingHoverImage != null) trainingHoverImage.SetActive(isHighlighted);
        if (trainingButtonText != null)
        {
            trainingButtonText.fontSize = isHighlighted ? highlightedFontSize : normalFontSize;
        }
        if (isHighlighted && SoundManager.Instance != null) SoundManager.Instance.PlaySE("ChangeSelected");
    }

    private void ResetTextSizes()
    {
        if (survivalButtonText != null) survivalButtonText.fontSize = normalFontSize;
        if (trainingButtonText != null) trainingButtonText.fontSize = normalFontSize;
    }

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

    private void CleanUpVideoListener()
    {
        _isVideoPlaying = false;
        if (_videoSkipListener != null)
        {
            _videoSkipListener.Dispose();
            _videoSkipListener = null;
        }
    }

    private void OnDestroy()
    {
        CleanUpVideoListener();

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