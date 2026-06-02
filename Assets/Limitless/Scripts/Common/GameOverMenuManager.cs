using System.Collections;
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

    [Header("ーー ✨演出用：各ボタンのコンポーネント ーー")]
    // 0:Retry, 1:Title, 2:Quit の順番でアタッチ
    [SerializeField] private Outline[] buttonOutlines; // 各ボタンのOutlineコンポーネント
    [SerializeField] private Image[] buttonTargetImages; // 色を変えたいImage（ボタン背景など）

    [Header("ーー ✨演出用：アウトラインの太さ設定 ーー")]
    [SerializeField] private Vector2 normalOutlineEffectDistance = new Vector2(1f, -1f);
    [SerializeField] private Vector2 highlightedOutlineEffectDistance = new Vector2(3f, -3f);

    [Header("ーー ✨演出用：カラー設定 ーー")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightedColor = Color.red; // 💡ゲームオーバーなので赤やオレンジ系も似合います（お好みで）

    void Start()
    {
        // 起動時に念のためすべてのハイライトを通常状態に戻しておく
        ResetAllHighlights();
    }

    /// <summary>
    /// 💀【外部連携用】別スクリプトでGameOver画面を表示した「直後」にこれを呼び出してください
    /// </summary>
    public void ActivateMenu()
    {
        // 画面が開く瞬間に一度すべて通常状態へリセット
        ResetAllHighlights();
        // 1フレーム待ってから安全に「Retry」ボタンを強制フォーカス
        StartCoroutine(SelectFirstButtonDelay());
    }

    private IEnumerator SelectFirstButtonDelay()
    {
        yield return null; // 確実なフォーカスのために1フレーム待機
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
    // ✨【新規】引数を1つにしてUnityに認識させるメソッド
    // ==========================================

    /// <summary>
    /// ボタンが選択された（フォーカス・ホバーされた）とき【EventTrigger用】
    /// </summary>
    /// <param name="index">0:Retry, 1:Title, 2:Quit</param>
    public void OnButtonSelect(int index)
    {
        SetButtonVisualState(index, true);
    }

    /// <summary>
    /// ボタンから選択が外れた（離れた）とき【EventTrigger用】
    /// </summary>
    /// <param name="index">0:Retry, 1:Title, 2:Quit</param>
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
    // 🔘 各ボタンのクリックイベント（Submitと連動）
    // ==========================================

    public void RetryGame()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("ConfirmPauseMenu");

        Time.timeScale = 1f;
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);
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