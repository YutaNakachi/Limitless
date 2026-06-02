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
    [SerializeField] private GameObject pauseCanvasRoot;  // Canvas自体、または最親のGameObject
    [SerializeField] private GameObject pauseMenuRoot;    // ポーズメニューのメインボタン群（5つ）の親
    [SerializeField] private GameObject controlsRoot;     // 操作説明画面の親

    [Header("ーー ゲームパッド用の初期選択ボタン ーー")]
    [SerializeField] private Button resumeButton;         // ポーズを開いたときに最初にフォーカスするボタン
    [SerializeField] private Button controlsFirstButton;  // 操作説明を開いたときにフォーカスするボタン（任意）

    [Header("ーー 🎮 Input System アクション参照 (直接登録用) ーー")]
    [SerializeField] private InputActionReference pauseActionReference;
    [SerializeField] private InputActionReference cancelActionReference;

    private bool _isPaused = false;
    private bool _isHowToPlayOpen = false;

    void Start()
    {
        // ゲーム開始時はポーズ画面を確実に閉じる
        ClosePauseMenu();
    }

    // 💡 スクリプト有効時にInput Systemのイベントを登録
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

    // 💡 スクリプト無効・破棄時にイベントを安全に解除
    private void OnDisable()
    {
        if (pauseActionReference != null)
        {
            pauseActionReference.action.started -= OnPauseTriggered;
        }

        if (cancelActionReference != null)
        {
            cancelActionReference.action.started -= OnCancelTriggered;
        }
    }

    // ==========================================
    // 🎮 Input System コールバック受け取り
    // ==========================================

    private void OnPauseTriggered(InputAction.CallbackContext context)
    {
        if (_isPaused)
        {
            ResumeGame();
        }
        else
        {
            OpenPauseMenu();
        }
    }

    private void OnCancelTriggered(InputAction.CallbackContext context)
    {
        if (_isPaused && _isHowToPlayOpen)
        {
            BackToPauseMenuFromControls();
        }
    }

    // ==========================================
    // ⚙️ 画面の開閉・状態コントロール内部ロジック
    // ==========================================

    private void OpenPauseMenu()
    {
        _isPaused = true;
        _isHowToPlayOpen = false;

        Time.timeScale = 0f; // ★ゲーム内の時間を完全停止

        pauseCanvasRoot.SetActive(true);
        pauseMenuRoot.SetActive(true);
        controlsRoot.SetActive(false);

        // ゲームパッド操作のために「ゲームに戻る」ボタンをフォーカス
        SetSelectedButton(resumeButton);

        // ※SoundManagerの実装に合わせて再生してください
        // SoundManager.Instance.PlaySEAtPosition("PauseOpen", Vector3.zero);
    }

    private void ClosePauseMenu()
    {
        _isPaused = false;
        _isHowToPlayOpen = false;

        Time.timeScale = 1f; // ★ゲーム内の時間を再開

        pauseCanvasRoot.SetActive(false);
        pauseMenuRoot.SetActive(false);
        controlsRoot.SetActive(false);

        // ※SoundManagerの実装に合わせて再生してください
        // SoundManager.Instance.PlaySEAtPosition("PauseClose", Vector3.zero);
    }

    private void SetSelectedButton(Button targetButton)
    {
        if (targetButton == null || EventSystem.current == null) return;

        // 一度選択をクリアしてからフォーカスを当てる（EventSystemのバグ対策）
        EventSystem.current.SetSelectedGameObject(null);
        targetButton.Select();
    }

    // ==========================================
    // 🔘 各ボタンのクリックイベント（Submitと連動）
    // ==========================================

    /// <summary>
    /// ボタン1：ゲームに戻る（Resume）
    /// </summary>
    public void ResumeGame()
    {
        ClosePauseMenu();
    }

    /// <summary>
    /// ボタン2：リスタート（Restart）
    /// </summary>
    public void RestartGame()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("SelectConfirmed", Vector3.zero);

        Time.timeScale = 1f; // ★シーン読み込み前に時間を必ず1に戻す！

        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    /// <summary>
    /// ボタン3：操作説明を表示（HowToPlay）
    /// </summary>
    public void OpenControls()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("SelectConfirmed", Vector3.zero);
        _isHowToPlayOpen = true;

        pauseMenuRoot.SetActive(false);
        controlsRoot.SetActive(true);

        // 操作説明内に閉じるボタン等があればそれを選択、無ければフォーカスを外す
        if (controlsFirstButton != null)
        {
            SetSelectedButton(controlsFirstButton);
        }
        else
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>
    /// 操作説明画面からポーズメイン画面へ戻る（Cancelキー入力時など）
    /// </summary>
    public void BackToPauseMenuFromControls()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("Cancel", Vector3.zero);
        _isHowToPlayOpen = false;

        controlsRoot.SetActive(false);
        pauseMenuRoot.SetActive(true);

        // 操作説明を閉じた後は、「操作説明を開いたボタン（またはゲームに戻る）」にカーソルを戻す
        SetSelectedButton(resumeButton);
    }

    /// <summary>
    /// ボタン4：タイトルに戻る（Title）
    /// </summary>
    public void ReturnToTitle()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("SelectConfirmed", Vector3.zero);

        Time.timeScale = 1f; // ★タイトルシーンに戻る前に時間を必ず1に戻す！
        SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>
    /// ボタン5：ゲーム終了（Exit）
    /// </summary>
    public void Quit()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("SelectConfirmed", Vector3.zero);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}