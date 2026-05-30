using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("ーー 効果音・BGMアセットの登録 ーー")]
    [SerializeField] private SoundDataAsset seAsset;

    private AudioSource _globalSource;   // SE再生用の2Dスピーカー
    private AudioSource _bgmSource;      // BGM再生用のループスピーカー

    void Awake()
    {
        // 🛠️ シングルトン（Singleton）の設定：世界に1つだけの存在にする
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーンが切り替わっても破棄しない
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 🔊 SE用のベースAudioSourceを自分自身に自動追加
        _globalSource = gameObject.AddComponent<AudioSource>();
        _globalSource.playOnAwake = false;
        _globalSource.spatialBlend = 0f; // 完全な2Dサウンド設定（画面全体で均一に鳴らす）

        // 🎵 BGM用のベースAudioSourceを自分自身に自動追加
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;          // BGMは基本自動ループ
        _bgmSource.spatialBlend = 0f;    // 完全な2Dサウンド設定
    }

    // ==========================================
    // 🔊 SE（効果音）コントロール
    // ==========================================

    /// <summary>
    /// 2Dサウンド（画面全体・UI・どこでも一律の音量）としてSEを鳴らす
    /// </summary>
    public void PlaySE(string key)
    {
        if (seAsset == null) return;
        var se = seAsset.GetSE(key);
        if (se.clip != null)
        {
            _globalSource.PlayOneShot(se.clip, se.volume);
        }
    }

    /// <summary>
    /// 3Dサウンド（指定したキャラクターなどの位置）からSEを鳴らす
    /// </summary>
    public void PlaySEAtPosition(string key, Vector3 position)
    {
        if (seAsset == null) return;
        var se = seAsset.GetSE(key);
        if (se.clip != null)
        {
            // 指定座標に一瞬だけUnityが自動でスピーカーを作って鳴らし、終わったら自動消滅する（メモリに優しい）
            AudioSource.PlayClipAtPoint(se.clip, position, se.volume);
        }
    }

    // ==========================================
    // 🎵 BGM（背景音楽）コントロール
    // ==========================================

    /// <summary>
    /// BGMを再生する（すでに同じ曲が流れている場合は巻き戻らないようスルーする安全設計）
    /// </summary>
    public void PlayBGM(string key)
    {
        if (seAsset == null) return;
        var bgm = seAsset.GetBGM(key);

        if (bgm.clip != null)
        {
            // 現在再生中の曲と「全く同じ曲」をリクエストされた場合はスルー
            if (_bgmSource.clip == bgm.clip && _bgmSource.isPlaying) return;

            _bgmSource.clip = bgm.clip;
            _bgmSource.volume = bgm.volume;
            _bgmSource.Play();
        }
    }

    /// <summary>
    /// BGMを一時停止する（ポーズメニューを開いた時など）
    /// </summary>
    public void PauseBGM()
    {
        if (_bgmSource.isPlaying)
        {
            _bgmSource.Pause();
        }
    }

    /// <summary>
    /// 一時停止したBGMを途中から再開する（ポーズメニューを閉じた時など）
    /// </summary>
    public void ResumeBGM()
    {
        if (!_bgmSource.isPlaying && _bgmSource.clip != null)
        {
            _bgmSource.UnPause();
        }
    }

    /// <summary>
    /// BGMを完全にストップする
    /// </summary>
    public void StopBGM()
    {
        _bgmSource.Stop();
        _bgmSource.clip = null; // クリップもクリア
    }
}