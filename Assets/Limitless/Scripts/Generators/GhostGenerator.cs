using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostGenerator : ObjectGenerator
{
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private int maxGhostCount = 10; // 最大同時出現数
    public float ghostSpawnInterval;

    // 現在画面に存在しているゴーストを保持するリスト
    private List<GameObject> activeGhosts = new List<GameObject>();

    void Start()
    {
        StartCoroutine(GhostSpawnLoop());
    }

    private IEnumerator GhostSpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(ghostSpawnInterval);

            // 1. すでに破壊された(nullになった)要素をリストから削除
            activeGhosts.RemoveAll(ghost => ghost == null);

            // 2. 現在の数が最大数未満なら生成する
            if (activeGhosts.Count < maxGhostCount)
            {
                GameObject newGhost = GenerateObject(ghostPrefab, new Vector3(Random.Range(-7.5f, 7.5f), Random.Range(0.0f, 3.5f)));

                // 生成したゴーストをリストに追加
                if (newGhost != null)
                {
                    activeGhosts.Add(newGhost);
                }
            }
            else
            {

            }
        }
    }
}