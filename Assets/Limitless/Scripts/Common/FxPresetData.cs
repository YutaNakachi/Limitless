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

        [Header("Continuous Occurrence Limit")]
        [Tooltip("チェックを入れると、この演出が連続で大量に呼ばれた際に演出を自動で間引きます")]
        public bool useCoolTime; // 👈 これを新しく追加します！
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