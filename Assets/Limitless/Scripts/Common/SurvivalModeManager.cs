using System.Collections.Generic;
using TMPro; // UI表示にTextMeshProを使用する場合
using UnityEngine;

public class SurvivalModeManager : MonoBehaviour
{
    [Header("ーー プレイヤー設定 ーー")]
    [SerializeField] private MobStatus playerStatus;

    [Header("ーー スポーン設定 ーー")]
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float initialSpawnInterval = 5f;
    [SerializeField] private float minSpawnInterval = 1f;
    [SerializeField] private float spawnSpeedUpRate = 0.05f; // 1秒ごとにスポーン間隔がどれだけ短くなるか

    [Header("ーー Batの強化設定 ーー")]
    [SerializeField] private int baseBatHp = 1;
    [SerializeField] private float hpScaleRate = 0.1f; // 1秒ごとに最大HPがどれだけ上昇するか

    [Header("ーー コンボ設定 ーー")]
    [SerializeField] private float comboTimeout = 5f;

    [Header("ーー UI参照 (任意) ーー")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText; // リザルト表示用のテキスト

    // 内部ステート管理
    private List<GameObject> _activeBats = new List<GameObject>();
    private float _currentSpawnInterval;
    private float _spawnTimer;
    private float _elapsedTime;

    private int _score;
    private int _comboCount;
    private float _comboTimer;
    private bool _isGameOver;

    // 🔥 リザルト用に追加した内部変数
    private int _killCount;
    private int _maxComboCount;

    private void Start()
    {
        _currentSpawnInterval = initialSpawnInterval;
        _spawnTimer = _currentSpawnInterval;
        _elapsedTime = 0f;
        _score = 0;
        _comboCount = 0;
        _killCount = 0;      // 初期化
        _maxComboCount = 0;  // 初期化
        _isGameOver = false;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // プレイヤーのイベントを予約
        if (playerStatus != null)
        {
            playerStatus.OnDeathEvent += (playerObj) => GameOver();
            playerStatus.OnTakeDamageEvent += (damage) => OnPlayerDamaged();
        }

        // 最初の1匹を即座にスポーン
        SpawnBat();
    }

    private void Update()
    {
        if (_isGameOver) return;

        _elapsedTime += Time.deltaTime;

        // 1. スポーン時間間隔の短縮化
        _currentSpawnInterval = Mathf.Max(minSpawnInterval, initialSpawnInterval - (_elapsedTime * spawnSpeedUpRate));

        // 2. スポーンタイマー処理、または画面内0匹時の即時スポーン
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f || _activeBats.Count == 0)
        {
            SpawnBat();
            _spawnTimer = _currentSpawnInterval;
        }

        // 3. コンボタイマーの監視
        if (_comboCount > 0)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f)
            {
                BreakCombo();
            }
        }

        UpdateUI();
    }

    /// <summary>
    /// Batを生成する
    /// </summary>
    private void SpawnBat()
    {
        if (spawnPoints.Length == 0 || batPrefab == null) return;

        Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject newBat = Instantiate(batPrefab, sp.position, Quaternion.identity);

        _activeBats.Add(newBat);

        EnemyStatus enemyStatus = newBat.GetComponent<EnemyStatus>();
        if (enemyStatus != null)
        {
            int bonusHp = Mathf.FloorToInt(_elapsedTime * hpScaleRate);
            int targetHp = baseBatHp + bonusHp;

            enemyStatus.SetMaxLife(targetHp);

            // イベント自動紐付け
            enemyStatus.OnTakeDamageEvent += (damage) => OnPlayerHitSuccess();
            enemyStatus.OnDeathEvent += (batObj) => OnBatKilled(batObj);
        }
    }

    /// <summary>
    /// 敵に攻撃がヒットしたときに呼ばれる
    /// </summary>
    private void OnPlayerHitSuccess()
    {
        if (_isGameOver) return;

        _comboCount++;
        _comboTimer = comboTimeout;

        // 🔥 【追加】最大コンボ数をリアルタイムに更新・記録
        if (_comboCount > _maxComboCount)
        {
            _maxComboCount = _comboCount;
        }

        Debug.Log($"🔥 コンボ継続！ 現在: {_comboCount} Combo (最高: {_maxComboCount})");
    }

    /// <summary>
    /// Playerがダメージを受けたときに呼ばれる
    /// </summary>
    private void OnPlayerDamaged()
    {
        if (_isGameOver) return;
        BreakCombo();
    }

    /// <summary>
    /// コンボが途切れたときの処理
    /// </summary>
    private void BreakCombo()
    {
        if (_comboCount > 0)
        {
            Debug.Log("🍃 コンボが途切れた...");
            _comboCount = 0;
        }
    }

    /// <summary>
    /// Batが倒されたときに呼び出される（スコア加算）
    /// </summary>
    private void OnBatKilled(GameObject bat)
    {
        if (_isGameOver) return;

        if (_activeBats.Contains(bat))
        {
            _activeBats.Remove(bat);
        }

        // 🔥 【追加】Kill数をカウントアップ
        _killCount++;

        // スコア計算（コンボ倍率を適用）
        int baseScore = 100;
        float comboMultiplier = 1f + (_comboCount * 0.1f);
        int addedScore = Mathf.RoundToInt(baseScore * comboMultiplier);

        _score += addedScore;
        Debug.Log($"💀 Bat撃破！ スコア+{addedScore} 現在のKill数: {_killCount}");
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = $"SCORE: {_score}";
        if (comboText != null)
        {
            comboText.text = _comboCount > 0 ? $"{_comboCount} COMBO" : "";
        }
    }

    private void GameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        Debug.Log($"📢 GAME OVER! スコア: {_score}, KILLS: {_killCount}, MAX COMBO: {_maxComboCount}");

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // 🔥 【大改造】リザルトテキストに3つの情報をまとめてかっこよく表示
        if (finalScoreText != null)
        {
            finalScoreText.text = $"<size=120%> == GAME OVER == </size>\n\n" +
                                  $"FINAL SCORE : {_score}\n" +
                                  $"TOTAL KILLS : {_killCount}\n" +
                                  $"MAX COMBO   : {_maxComboCount} Combo";
        }

        //Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (playerStatus != null)
        {
            playerStatus.OnDeathEvent -= (playerObj) => GameOver();
            playerStatus.OnTakeDamageEvent -= (damage) => OnPlayerDamaged();
        }

        Time.timeScale = 1f;
    }
}