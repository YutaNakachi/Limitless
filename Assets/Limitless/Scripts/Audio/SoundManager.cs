using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("ーー 効果音・BGMアセットの登録 ーー")]
    [SerializeField] private SoundDataAsset seAsset;

    private AudioSource _bgmSource;      // BGM再生用のループスピーカー

    // 💡【追加】現在ループ再生中の一時的なGameObjectを管理する辞書
    private Dictionary<string, GameObject> _activeLoopSEs = new Dictionary<string, GameObject>();

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

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
    }

    // ==========================================
    // 🔊 SE（効果音）コントロール（ループ・トリミング対応版）
    // ==========================================

    /// <summary>
    /// 2Dサウンド（画面全体・UI）としてSEを鳴らす
    /// </summary>
    public void PlaySE(string key)
    {
        if (seAsset == null) return;
        var se = seAsset.GetSE(key);
        if (se.clip == null) return;

        // すでに同じキーのループSEが鳴っている場合は重複防止
        if (se.isLoop && _activeLoopSEs.ContainsKey(key)) return;

        GameObject tempGO = new GameObject($"TempSE_2D_{key}");
        tempGO.transform.SetParent(transform);

        AudioSource source = tempGO.AddComponent<AudioSource>();

        // 💡 ループSEの場合は辞書に登録
        if (se.isLoop) _activeLoopSEs[key] = tempGO;

        SetupSourceAndPlay(source, se, false, Vector3.zero, se.isLoop);
    }

    /// <summary>
    /// 3Dサウンド（指定座標）からSEを鳴らす
    /// </summary>
    public void PlaySEAtPosition(string key, Vector3 position)
    {
        if (seAsset == null) return;
        var se = seAsset.GetSE(key);
        if (se.clip == null) return;

        // すでに同じキーのループSEが鳴っている場合は重複防止
        if (se.isLoop && _activeLoopSEs.ContainsKey(key)) return;

        GameObject tempGO = new GameObject($"TempSE_3D_{key}");
        tempGO.transform.position = position;

        // 💡 ループSEの場合、動くキャラクターに追従させるなら、親をそのキャラクターにするアプローチも可能ですが、
        // 今回は位置指定型の単発・ループ両対応として安全に生成します。
        AudioSource source = tempGO.AddComponent<AudioSource>();

        if (se.isLoop) _activeLoopSEs[key] = tempGO;

        SetupSourceAndPlay(source, se, true, position, se.isLoop);
    }

    /// <summary>
    /// 💡【新規追加】ループ再生中の効果音を明示的に停止し、オブジェクトを破棄する
    /// </summary>
    public void StopLoopSE(string key)
    {
        if (_activeLoopSEs.TryGetValue(key, out GameObject targetObj))
        {
            if (targetObj != null)
            {
                Destroy(targetObj);
            }
            _activeLoopSEs.Remove(key);
        }
    }

    /// <summary>
    /// 外部のPlayerスクリプトなどから安全にSEデータを参照するためのメソッド
    /// </summary>
    public SoundDataAsset.SoundEffect GetSEData(string key)
    {
        return seAsset != null ? seAsset.GetSE(key) : default;
    }

    // スピーカーの初期化設定
    private void SetupSourceAndPlay(AudioSource source, SoundDataAsset.SoundEffect se, bool is3D, Vector3 pos, bool isLoop)
    {
        source.clip = se.clip;
        source.volume = se.volume;
        source.playOnAwake = false;
        source.loop = isLoop; // 💡 アセットの設定に基づいてループ設定

        if (is3D)
        {
            source.spatialBlend = 1f;
            source.minDistance = 5f;
            source.maxDistance = 20f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        // コルーチンを起動（ループ音か通常音かで挙動を切り替える）
        StartCoroutine(PlayWithTrimRoutine(source, se.startTime, se.endTime, isLoop ? null : source.gameObject));
    }

    // トリミング再生・自動破棄コルーチン
    private IEnumerator PlayWithTrimRoutine(AudioSource source, float startTime, float endTime, GameObject objToDestroy)
    {
        float startSec = Mathf.Clamp(startTime, 0f, source.clip.length);
        float endSec = (endTime > 0f && endTime < source.clip.length) ? endTime : source.clip.length;

        source.time = startSec;
        source.Play();

        // 💡 ループ再生の場合（手動ループ制御に切り替え）
        if (source.loop)
        {
            // source.loop自体はtrueのままだとUnityが勝手に末尾で戻してしまうので、
            // プログラムで制御するために内部的にはfalseにして監視します。
            source.loop = false;

            while (source != null)
            {
                // endTime（またはファイル末尾）に達したか監視
                if (source.time >= endSec || !source.isPlaying)
                {
                    source.time = startSec; // 指定した開始位置に戻す
                    source.Play();
                }
                yield return null; // 毎フレームチェック
            }
            yield break;
        }

        // 通常の単発SE（変更なし）
        float duration = endSec - startSec;
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        if (source != null) source.Stop();
        if (objToDestroy != null) Destroy(objToDestroy);
    }

    // ==========================================
    // 🎵 BGM（背景音楽）コントロール
    // ==========================================
    public void PlayBGM(string key)
    {
        if (seAsset == null) return;
        var bgm = seAsset.GetBGM(key);

        if (bgm.clip != null)
        {
            if (_bgmSource.clip == bgm.clip && _bgmSource.isPlaying) return;

            _bgmSource.clip = bgm.clip;
            _bgmSource.volume = bgm.volume;
            _bgmSource.time = Mathf.Clamp(bgm.startTime, 0f, bgm.clip.length);
            _bgmSource.Play();
        }
    }

    public void PauseBGM() { if (_bgmSource.isPlaying) _bgmSource.Pause(); }
    public void ResumeBGM() { if (!_bgmSource.isPlaying && _bgmSource.clip != null) _bgmSource.UnPause(); }
    public void StopBGM() { _bgmSource.Stop(); _bgmSource.clip = null; }
}