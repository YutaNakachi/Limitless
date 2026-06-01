using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundDataAsset", menuName = "Audio/SoundDataAsset")]
public class SoundDataAsset : ScriptableObject
{
    // --- 🔊 SE（効果音）用のデータ構造 ---
    [System.Serializable]
    public struct SoundEffect
    {
        public string key;                  // 識別名
        public AudioClip clip;              // オーディオファイル
        [Range(0f, 1f)] public float volume; // 個別の音量調整

        // 💡 ループさせるかどうかの設定フラグ
        [Header("🔁 ループ設定")]
        public bool isLoop;

        [Header("⏱️ トリミング設定（秒単位 / 0の場合は無効）")]
        public float startTime;
        public float endTime;
    }

    // --- 🎵 BGM（背景音楽）用のデータ構造 ---
    [System.Serializable]
    public struct BackgroundMusic
    {
        public string key;                  // 識別名
        public AudioClip clip;              // オーディオファイル
        [Range(0f, 1f)] public float volume; // 曲ごとの音量調整

        // 💡【修正】BGM側にもループ区間を指定できるように endTime を追加
        [Header("⏱️ トリミング設定（秒単位 / 0の場合は無効）")]
        public float startTime;             // 何秒目から再生（またはループ復帰）するか
        public float endTime;               // 何秒目でループ（折り返し）させるか
    }

    [Header("ーー SE（効果音）データの登録 ーー")]
    public List<SoundEffect> seList;

    [Header("ーー BGM（背景音楽）データの登録 ーー")]
    public List<BackgroundMusic> bgmList;

    public SoundEffect GetSE(string key)
    {
        var se = seList.Find(s => s.key == key);
        if (se.clip == null)
        {
            Debug.LogWarning($"⚠️ SEキー: '{key}' が見つからないか、Clipが未設定です。");
        }
        return se;
    }

    public BackgroundMusic GetBGM(string key)
    {
        var bgm = bgmList.Find(b => b.key == key);
        if (bgm.clip == null)
        {
            Debug.LogWarning($"⚠️ BGMキー: '{key}' が見つからないか、Clipが未設定です。");
        }
        return bgm;
    }
}