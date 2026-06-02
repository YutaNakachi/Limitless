using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    [Header("ーー シーン名設定 ーー")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("ーー UIルートオブジェクトの参照 ーー")]
    [SerializeField] private GameObject pauseCanvasRoot;
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject controlsRoot;

    [Header("ーー ゲームパッド用の初期選択ボタン ーー")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button controlsFirstButton;

    [Header("ーー ✨演出用：各ボタンのコンポーネント ーー")]
    // 0:Resume, 1:Restart, 2:Controls, 3:Title, 4:Quit の順番でアタッチ
    [SerializeField] private Outline[] buttonOutlines; // 各ボタンのOutlineコンポーネント
    [SerializeField] private Image[] buttonTargetImages; // 色を変えたいImage（ボタン背景など）

    [Header("ーー ✨演出用：アウトラインの太さ設定 ーー")]
    [SerializeField] private Vector2 normalOutlineEffectDistance = new Vector2(1f, -1f);
    [SerializeField] private Vector2 highlightedOutlineEffectDistance = new Vector2(3f, -3f);

    [Header("ーー ✨演出用：カラー設定 ーー")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = Color.yellow;

    [Header("ーー 🎮 Input System アクション参照 ーー")]
    [SerializeField] private InputActionReference pauseActionReference;
    [SerializeField] private InputActionReference cancelActionReference;

    private bool _isPaused = false;
    private bool _isHowToPlayOpen = false;

    void Start()
    {
        ClosePauseMenu();
        ResetAllHighlights();
    }

    private void OnEnable()
    {
        if (pauseActionReference != null)
        {
            pauseActionReference.action.started += OnPauseTriggered;
            pauseActionReference.action.Enable();
        }
        if (cancelActionReference != null)
        {
            cancelActionReference.action.started += OnCancelTriggered;
            cancelActionReference.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (pauseActionReference != null) pauseActionReference.action.started -= OnPauseTriggered;
        if (cancelActionReference != null) cancelActionReference.action.started -= OnCancelTriggered;
    }

    private void OnPauseTriggered(InputAction.CallbackContext context)
    {
        if (_isPaused) ResumeGame();
        else OpenPauseMenu();
    }

    private void OnCancelTriggered(InputAction.CallbackContext context)
    {
        if (_isPaused && _isHowToPlayOpen) BackToPauseMenuFromControls();
    }

    private void OpenPauseMenu()
    {
        _isPaused = true;
        _isHowToPlayOpen = false;
        Time.timeScale = 0f;

        pauseCanvasRoot.SetActive(true);
        pauseMenuRoot.SetActive(true);
        controlsRoot.SetActive(false);

        // 画面が開く瞬間に一度すべて通常状態へリセット
        ResetAllHighlights();
        StartCoroutine(SelectFirstButtonDelay());
    }

    private IEnumerator SelectFirstButtonDelay()
    {
        yield return null;
        SetSelectedButton(resumeButton);
    }

    private void ClosePauseMenu()
    {
        _isPaused = false;
        _isHowToPlayOpen = false;
        Time.timeScale = 1f;

        pauseCanvasRoot.SetActive(false);
        pauseMenuRoot.SetActive(false);
        controlsRoot.SetActive(false);
    }

    private void SetSelectedButton(Button targetButton)
    {
        if (targetButton == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        targetButton.Select();
    }

    // ==========================================
    // ✨【修正版】引数を1つにしてUnityに認識させるメソッド
    // ==========================================

    /// <summary>
    /// ボタンが選択された（フォーカス・ホバーされた）とき【EventTrigger用】
    /// </summary>
    /// <param name="index">0:Resume, 1:Restart, 2:Controls, 3:Title, 4:Quit</param>
    public void OnButtonSelect(int index)
    {
        SetButtonVisualState(index, true);
    }

    /// <summary>
    /// ボタンから選択が外れた（離れた）とき【EventTrigger用】
    /// </summary>
    /// <param name="index">0:Resume, 1:Restart, 2:Controls, 3:Title, 4:Quit</param>
    public void OnButtonDeselect(int index)
    {
        SetButtonVisualState(index, false);
    }

    /// <summary>
    /// 実際の見た目を切り替える内部共通ロジック
    /// </summary>
    private void SetButtonVisualState(int index, bool isHighlighted)
    {
        // 1. アウトラインの太さを変更
        if (buttonOutlines != null && index < buttonOutlines.Length && buttonOutlines[index] != null)
        {
            buttonOutlines[index].effectDistance = isHighlighted ? highlightedOutlineEffectDistance : normalOutlineEffectDistance;
        }

        // 2. ボタンのカラーを変更
        if (buttonTargetImages != null && index < buttonTargetImages.Length && buttonTargetImages[index] != null)
        {
            buttonTargetImages[index].color = isHighlighted ? highlightedColor : normalColor;
        }
    }

    private void ResetAllHighlights()
    {
        // すべて通常時の太さと色に戻す
        if (buttonOutlines != null)
        {
            for (int i = 0; i < buttonOutlines.Length; i++)
            {
                if (buttonOutlines[i] != null) buttonOutlines[i].effectDistance = normalOutlineEffectDistance;
            }
        }

        if (buttonTargetImages != null)
        {
            for (int i = 0; i < buttonTargetImages.Length; i++)
            {
                if (buttonTargetImages[i] != null) buttonTargetImages[i].color = normalColor;
            }
        }
    }

    // ==========================================
    // 🔘 ボタンクリックイベント
    // ==========================================

    public void ResumeGame() => ClosePauseMenu();

    public void RestartGame()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmPauseMenu");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OpenControls()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmPauseMenu");
        _isHowToPlayOpen = true;
        pauseMenuRoot.SetActive(false);
        controlsRoot.SetActive(true);
        if (controlsFirstButton != null) SetSelectedButton(controlsFirstButton);
    }

    public void BackToPauseMenuFromControls()
    {
        _isHowToPlayOpen = false;
        controlsRoot.SetActive(false);
        pauseMenuRoot.SetActive(true);
        SetSelectedButton(controlsButton);
    }

    public void ReturnToTitle()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmPauseMenu");
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    public void Quit()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmPauseMenu");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}