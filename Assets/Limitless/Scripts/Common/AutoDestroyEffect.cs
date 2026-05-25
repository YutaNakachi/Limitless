using UnityEngine;

public class AutoDestroyEffect : MonoBehaviour
{
    [SerializeField] private float destroyDelay = 1f; // 消滅するまでの時間（秒）

    void Start()
    {
        // 指定された秒数後に、このエフェクト自体をメモリから削除する
        Destroy(gameObject, destroyDelay);
    }
}