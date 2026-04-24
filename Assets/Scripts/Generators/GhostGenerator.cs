using System.Collections;
using UnityEngine;

public class GhostGenerator : ObjectGenerator
{
    [SerializeField] private GameObject ghostPrefab;

    public float ghostSpawnInterval;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(GhostSpawnLoop());
    }

    private IEnumerator GhostSpawnLoop()
    {
        while (true)
        {
            // 1. 指定した秒数待機
            yield return new WaitForSeconds(ghostSpawnInterval);

            // 2. 生成処理
            GenerateObject(ghostPrefab, new Vector3(Random.Range(-7.5f, 7.5f), Random.Range(0.0f, 3.5f)));

            // 3. ループの先頭に戻り、再び待機に入る
        }
    }
}
