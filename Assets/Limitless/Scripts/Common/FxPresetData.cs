using UnityEngine;

[CreateAssetMenu(fileName = "NewFxPreset", menuName = "Custom/FX Preset Data")]
public class FxPresetData : ScriptableObject
{
    [System.Serializable]
    public struct FxSettings
    {
        public string label;

        [Header("Hit Stop")]
        public float stopDuration;
        [Range(0f, 1f)] public float timeScale;

        [Header("Camera Shake")]
        public float shakeDuration;
        public float shakeMagnitude;

        [Header("Object Shake")]
        public float objectShakeMagnitude;
        public bool useObjectShakeY;

        // 🎮【新規追加】ゲームパッドの振動設定
        [Header("Gamepad Game Haptics")]
        [Tooltip("左モーター：重い低周波の振動（ドンッという衝撃）")]
        [Range(0f, 1f)] public float rumbleLeft;
        [Tooltip("右モーター：高い高周波の振動（チリチリ・カリッとした感触）")]
        [Range(0f, 1f)] public float rumbleRight;

        [Header("Continuous Occurrence Limit")]
        [Tooltip("チェックを入れると、この演出が連続で大量に呼ばれた際に演出を自動で間引きます")]
        public bool useCoolTime;
    }

    public FxSettings[] presets;

    public FxSettings GetPreset(string labelName)
    {
        foreach (var preset in presets)
        {
            if (preset.label == labelName) return preset;
        }
        return presets.Length > 0 ? presets[0] : default;
    }
}