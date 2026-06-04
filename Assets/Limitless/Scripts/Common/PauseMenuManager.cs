using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    // 💡【追加】外部のFxManager等から「今ポーズ中か」をノーコストで参照できるように静的プロパティ化
    public static bool IsPaused { get; private set; } = false;

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
    [SerializeField] private Outline[] buttonOutlines;
    [SerializeField] private Image[] buttonTargetImages;

    [Header("ーー ✨演出用：アウトラインの太さ設定 ーー")]
    [SerializeField] private Vector2 normalOutlineEffectDistance = new Vector2(1f, -1f);
    [SerializeField] private Vector2 highlightedOutlineEffectDistance = new Vector2(3f, -3f);

    [Header("ーー ✨演出用：カラー設定 ーー")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = Color.yellow;

    [Header("ーー 🎮 Input System アクション参照 ーー")]
    [SerializeField] private InputActionReference pauseActionReference;
    [SerializeField] private InputActionReference cancelActionReference;

    [Header("ーー 🎮 プレイヤー入力の制御用 ーー")]
    [SerializeField] private PlayerInput playerInput;

    private bool _isHowToPlayOpen = false;

    void Awake()
    {
        // 💡【重要】static変数はシーンを跨いでもメモリに残るため、
        // シーン起動時（リスタート時など）に必ず確実に false へ初期化（リセット）します
        IsPaused = false;
    }

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
        if (IsPaused) ResumeGame();
        else OpenPauseMenu();
    }

    private void OnCancelTriggered(InputAction.CallbackContext context)
    {
        if (IsPaused && _isHowToPlayOpen) BackToPauseMenuFromControls();
    }

    private void OpenPauseMenu()
    {
        IsPaused = true;
        _isHowToPlayOpen = false;
        Time.timeScale = 0f;

        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player")?.Disable();

            // 💡【追加】ポーズ時にプレイヤーのアニメーションを完全にフリーズさせる
            // (Unscaled Time モードであっても、コンポーネント自体がOFFになれば強制停止します)
            Animator playerAnim = playerInput.GetComponentInChildren<Animator>();
            if (playerAnim != null)
            {
                playerAnim.enabled = false;
            }
        }

        pauseCanvasRoot.SetActive(true);
        pauseMenuRoot.SetActive(true);
        controlsRoot.SetActive(false);

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
        IsPaused = false;
        _isHowToPlayOpen = false;

        if (FxManager.Instance == null || !FxManager.Instance.IsPlayingHitStop)
        {
            Time.timeScale = 1f;
        }
        else
        {
            Debug.Log("⏳ 裏で演出タスクが待機中のため、TimeScaleの通常復帰を保留しました。");
        }

        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player")?.Enable();

            // 💡【追加】ポーズ解除時にアニメーションを再開させる
            Animator playerAnim = playerInput.GetComponentInChildren<Animator>();
            if (playerAnim != null)
            {
                playerAnim.enabled = true;
            }
        }

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

    public void OnButtonSelect(int index) => SetButtonVisualState(index, true);
    public void OnButtonDeselect(int index) => SetButtonVisualState(index, false);

    private void SetButtonVisualState(int index, bool isHighlighted)
    {
        if (buttonOutlines != null && index < buttonOutlines.Length && buttonOutlines[index] != null)
        {
            buttonOutlines[index].effectDistance = isHighlighted ? highlightedOutlineEffectDistance : normalOutlineEffectDistance;
        }
        if (buttonTargetImages != null && index < buttonTargetImages.Length && buttonTargetImages[index] != null)
        {
            buttonTargetImages[index].color = isHighlighted ? highlightedColor : normalColor;
        }
    }

    private void ResetAllHighlights()
    {
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