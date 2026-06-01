using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DummyWallGenerator : MonoBehaviour
{
    [Header("ーー プレハブ・オブジェクト設定 ーー")]
    [SerializeField] private GameObject dummyWallPrefab;
    [SerializeField] private Tilemap realWallTilemap;

    [Header("ーー 左右の壁の親（StageManagerの子要素） ーー")]
    [SerializeField] private Transform leftWallGroup;
    [SerializeField] private Transform rightWallGroup;

    [Header("ーー 落下高さ設定 ーー")]
    [SerializeField] private float spawnHeightY = 15f;

    private List<GameObject> _spawnedDummies = new List<GameObject>();

    // 💡 左右のコルーチンが両方とも終わったかを数えるカウンター
    private int _finishedCoroutineCount = 0;

    void Start()
    {
        if (realWallTilemap != null)
        {
            if (realWallTilemap.TryGetComponent<TilemapRenderer>(out var renderer))
            {
                renderer.enabled = false;
            }

            // 💡 演出スタート
            StartParallelDrop();
        }
    }

    private void StartParallelDrop()
    {
        if (realWallTilemap == null || dummyWallPrefab == null) return;

        // 💡 1. 左右それぞれの着地座標を入れるリストを用意
        List<Vector3> leftPositions = new List<Vector3>();
        List<Vector3> rightPositions = new List<Vector3>();

        BoundsInt bounds = realWallTilemap.cellBounds;
        float managerX = transform.position.x;

        // 下から上へ、1マスずつスキャン
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                if (realWallTilemap.HasTile(cellPos))
                {
                    Vector3 worldPos = realWallTilemap.GetCellCenterWorld(cellPos);

                    // 💡 スキャンした時点で、右側か左側かでリストを振り分ける
                    if (worldPos.x >= managerX)
                    {
                        rightPositions.Add(worldPos);
                    }
                    else
                    {
                        leftPositions.Add(worldPos);
                    }
                }
            }
        }

        // カウンターをリセット（2つのコルーチンが走るため）
        _finishedCoroutineCount = 0;

        // 💡 2. 左側と右側のコルーチンを「同時に」別々でスタートさせる！
        StartCoroutine(DropGroupCoroutine(leftPositions, leftWallGroup));
        StartCoroutine(DropGroupCoroutine(rightPositions, rightWallGroup));

        // 💡 3. 両方が終わるのを監視するコルーチンも裏で1つ動かす
        StartCoroutine(WaitForAllRepairsCoroutine());
    }

    /// <summary>
    /// 💡 指定されたリストの座標へ、順番にダミーを落とす汎用コルーチン
    /// </summary>
    private IEnumerator DropGroupCoroutine(List<Vector3> targetPositions, Transform parentGroup)
    {
        foreach (Vector3 targetPos in targetPositions)
        {
            Vector3 spawnPos = new Vector3(targetPos.x, spawnHeightY, realWallTilemap.transform.position.z);

            GameObject dummy = Instantiate(dummyWallPrefab, spawnPos, Quaternion.identity);
            _spawnedDummies.Add(dummy);

            // 親子関係を設定（LeftWall または RightWall の配下へ）
            if (parentGroup != null) dummy.transform.SetParent(parentGroup);

            if (dummy.TryGetComponent<Rigidbody2D>(out var rb))
            {
                yield return null;

                // 着地待ち
                while (rb != null && !(Mathf.Abs(rb.linearVelocity.y) < 0.01f && dummy.transform.position.y <= targetPos.y + 0.1f))
                {
                    yield return null;
                }

                if (rb == null) continue;

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;

                SoundManager.Instance.PlaySEAtPosition("DummyWall", dummy.transform.position);

                dummy.transform.position = targetPos;
            }

            yield return new WaitForSeconds(0.01f);
        }

        // 💡 このグループの落下演出が終わったらカウンターを増やす
        _finishedCoroutineCount++;
    }

    /// <summary>
    /// 💡 左右両方のコルーチンが完全に終了するのを待ってから本物と入れ替える監視コルーチン
    /// </summary>
    private IEnumerator WaitForAllRepairsCoroutine()
    {
        // 左右の2つのコルーチンが両方終わる（値が2になる）まで待機
        while (_finishedCoroutineCount < 2)
        {
            yield return null;
        }

        // 全て終わったら、0.5秒の余韻ののち、本物と入れ替え！
        yield return new WaitForSeconds(0.5f);
        SwitchToRealWall();
    }

    private void SwitchToRealWall()
    {
        if (realWallTilemap != null)
        {
            if (realWallTilemap.TryGetComponent<TilemapRenderer>(out var renderer))
            {
                renderer.enabled = true;
            }
        }

        if (_spawnedDummies != null)
        {
            foreach (var dummy in _spawnedDummies)
            {
                if (dummy != null) dummy.SetActive(false);
            }
        }

        DeactivateAllChildren(leftWallGroup);
        DeactivateAllChildren(rightWallGroup);
    }

    private void DeactivateAllChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent)
        {
            if (child != null) child.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (realWallTilemap != null)
        {
            if (realWallTilemap.TryGetComponent<TilemapRenderer>(out var renderer))
            {
                renderer.enabled = true;
            }
        }
    }
}