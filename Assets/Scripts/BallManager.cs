using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private int maxBalls = 5;
    [SerializeField] private float orbitRadius = 2f;
    [SerializeField] private float rotateSpeed = 100f;
    [SerializeField] private float reloadTime = 2.0f; // 補充にかかる時間

    private List<BallController> activeBalls = new List<BallController>();
    private float currentAngle;
    private bool isReloading;

    private void Start()
    {
        GenerateBalls();
    }

    private void Update()
    {
        // 残弾チェック：蹴られていないボールが0になったら自動リロード
        if (!isReloading && GetRemainingBallCount() == 0)
        {
            StartCoroutine(ReloadRoutine());
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

    private void GenerateBalls()
    {
        // 既存のリストをクリア（飛んでいったボールは別途Destroyされる想定）
        activeBalls.Clear();

        for (int i = 0; i < maxBalls; i++)
        {
            GameObject go = Instantiate(ballPrefab);
            BallController ball = go.GetComponent<BallController>();

            // ColliderのisTriggerを一旦ON
            ball.GetComponent<Collider2D>().isTrigger = true;

            // 色のランダム決定
            ball.ballColor = (BallColor)Random.Range(0, 3);

            // 色に合わせて見た目を変える処理を呼ぶ
            ball.ApplyColorVisual();

            activeBalls.Add(ball);
        }
    }

    public void RefillEmptySlots()
    {
        // 1. まず、すでに蹴られたボール（isKicked）や、Destroyされたボールをリストから除外
        activeBalls.RemoveAll(ball => ball == null || ball.isKicked);

        // 2. 最大数に足りない分だけ補充
        int refillCount = maxBalls - activeBalls.Count;

        if (refillCount <= 0) return; // 満タンなら何もしない

        Debug.Log($"{refillCount}個のボールを補充します。");

        for (int i = 0; i < refillCount; i++)
        {
            GameObject go = Instantiate(ballPrefab);
            BallController ball = go.GetComponent<BallController>();

            // ColliderのisTriggerを一旦ON
            ball.GetComponent<Collider2D>().isTrigger = true;

            // 色のランダム決定
            ball.ballColor = (BallColor)Random.Range(0, 3);

            // 色に合わせて見た目を変える処理を呼ぶ
            ball.ApplyColorVisual();

            activeBalls.Add(ball);
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

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        Debug.Log("弾切れ！リロード中・・・");

        // 指定秒数待機（リロード時間）
        yield return new WaitForSeconds(reloadTime);

        // 古いボール（飛んでいった残骸）を整理して再生成
        GenerateBalls();

        isReloading = false;
        Debug.Log("リロード完了");
    }
}
