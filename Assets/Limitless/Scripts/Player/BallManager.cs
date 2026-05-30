using Cysharp.Threading.Tasks; // 💡 UniTaskを有効化
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BallManager : MonoBehaviour
{
    [System.Serializable]
    public struct ResonanceRecipe
    {
        public string name;

        [Header("素材ボール (順不同)")]
        public BallType materialA;
        public BallType materialB;

        [Header("生成されるボール（Ballアセット）")]
        public Ball resultBall;

        [Header("優先度設定 (0が最優先)")]
        public int priority;

        [Header("演出設定")]
        public bool hasProduction; // 💡 演出を再生するかどうか
        public GameObject backgroundEffectPrefab; // 💡 背後に表示するエフェクト
    }

    [Header("Ball Prefabs")]
    [SerializeField] private List<Ball> balls;

    [Header("ーー レゾナンスシステム設定 ーー")]
    [SerializeField] private InputActionReference resonanceAction;
    [SerializeField] private List<ResonanceRecipe> resonanceList;

    [Header("Ball Rotation Settings")]
    [SerializeField] private int maxBalls = 5;
    [SerializeField] private float orbitRadius = 2f;
    [SerializeField] private float centerOffsetY = -0.2f;
    [SerializeField] private float rotateSpeed = 100f;
    [SerializeField] private float reloadWaitingTime = 0.6f;
    [SerializeField] private float ballActivateDuration = 0.2f;

    private BallAbility[] activeBalls;
    private float currentAngle;

    // 👤 プレイヤー制限用のキャッシュ
    private Rigidbody2D _playerRb;
    private PlayerStatus _playerStatus;
    private Animator _playerAnimator;

    // Background Effectのキャッシュ
    private GameObject _backEffect;

    public bool isReloading { get; private set; } = false;
    private bool isProducing = false; // 演出中の連続暴発ガード

    private void Awake()
    {
        // 自身にアタッチされているコンポーネントを自動キャッシュ
        _playerRb = GetComponent<Rigidbody2D>();
        _playerStatus = GetComponent<PlayerStatus>();
        _playerAnimator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        activeBalls = new BallAbility[maxBalls];
        if (resonanceAction != null)
        {
            resonanceAction.action.Enable();
            resonanceAction.action.performed += OnResonancePerformed;
        }
    }

    private void OnDisable()
    {
        if (resonanceAction != null)
        {
            resonanceAction.action.performed -= OnResonancePerformed;
        }
    }

    private void Update()
    {
        if (!isReloading && GetRemainingBallCount() == 0)
        {
            StartCoroutine(OutOfBallsRoutine());
        }

        // 💡 演出停止中（Time.timeScaleが低速/0の時）はボールの回転も止まってほしいので Time.deltaTime のままにします
        currentAngle -= rotateSpeed * Time.deltaTime;
        UpdateBallPositions();
    }

    private void UpdateBallPositions()
    {
        for (int i = 0; i < activeBalls.Length; i++)
        {
            if (activeBalls[i] == null || activeBalls[i].isKicked) continue;
            SetBallPosition(activeBalls[i].transform, i);
        }
    }

    private void SetBallPosition(Transform ballTransform, int index)
    {
        float angle = currentAngle + (index * 360f / maxBalls);
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orbitRadius;
        ballTransform.transform.position = transform.position + offset + new Vector3(0, centerOffsetY, 0);
    }

    private void OnResonancePerformed(InputAction.CallbackContext context)
    {

        // 👤 【追加の防壁】プレイヤーが動ける状態（IsMovable == true）のときだけ発動可能にする
        // ※プロパティ名や参照方法（メソッド型など）が異なる場合は、実際の PlayerStatus の定義に合わせてください。
        if (_playerStatus != null && !_playerStatus.IsMovable) return;

        if (isReloading || isProducing || resonanceList == null || resonanceList.Count == 0) return;

        List<ResonanceRecipe> validRecipes = new List<ResonanceRecipe>();
        Dictionary<ResonanceRecipe, Vector2Int> recipeToSlotIndices = new Dictionary<ResonanceRecipe, Vector2Int>();

        foreach (var recipe in resonanceList)
        {
            if (TryFindMaterials(recipe, out int slotA, out int slotB))
            {
                validRecipes.Add(recipe);
                recipeToSlotIndices[recipe] = new Vector2Int(slotA, slotB);
            }
        }

        if (validRecipes.Count == 0) return;

        int highestPriority = int.MaxValue;
        foreach (var recipe in validRecipes)
        {
            if (recipe.priority < highestPriority) highestPriority = recipe.priority;
        }

        List<ResonanceRecipe> bestRecipes = validRecipes.FindAll(r => r.priority == highestPriority);
        int randomIndex = Random.Range(0, bestRecipes.Count);
        ResonanceRecipe finalRecipe = bestRecipes[randomIndex];

        Vector2Int targetSlots = recipeToSlotIndices[finalRecipe];

        ExecuteResonanceProcess(finalRecipe, targetSlots.x, targetSlots.y).Forget();
    }

    /// <summary>
    /// 🔮 素材消去 → プレイヤー拘束＆演出 → FxManagerの時間停止終了を待って新ボール生成
    /// </summary>
    private async UniTaskVoid ExecuteResonanceProcess(ResonanceRecipe recipe, int slotA, int slotB)
    {
        if (recipe.resultBall == null || recipe.resultBall.BallPrefab == null) return;

        isProducing = true;
        Debug.Log($"🔮 【レゾナンスシステム起動】: {recipe.name}");

        // 1. 素材のオブジェクトを即座に削除
        Destroy(activeBalls[slotA].gameObject);
        Destroy(activeBalls[slotB].gameObject);
        activeBalls[slotA] = null;
        activeBalls[slotB] = null;

        // 2. 演出ありの設定ならプレイヤー拘束＆時間停止
        if (recipe.hasProduction)
        {
            // 👤 プレイヤーの行動・物理制限を適用
            ApplyPlayerRestrictions();

            // ⏱️ FxManagerで画面全体をフリーズ（Time.timeScaleを変更するシステム）
            FxManager.Instance.Play("Resonance", transform);

            // 🎬 背後の特殊エフェクトを生成
            if (recipe.backgroundEffectPrefab != null)
            {
                _backEffect = Instantiate(recipe.backgroundEffectPrefab, transform.position, Quaternion.identity);
                _backEffect.transform.SetParent(this.transform);
            }

            // ⏳ 【最重要リファクタリング】
            // FxManagerの非同期処理が終わり、世界の時間軸（Time.timeScale）が通常（1.0f以上）に戻るまで待機する！
            // これにより、手動の秒数指定ではなくFxManagerのアセット設定と完全に自動同期します。
            await UniTask.WaitUntil(() => Time.timeScale >= 1.0f, PlayerLoopTiming.Update);



            // 👤 プレイヤーの行動制限を解除して通常状態へ戻す
            ReleasePlayerRestrictions();

            Destroy(_backEffect);
        }

        // 3. 演出が明けたら、Result Ball を slotA の位置に生成
        GameObject newBallGo = Instantiate(recipe.resultBall.BallPrefab);
        BallAbility newBall = newBallGo.GetComponent<BallAbility>();

        newBall.GetComponent<Collider2D>().isTrigger = true;
        SetBallPosition(newBall.transform, slotA);
        newBall.ballType = recipe.resultBall.Type;

        activeBalls[slotA] = newBall;

        isProducing = false;
    }

    /// <summary>
    /// 👤 プレイヤーをレゾナンス演出用に固定・無敵・アニメーションUnscaled化
    /// </summary>
    private void ApplyPlayerRestrictions()
    {
        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector2.zero;
            _playerRb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (_playerStatus != null)
        {
            _playerStatus.SetInvicible(); // 無敵化
            _playerStatus.SetIntroState(); // 操作受付停止（Intro状態）
        }

        if (_playerAnimator != null)
        {
            _playerAnimator.SetTrigger("Resonance");
            _playerAnimator.SetBool("IsOnResonance", true);

            _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime; // 👈 時間停止中もアニメを等速で動かす
            _playerAnimator.Update(0f); // 設定変更を即時リフレッシュ
        }

        Debug.Log("👤 [Resonance] プレイヤーを演出状態にロックしました（UnscaledTimeアニメーション）。");
    }

    /// <summary>
    /// 👤 プレイヤーの各種ロック状態を解除し、通常状態に引き戻す
    /// </summary>
    private void ReleasePlayerRestrictions()
    {
        if (_playerRb != null)
        {
            _playerRb.bodyType = RigidbodyType2D.Dynamic; // 物理挙動を通常に戻す
        }

        if (_playerStatus != null)
        {
            _playerStatus.CancelInvicible(); // 無敵解除
            _playerStatus.GoToNormalStateIfPossible(); // 通常状態（操作受付）へ復帰
        }

        if (_playerAnimator != null)
        {
            _playerAnimator.SetBool("IsOnResonance", false);
            _playerAnimator.updateMode = AnimatorUpdateMode.Normal; // 通常の時間軸に戻す
        }

        Debug.Log("👤 [Resonance] プレイヤーのロックを解除しました。");
    }

    private bool TryFindMaterials(ResonanceRecipe recipe, out int slotA, out int slotB)
    {
        slotA = -1; slotB = -1;
        List<int> usedIndices = new List<int>();

        for (int i = 0; i < activeBalls.Length; i++)
        {
            if (activeBalls[i] == null || activeBalls[i].isKicked) continue;
            if (activeBalls[i].ballType == recipe.materialA)
            {
                slotA = i;
                usedIndices.Add(i);
                break;
            }
        }
        for (int i = 0; i < activeBalls.Length; i++)
        {
            if (activeBalls[i] == null || activeBalls[i].isKicked || usedIndices.Contains(i)) continue;
            if (activeBalls[i].ballType == recipe.materialB)
            {
                slotB = i;
                break;
            }
        }
        return (slotA != -1 && slotB != -1);
    }

    // --- 以下リロード処理（変更なし） ---
    public Ball GetRandomBallData()
    {
        float rateTotal = 0;
        foreach (Ball ball in balls) rateTotal += ball.Rate;
        float randomValue = Random.Range(0, rateTotal);
        float currentValue = 0;
        foreach (Ball ball in balls)
        {
            currentValue += ball.Rate;
            if (randomValue < currentValue) return ball;
        }
        return null;
    }

    public void RefillEmptySlots() => StartCoroutine(ReloadBalls());

    private IEnumerator ReloadBalls()
    {
        isReloading = true;
        for (int i = 0; i < activeBalls.Length; i++)
        {
            if (activeBalls[i] == null || activeBalls[i].isKicked)
            {
                yield return new WaitForSeconds(ballActivateDuration);
                Ball generatedBallData = GetRandomBallData();
                if (generatedBallData != null)
                {
                    GameObject go = Instantiate(generatedBallData.BallPrefab);
                    BallAbility ball = go.GetComponent<BallAbility>();
                    ball.GetComponent<Collider2D>().isTrigger = true;
                    SetBallPosition(ball.transform, i);
                    ball.ballType = generatedBallData.Type;
                    activeBalls[i] = ball;
                }
            }
        }
        isReloading = false;
    }

    private int GetRemainingBallCount()
    {
        int count = 0;
        foreach (var ball in activeBalls) if (ball != null && !ball.isKicked) count++;
        return count;
    }

    private IEnumerator OutOfBallsRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadWaitingTime);
        yield return StartCoroutine(ReloadBalls());
    }
}