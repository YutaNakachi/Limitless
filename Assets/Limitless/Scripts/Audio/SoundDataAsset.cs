using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundDataAsset", menuName = "Audio/SoundDataAsset")]
public class SoundDataAsset : ScriptableObject
{
    // --- 🔊 SE（効果音）用のデータ構造 ---
    [System.Serializable]
    public struct SoundEffect
    {
        public string key;                  // "Kick", "Resonance_Start", "Footstep" などの識別名
        public AudioClip clip;              // 再生するオーディオファイル
        [Range(0f, 1f)] public float volume; // 個別の音量調整（初期値は1.0推奨）
    }

    // --- 🎵 BGM（背景音楽）用のデータ構造 ---
    [System.Serializable]
    public struct BackgroundMusic
    {
        public string key;                  // "Title", "Stage1", "Boss" などの識別名
        public AudioClip clip;              // 再生するオーディオファイル
        [Range(0f, 1f)] public float volume; // 曲ごとの音量調整（初期値は1.0推奨）
    }

    [Header("ーー SE（効果音）データの登録 ーー")]
    public List<SoundEffect> seList;

    [Header("ーー BGM（背景音楽）データの登録 ーー")]
    public List<BackgroundMusic> bgmList;

    /// <summary>
    /// 指定されたキーに対応するSEデータを取得する
    /// </summary>
    public SoundEffect GetSE(string key)
    {
        var se = seList.Find(s => s.key == key);
        if (se.clip == null)
        {
            Debug.LogWarning($"⚠️ SEキー: '{key}' が見つからないか、Clipが未設定です。");
        }
        return se;
    }

    /// <summary>
    /// 指定されたキーに対応するBGMデータを取得する
    /// </summary>
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