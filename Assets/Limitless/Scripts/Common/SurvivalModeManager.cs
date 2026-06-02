using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem; // ✨【追加】Input Systemを使用可能にする

public class SurvivalModeManager : MonoBehaviour
{
    [Header("ーー プレイヤー設定 ーー")]
    [SerializeField] private MobStatus playerStatus;

    // ✨【追加】プレイヤーの入力をゲームオーバー時に最速で止めるための参照
    [SerializeField] private PlayerInput playerInput;

    [Header("ーー スポーン設定 ーー")]
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float initialSpawnInterval = 5f;
    [SerializeField] private float minSpawnInterval = 1f;
    [SerializeField] private float spawnSpeedUpRate = 0.05f;

    [Header("ーー Batの強化設定 ーー")]
    [SerializeField] private int baseBatHp = 1;
    [SerializeField] private float hpScaleRate = 0.1f;

    [Header("ーー コンボ設定 ーー")]
    [SerializeField] private float comboTimeout = 5f;

    [Header("ーー UI参照 (任意) ーー")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private GameOverMenuManager gameOverMenuManager;

    private List<GameObject> _activeBats = new List<GameObject>();
    private float _currentSpawnInterval;
    private float _spawnTimer;
    private float _elapsedTime;

    private int _score;
    private int _comboCount;
    private float _comboTimer;
    private bool _isGameOver;

    private int _killCount;
    private int _maxComboCount;

    private void Start()
    {
        _currentSpawnInterval = initialSpawnInterval;
        _spawnTimer = _currentSpawnInterval;
        _elapsedTime = 0f;
        _score = 0;
        _comboCount = 0;
        _killCount = 0;
        _maxComboCount = 0;
        _isGameOver = false;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (playerStatus != null)
        {
            playerStatus.OnDeathEvent += (playerObj) => GameOver();
            playerStatus.OnTakeDamageEvent += (damage) => OnPlayerDamaged();
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("Survival");
        }

        SpawnBat();
    }

    private void Update()
    {
        if (_isGameOver) return;

        _elapsedTime += Time.deltaTime;
        _currentSpawnInterval = Mathf.Max(minSpawnInterval, initialSpawnInterval - (_elapsedTime * spawnSpeedUpRate));

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f || _activeBats.Count == 0)
        {
            SpawnBat();
            _spawnTimer = _currentSpawnInterval;
        }

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

            enemyStatus.OnTakeDamageEvent += (damage) => OnPlayerHitSuccess();
            enemyStatus.OnDeathEvent += (batObj) => OnBatKilled(batObj);
        }
    }

    private void OnPlayerHitSuccess()
    {
        if (_isGameOver) return;

        _comboCount++;
        _comboTimer = comboTimeout;

        if (_comboCount > _maxComboCount)
        {
            _maxComboCount = _comboCount;
        }

        Debug.Log($"🔥 コンボ継続！ 現在: {_comboCount} Combo (最高: {_maxComboCount})");
    }

    private void OnPlayerDamaged()
    {
        if (_isGameOver) return;
        BreakCombo();
    }

    private void BreakCombo()
    {
        if (_comboCount > 0)
        {
            Debug.Log("🍃 コンボが途切れた...");
            _comboCount = 0;
        }
    }

    private void OnBatKilled(GameObject bat)
    {
        if (_isGameOver) return;

        if (_activeBats.Contains(bat))
        {
            _activeBats.Remove(bat);
        }

        _killCount++;

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

    public void StopStageBGM()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
        }
    }

    private void GameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        StopStageBGM();

        // ✨【重要】死亡した瞬間に、プレイヤーのアクションマップ（操作）を即座に止める！
        // これにより、リザルト画面が出るまでの2.5秒間も死体撃ちや暴発を完全に防げます
        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player")?.Disable();
        }

        StartCoroutine(ResultCoroutine());
    }

    private IEnumerator ResultCoroutine()
    {
        // 死亡演出を待つ間（2.5秒間）はゲーム内の時間は動いているため、
        // 敵の動きやパーティクルは綺麗に再生され続けます
        yield return new WaitForSeconds(2.5f);
        Debug.Log($"📢 GAME OVER! スコア: {_score}, KILLS: {_killCount}, MAX COMBO: {_maxComboCount}");

        // ✨【重要】リザルトパネルが開いた段階で、ゲーム内の時間を完全停止させる
        Time.timeScale = 0f;

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (finalScoreText != null)
        {
            finalScoreText.text = $"<size=120%> == GAME OVER == </size>\n\n" +
                                  $"FINAL SCORE : {_score}\n" +
                                  $"TOTAL KILLS : {_killCount}\n" +
                                  $"MAX COMBO : {_maxComboCount} Combo";
        }

        if (gameOverMenuManager != null)
        {
            gameOverMenuManager.ActivateMenu();
        }
    }

    private void OnDestroy()
    {
        if (playerStatus != null)
        {
            playerStatus.OnDeathEvent -= (playerObj) => GameOver();
            playerStatus.OnTakeDamageEvent -= (damage) => OnPlayerDamaged();
        }

        StopStageBGM();

        // シーン破棄（終了・リトライ時）には必ず時間を1に戻しておく
        Time.timeScale = 1f;
    }
}