using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour
{
    [Header("Ball Prefabs")]
    [SerializeField] private List<Ball> balls;

    [Header("Ball Rotation Settings")]

    [Tooltip("最大装填数")]
    [SerializeField] private int maxBalls = 5;

    [Tooltip("回転半径")]
    [SerializeField] private float orbitRadius = 2f;

    [Tooltip("回転速度")]
    [SerializeField] private float rotateSpeed = 100f;

    [Tooltip("リロードクールタイム")]
    [SerializeField] private float reloadTime = 2.0f;


    private List<BallController> activeBalls = new List<BallController>();
    private float currentAngle;
    public bool isReloading { get; private set; } = false;

    private void Start()
    {
        //GenerateBalls();
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
        for (int i = 0; i < activeBalls.Count; i++)
        {
            if (activeBalls[i] == null || activeBalls[i].isKicked) continue;

            // 各ボールの角度を均等に割り振る（360度 / ボール数）
            float angle = currentAngle + (i * 360f / maxBalls);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orbitRadius;

            activeBalls[i].Orbit(transform.position + offset + new Vector3(0, GetComponent<CapsuleCollider2D>().size.y / 2 - 0.2f, 0));
        }
    }

    public GameObject GetRandomBall()
    {
        // 全体の重みの合計を出す
        float rateTotal = 0;
        foreach (Ball ball in balls) rateTotal += ball.Rate;

        // 0 ～ 合計値 の間でランダムな数字を決める
        float randomValue = Random.Range(0, rateTotal);

        // どの重みの範囲に入っているかチェック
        float currentValue = 0;
        foreach (Ball ball in balls)
        {
            currentValue += ball.Rate;
            if (randomValue < currentValue)
            {
                return ball.BallPrefab; // 当たったボールを返す
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
        // すでに蹴られたボール（isKicked）や、Destroyされたボールをリストから除外
        activeBalls.RemoveAll(ball => ball == null || ball.isKicked);

        // 最大数に足りない分だけ補充
        int refillCount = maxBalls - activeBalls.Count;

        if (refillCount <= 0)
        {
            isReloading = false;
            yield break; // 満タンなら何もしない
        }

        Debug.Log($"{refillCount}個のボールを補充します。");

        if (isReloading)
        {
            for (int i = 0; i < refillCount; i++)
            {
                yield return new WaitForSeconds(0.5f);

                GameObject generatedBall = GetRandomBall();

                GameObject go = Instantiate(generatedBall);
                BallController ball = go.GetComponent<BallController>();

                // ColliderのisTriggerを一旦ON
                ball.GetComponent<Collider2D>().isTrigger = true;

                activeBalls.Add(ball);

                // 生成時のSEやエフェクトをここで鳴らすと最高に気持ちいいです
                // AudioSource.PlayClipAtPoint(reloadSound, transform.position);
            }
        }

        isReloading = false;
    }

    private int GetRemainingBallCount()
    {
        // まだ蹴られていない（isKickedがfalse）のボールを数える
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

        // 指定秒数待機（リロード時間）
        yield return new WaitForSeconds(reloadTime);

        // 再生成
        StartCoroutine(ReloadBalls());

        Debug.Log("リロード完了");
    }
}
