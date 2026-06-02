using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverMenuManager : MonoBehaviour
{
    [Header("ーー シーン名設定 ーー")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("ーー ゲームパッド用の初期選択ボタン ーー")]
    [SerializeField] private Button retryButton; // 画面が開いたときに最初にフォーカスするボタン

    /// <summary>
    /// 💀【外部連携用】別スクリプトでGameOver画面を表示した「直後」にこれを呼び出してください
    /// </summary>
    public void ActivateMenu()
    {
        // ゲームパッド操作のために「Retry」ボタンを強制フォーカス
        SetSelectedButton(retryButton);
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
    /// ボタン1：もう一度最初から（Retry）
    /// </summary>
    public void RetryGame()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("SelectConfirmed", Vector3.zero);

        Time.timeScale = 1f; // ★シーン読み込み前に時間を必ず1に戻す！

        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
    }

    /// <summary>
    /// ボタン2：タイトルに戻る（Return to Title）
    /// </summary>
    public void ReturnToTitle()
    {
        // ※SoundManager.Instance.PlaySEAtPosition("SelectConfirmed", Vector3.zero);

        Time.timeScale = 1f; // ★タイトルシーンに戻る前に時間を必ず1に戻す！
        SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>
    /// ボタン3：ゲーム終了（Quit）
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