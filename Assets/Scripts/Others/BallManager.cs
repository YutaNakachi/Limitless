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


    //private List<BallController> activeBalls = new List<BallController>();
    private BallController[] activeBalls;

    private float currentAngle;

    public bool isReloading { get; private set; } = false;

    private void OnEnable()
    {
        activeBalls = new BallController[maxBalls];
    }

    private void Update()
    {
        // 残弾チェック：蹴られていないボールが0になったら自動リロード
        if (!isReloading && GetRemainingBallCount() == 0)
        {
            StartCoroutine(OutOfBallsRoutine());
        }

        // Mathf.Sign は 正なら 1、負なら -1 を返す
        currentAngle -= Mathf.Sign(transform.localScale.x) * rotateSpeed * Time.deltaTime;

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
        // 各ボールの角度を均等に割り振る（360度 / ボール数）
        float angle = currentAngle + (index * 360f / maxBalls);
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orbitRadius;

        ballTransform.transform.position = transform.position + offset + new Vector3(0, GetComponent<CapsuleCollider2D>().size.y / 2 - 0.2f, 0);
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
        if (isReloading)
        {
            for (int i = 0; i < activeBalls.Length; i++)
            {
                // 枠が空（null）、または中身がすでに蹴られている場合
                if (activeBalls[i] == null || activeBalls[i].isKicked)
                {
                    yield return new WaitForSeconds(0.5f);

                    // 飛んでいった古いボールが残っている場合は念のため参照を切る
                    activeBalls[i] = null;

                    GameObject generatedBall = GetRandomBall();

                    // 新しいボールを生成
                    GameObject go = Instantiate(generatedBall);
                    BallController ball = go.GetComponent<BallController>();

                    // ColliderのisTriggerを一旦ON
                    ball.GetComponent<Collider2D>().isTrigger = true;

                    SetBallPosition(ball.transform, i);

                    // リストの「i番目」に上書き代入
                    activeBalls[i] = ball;

                    // 生成時のSEやエフェクトをここで鳴らす
                    // AudioSource.PlayClipAtPoint(reloadSound, transform.position);
                }
            }
            isReloading = false;
        }
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
