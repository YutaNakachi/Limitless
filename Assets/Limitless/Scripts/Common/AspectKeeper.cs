using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectKeeper : MonoBehaviour
{
    void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        // 💡 エディタにデータを記憶させず、コード内で直接 16:9 をハードコーディング（自動計算）
        float targetRatio = 16f / 9f;

        float currentRatio = (float)Screen.width / Screen.height;
        float scaleWidth = currentRatio / targetRatio;

        Rect rect = cam.rect;

        if (scaleWidth < 1.0f)
        {
            rect.width = 1.0f;
            rect.height = scaleWidth;
            rect.x = 0;
            rect.y = (1.0f - scaleWidth) / 2.0f;
        }
        else
        {
            float scaleHeight = 1.0f / scaleWidth;
            rect.width = scaleHeight;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleHeight) / 2.0f;
            rect.y = 0;
        }

        cam.rect = rect;
    }
}