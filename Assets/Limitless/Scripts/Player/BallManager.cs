using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 💡 Input Systemを有効化

public class BallManager : MonoBehaviour
{
    // ⚙️ レゾナンスのレシピをインスペクターでList管理するための構造体
    [System.Serializable]
    public struct ResonanceRecipe
    {
        public string name; // インスペクターで見やすくするためのラベル（例: "Red + Blue = Purple"）

        [Header("素材ボール (順不同)")]
        public BallType materialA;
        public BallType materialB;

        [Header("生成されるボール（Ballアセット）")]
        public Ball resultBall; // 💡 既存のBallクラスをそのまま指定できるようにします

        [Header("優先度設定 (0が最優先)")]
        public int priority;
    }

    [Header("Ball Prefabs")]
    [SerializeField] private List<Ball> balls;

    [Header("ーー レゾナンスシステム設定 ーー")]
    [SerializeField] private InputActionReference resonanceAction; // InputSystemのアクション参照
    [SerializeField] private List<ResonanceRecipe> resonanceList;   // インスペクター用のレシピリスト

    [Header("Ball Rotation Settings")]
    [Tooltip("最大装填数")]
    [SerializeField] private int maxBalls = 5;

    [Tooltip("回転半径")]
    [SerializeField] private float orbitRadius = 2f;

    [Tooltip("回転中心高さオフセット")]
    [SerializeField] private float centerOffsetY = -0.2f;

    [Tooltip("回転速度")]
    [SerializeField] private float rotateSpeed = 100f;

    [Tooltip("リロードクールタイム")]
    [SerializeField] private float reloadWaitingTime = 0.6f;

    [Tooltip("ボールの生成間隔")]
    [SerializeField] private float ballActivateDuration = 0.2f;

    private BallAbility[] activeBalls;
    private float currentAngle;

    public bool isReloading { get; private set; } = false;

    private void OnEnable()
    {
        activeBalls = new BallAbility[maxBalls];

        // 🎮 Input Systemのイベント登録
        if (resonanceAction != null)
        {
            resonanceAction.action.Enable();
            resonanceAction.action.performed += OnResonancePerformed;
        }
    }

    private void OnDisable()
    {
        // 🎮 イベントの解除
        if (resonanceAction != null)
        {
            resonanceAction.action.performed -= OnResonancePerformed;
        }
    }

