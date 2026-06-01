using UnityEngine;

public class TrainingManager : MonoBehaviour
{
    [Header("ーー 敵の設定 ーー")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("ーー 配置ポイント（5箇所分を登録） ーー")]
    [SerializeField] private Transform[] spawnPoints;

    // 各配置ポイントにいる敵の生存を管理する配列
    private GameObject[] _activeEnemies;

    void Start()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("🚨 Spawn Points が設定されていません！");
            return;
        }

        _activeEnemies = new GameObject[spawnPoints.Length];

        // 💡 初期配置として5体を生成
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnEnemyAtIndex(i);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("Training");
        }
    }

    void Update()
    {
        // 💡 毎フレーム、各スロットの敵が死んだ（Destroyされた）かチェック
        for (int i = 0; i < _activeEnemies.Length; i++)
        {
            // オブジェクトがnull（破棄された）になっていたら、その場所に復活させる
            if (_activeEnemies[i] == null)
            {
                SpawnEnemyAtIndex(i);
            }
        }
    }

    /// <summary>
    /// 指定されたインデックスの配置ポイントに敵を生成する
    /// </summary>
    private void SpawnEnemyAtIndex(int index)
    {
        if (spawnPoints[index] == null || enemyPrefab == null) return;

        // 生成
        GameObject newEnemy = Instantiate(enemyPrefab, spawnPoints[index].position, spawnPoints[index].rotation);
        _activeEnemies[index] = newEnemy;
    }

    public void StopStageBGM()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
    }

}