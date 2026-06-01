using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenuManager : MonoBehaviour
{
    [Header("Transition Scene Name")]
    [SerializeField] private string survivalModeSceneName = "SurvivalModeScene";
    [SerializeField] private string trainingModeSceneName = "TrainingModeScene";

    void Start()
    {
        // タイトル画面に戻ってきたときのために、念のためタイムスケールを戻しておく
        Time.timeScale = 1f;

        // もしタイトル用のBGMを流すならここで再生
        SoundManager.Instance.PlayBGM("Title");
    }

    /// <summary>
    /// ゲーム開始ボタンから呼ばれるメソッド
    /// </summary>
    public void OnSelectSurvivalMode()
    {
        // 決定音などを鳴らす
        // SoundManager.Instance.PlaySE("SelectConfirmed");

        // シーンの読み込み
        SceneManager.LoadScene(survivalModeSceneName);
    }
    public void OnSelectTrainingMode()
    {
        // 決定音などを鳴らす
        // SoundManager.Instance.PlaySE("SelectConfirmed");

        // シーンの読み込み
        SceneManager.LoadScene(trainingModeSceneName);
    }

    /// <summary>
    /// ゲーム終了ボタンから呼ばれるメソッド（ビルド後のみ動作）
    /// </summary>
    public void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}