    private void Update()
    {
        // 残弾チェック：蹴られていないボールが0になったら自動リロード
        if (!isReloading && GetRemainingBallCount() == 0)
        {
            StartCoroutine(OutOfBallsRoutine());
        }

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

    // ================================================================
    // 🔮 レゾナンスシステム（共鳴・合成回路）
    // ================================================================
    private void OnResonancePerformed(InputAction.CallbackContext context)
    {
        if (isReloading || resonanceList == null || resonanceList.Count == 0) return;

        List<ResonanceRecipe> validRecipes = new List<ResonanceRecipe>();
        Dictionary<ResonanceRecipe, Vector2Int> recipeToSlotIndices = new Dictionary<ResonanceRecipe, Vector2Int>();

        // 1. 現在の残弾の中から、発動可能なレシピをすべて洗い出す
        foreach (var recipe in resonanceList)
        {
            if (TryFindMaterials(recipe, out int slotA, out int slotB))
            {
                validRecipes.Add(recipe);
                recipeToSlotIndices[recipe] = new Vector2Int(slotA, slotB);
            }
        }

        if (validRecipes.Count == 0) return;

        // 2. 優先度（Priority）が最も高い（数字が最も低い）ものを抽出
        int highestPriority = int.MaxValue;
        foreach (var recipe in validRecipes)
        {
            if (recipe.priority < highestPriority)
            {
                highestPriority = recipe.priority;
            }
        }

        List<ResonanceRecipe> bestRecipes = validRecipes.FindAll(r => r.priority == highestPriority);

        // 3. 同率優先度があればランダムで1つ選出
        int randomIndex = Random.Range(0, bestRecipes.Count);
        ResonanceRecipe finalRecipe = bestRecipes[randomIndex];

        // 4. 【消費＆生成の実行】
        Vector2Int targetSlots = recipeToSlotIndices[finalRecipe];
        ExecuteResonance(finalRecipe, targetSlots.x, targetSlots.y);
    }

    /// <summary>
    /// 指定されたレシピの素材がactiveBallsの中に存在するかチェックし、そのスロットインデックスを返す
    /// </summary>
    private bool TryFindMaterials(ResonanceRecipe recipe, out int slotA, out int slotB)
    {
        slotA = -1;
        slotB = -1;

        List<int> usedIndices = new List<int>();

        // 素材Aを探す
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

        // 素材Bを探す（素材Aで使ったスロットは除外）
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

    /// <summary>
    /// 素材となった2つのボールを消去し、片方の空いたスロットの位置に新しいボールを生成・装填する
    /// </summary>
    private void ExecuteResonance(ResonanceRecipe recipe, int slotA, int slotB)
    {
        if (recipe.resultBall == null || recipe.resultBall.BallPrefab == null)
        {
            Debug.LogError($"⚠️ レゾナンスレシピ [{recipe.name}] の生成プレハブが空っぽです！");
            return;
        }

        Debug.Log($"🔮 【レゾナンス発動】 {recipe.materialA} + {recipe.materialB} ⇒ {recipe.resultBall.Type} (Priority: {recipe.priority})");

        // 素材のオブジェクトを削除
        Destroy(activeBalls[slotA].gameObject);
        Destroy(activeBalls[slotB].gameObject);

        activeBalls[slotA] = null;
        activeBalls[slotB] = null;

        // 片方のスロット（slotA）の位置を引き継いで新しいボールを生成
        GameObject newBallGo = Instantiate(recipe.resultBall.BallPrefab);
        BallAbility newBall = newBallGo.GetComponent<BallAbility>();

        // コライダーと初期位置の設定
        newBall.GetComponent<Collider2D>().isTrigger = true;
        SetBallPosition(newBall.transform, slotA);

        // 💡 自身の管理用BallTypeを注入
        newBall.ballType = recipe.resultBall.Type;

        // スロットに装填
        activeBalls[slotA] = newBall;
    }

    // ================================================================
    // 🔄 ガチャ＆自動リロードシステム
    // ================================================================
    public Ball GetRandomBallData()
    {
        float rateTotal = 0;
        foreach (Ball ball in balls) rateTotal += ball.Rate;

        float randomValue = Random.Range(0, rateTotal);
        float currentValue = 0;
        foreach (Ball ball in balls)
        {
            currentValue += ball.Rate;
            if (randomValue < currentValue)
            {
                return ball; // 💡 プレハブ単体ではなく、Ballクラスごと返すように変更
            }
        }
        return null;
    }

    public void RefillEmptySlots()
    {
        isReloading = true;
        StartCoroutine(ReloadBalls());
    }

    private IEnumerator ReloadBalls()
    {
        if (isReloading)
        {
            for (int i = 0; i < activeBalls.Length; i++)
            {
                if (activeBalls[i] == null || activeBalls[i].isKicked)
                {
                    yield return new WaitForSeconds(ballActivateDuration);

                    activeBalls[i] = null;

                    Ball generatedBallData = GetRandomBallData();
                    if (generatedBallData != null)
                    {
                        GameObject go = Instantiate(generatedBallData.BallPrefab);
                        BallAbility ball = go.GetComponent<BallAbility>();

                        ball.GetComponent<Collider2D>().isTrigger = true;
                        SetBallPosition(ball.transform, i);

                        // 💡 通常リロード時も、生成されたボールに自身のBallTypeを注入する
                        ball.ballType = generatedBallData.Type;

                        activeBalls[i] = ball;
                    }
                }
            }
            isReloading = false;
        }
    }

    private int GetRemainingBallCount()
    {
        int count = 0;
        foreach (var ball in activeBalls)
        {
            if (ball != null && !ball.isKicked) count++;
        }
        return count;
    }

    private IEnumerator OutOfBallsRoutine()
    {
        isReloading = true;
        Debug.Log("弾切れ！リロード中・・・");

        yield return new WaitForSeconds(reloadWaitingTime);
        StartCoroutine(ReloadBalls());

        Debug.Log("リロード完了");
    }
}