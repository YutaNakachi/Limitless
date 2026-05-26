using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapFaller : MonoBehaviour
{
    [Header("ーー 演出設定 ーー")]
    [SerializeField] private GameObject dummyTilePrefab;
    [SerializeField] private float dropHeight = 15f;

    private Tilemap _tilemap;
    private TilemapRenderer _renderer;

    // 💡 生成したダミーを常に監視するためのリスト
    private List<GameObject> _spawnedDummies = new List<GameObject>();

    void Start()
    {
        _tilemap = GetComponent<Tilemap>();
        _renderer = GetComponent<TilemapRenderer>();

        if (_tilemap != null && _renderer != null)
        {
            _renderer.enabled = false;
            DropTilesOneByOneAsync().Forget();
        }
    }

    private async UniTaskVoid DropTilesOneByOneAsync()
    {
        BoundsInt bounds = _tilemap.cellBounds;

        for (int y = bounds.yMax; y >= bounds.yMin; y--)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);

                if (_tilemap.HasTile(cellPos))
                {
                    Vector3 worldPos = _tilemap.GetCellCenterWorld(cellPos);
                    Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y + dropHeight, 0);

                    GameObject dummy = Instantiate(dummyTilePrefab, spawnPos, Quaternion.identity);

                    Sprite tileSprite = _tilemap.GetSprite(cellPos);
                    if (dummy.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                    {
                        spriteRenderer.sprite = tileSprite;
                    }

                    // 💡 生成したら即座にリストに登録する
                    _spawnedDummies.Add(dummy);

                    if (dummy.TryGetComponent<Rigidbody2D>(out var rb))
                    {
                        await UniTask.Yield();
                        await UniTask.WaitUntil(() => rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.05f);
                        if (rb == null) continue;
                    }

                    await UniTask.Delay(60);
                }
            }
        }

        await UniTask.Delay(300);

        // 💡 演出が正常に最後まで終わった場合の処理
        CleanUpDummies();
    }

    /// <summary>
    /// 🔥 【新設】ゲーム終了時やシーン遷移時に、残ったダミーを根こそぎ消去する安全装置
    /// </summary>
    private void OnDestroy()
    {
        CleanUpDummies();
    }

    /// <summary>
    /// 🔥 ダミーを一斉にお掃除する共通関数
    /// </summary>
    private void CleanUpDummies()
    {
        // 本番のタイルマップを表示状態に戻す（念のため）
        if (_renderer != null)
        {
            _renderer.enabled = true;
        }

        // リストにいるダミーを全員消去
        if (_spawnedDummies != null)
        {
            foreach (var dummy in _spawnedDummies)
            {
                if (dummy != null)
                {
                    Destroy(dummy);
                }
            }
            _spawnedDummies.Clear(); // リストの中身を空にする
        }
    }
}