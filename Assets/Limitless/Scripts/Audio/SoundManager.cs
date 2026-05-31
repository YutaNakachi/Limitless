using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("ーー 効果音・BGMアセットの登録 ーー")]
    [SerializeField] private SoundDataAsset seAsset;

    private AudioSource _bgmSource;      // BGM再生用のループスピーカー

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // BGM用のスピーカーを自分自身に自動追加
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
    }

    // ==========================================
    // 🔊 SE（効果音）コントロール（トリミング対応版）
    // ==========================================

    /// <summary>
    /// 2Dサウンド（画面全体・UI）としてSEを鳴らす（トリミング対応）
    /// </summary>
    public void PlaySE(string key)
    {
        if (seAsset == null) return;
        var se = seAsset.GetSE(key);
        if (se.clip == null) return;

        // トリミング再生用に、一時的なAudioSourceを生成して再生
        GameObject tempGO = new GameObject($"TempSE_2D_{key}");
        tempGO.transform.SetParent(transform); // マネージャーの子にする

        AudioSource source = tempGO.AddComponent<AudioSource>();
        SetupSourceAndPlay(source, se.clip, se.volume, se.startTime, se.endTime, false, Vector3.zero, false);
    }

    /// <summary>
    /// 3Dサウンド（指定したキャラクターなどの位置）からSEを鳴らす（トリミング対応）
    /// </summary>
    public void PlaySEAtPosition(string key, Vector3 position)
    {
        if (seAsset == null) return;
        var se = seAsset.GetSE(key);
        if (se.clip == null) return;

        // 指定座標に一時的な3Dスピーカーオブジェクトを生成
        GameObject tempGO = new GameObject($"TempSE_3D_{key}");
        tempGO.transform.position = position;

        AudioSource source = tempGO.AddComponent<AudioSource>();
        SetupSourceAndPlay(source, se.clip, se.volume, se.startTime, se.endTime, true, position, true);
    }

    // スクリプトの取得を簡単にするため、SoundManagerの中にこれを入れておくとPlayerから一発で音データを参照できます
    public SoundDataAsset.SoundEffect GetSEData(string key)
    {
        return seAsset != null ? seAsset.GetSE(key) : default;
    }

    // スピーカーの共通初期化 ＆ 再生コルーチンの開始
    private void SetupSourceAndPlay(AudioSource source, AudioClip clip, float volume, float startTime, float endTime, bool is3D, Vector3 pos, bool destroyObj)
    {
        source.clip = clip;
        source.volume = volume;
        source.playOnAwake = false;

        if (is3D)
        {
            source.spatialBlend = 1f; // 💡完全な3Dサウンド
            source.minDistance = 1f;
            source.maxDistance = 20f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        else
        {
            source.spatialBlend = 0f; // 💡完全な2Dサウンド
        }

        // コルーチンを起動してトリミング再生を実行
        StartCoroutine(PlayWithTrimRoutine(source, startTime, endTime, destroyObj ? source.gameObject : null));
    }

    // トリミング再生を制御するコルーチン
    private IEnumerator PlayWithTrimRoutine(AudioSource source, float startTime, float endTime, GameObject objToDestroy)
    {
        // 再生開始位置を設定（クリップの長さを超えないようにクランプ）
        float startSec = Mathf.Clamp(startTime, 0f, source.clip.length);
        source.time = startSec;
        source.Play();

        // 終了時間を計算（endTimeが0、またはクリップ長以上の場合は最後まで再生）
        float endSec = (endTime > 0f && endTime < source.clip.length) ? endTime : source.clip.length;
        float duration = endSec - startSec;

        // 指定した再生時間ぶん待機
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        // 再生を停止
        if (source != null)
        {
            source.Stop();
        }

        // 使い終わったGameObjectの破棄（3Dの場合はそのオブジェクト、2Dの場合は子オブジェクト）
        if (objToDestroy != null)
        {
            Destroy(objToDestroy);
        }
        else if (source != null)
        {
            Destroy(source.gameObject);
        }
    }

    // ==========================================
    // 🎵 BGM（背景音楽）コントロール（トリミング対応版）
    // ==========================================

    /// <summary>
    /// BGMを再生する（トリミングの開始位置に対応）
    /// </summary>
    public void PlayBGM(string key)
    {
        if (seAsset == null) return;
        var bgm = seAsset.GetBGM(key);

        if (bgm.clip != null)
        {
            if (_bgmSource.clip == bgm.clip && _bgmSource.isPlaying) return;

            _bgmSource.clip = bgm.clip;
            _bgmSource.volume = bgm.volume;

            // 💡 BGMも開始時間を指定されていれば、そこから再生
            _bgmSource.time = Mathf.Clamp(bgm.startTime, 0f, bgm.clip.length);
            _bgmSource.Play();
        }
    }

    public void PauseBGM() { if (_bgmSource.isPlaying) _bgmSource.Pause(); }
    public void ResumeBGM() { if (!_bgmSource.isPlaying && _bgmSource.clip != null) _bgmSource.UnPause(); }
    public void StopBGM() { _bgmSource.Stop(); _bgmSource.clip = null; }
}