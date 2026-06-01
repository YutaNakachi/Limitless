using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
        // 60FPSに固定
        Application.targetFrameRate = 60;
    }
}