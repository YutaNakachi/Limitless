using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        // 60FPSに固定
        Application.targetFrameRate = 60;
    }

    private void Start()
    {
        // 💡 SoundManagerのBGM専用メソッドを呼び出し、"Survival" をループ再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("Survival");
        }
    }

    /// <summary>
    /// ステージクリアやゲームオーバー時など、明示的にBGMを止めたい場合に外部から呼ぶメソッド
    /// </summary>
    public void StopStageBGM()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
    }
}