using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Collider2D shootCollider;

    [Header("ーー シュート設定 ーー")]
    [SerializeField] private float shootForce = 15f;
    [SerializeField, Range(0f, 90f)] private float maxShootAngle = 45f;
    [SerializeField, Range(0f, 45f)] private float neutralShootAngle = 5f;
    [SerializeField] private float kickCooldownTime = 0.2f;

    [Header("ーー 入力バッファ設定 ーー")]
    [SerializeField] private float inputBufferTime = 0.1f;

    [Header("ーー 参照 ーー")]
    [SerializeField] private InputActionReference directionInput;

    [Header("ーー デバッグ設定 ーー")]
    [SerializeField] private bool showDebugGizmos = true; // ギズモの表示・非表示スイッチ
    [SerializeField] private float gizmoLineLength = 2.0f; // デバッグ線の長さ

    private MobStatus _status;

    private Vector2 _lastValidInputDirection = Vector2.zero;
    private float _lastInputTime = -999f;
    private Vector3 _currentPredictedDirection = Vector3.right; // 🧠 現在予測されるシュート方向（Gizmo用）

    private void Awake()
    {
        _status = GetComponent<MobStatus>();
    }

    private void Update()
    {
        // 1. 毎フレーム、レバーの入力を監視して記憶する
        Vector2 currentInput = directionInput.action.ReadValue<Vector2>();

        if (currentInput.sqrMagnitude > 0.05f)
        {
            _lastValidInputDirection = currentInput;
            _lastInputTime = Time.time;
        }

        // 🧠 2. 毎フレーム「いまボタンを押したらどっちに飛ぶか」を計算してキャッシュする（Gizmo用）
        _currentPredictedDirection = PredictShootDirection();
    }

    /// <summary>
    /// 現在のレバー状態とバッファから、弾道を予測する内部ロジック
    /// </summary>
    private Vector3 PredictShootDirection()
    {
        float faceDirection = Mathf.Sign(transform.localScale.x);
        bool isDirectionTargeted = (directionInput.action.ReadValue<Vector2>().sqrMagnitude > 0.05f) ||
                                   (Time.time - _lastInputTime <= inputBufferTime);

        if (isDirectionTargeted)
        {
            Vector2 finalInput = _lastValidInputDirection;
            float currentAngleDeg = Mathf.Atan2(finalInput.y, finalInput.x) * Mathf.Rad2Deg;

            if (faceDirection > 0)
            {
                currentAngleDeg = Mathf.Clamp(currentAngleDeg, -maxShootAngle, maxShootAngle);
            }
            else
            {
                if (currentAngleDeg < 0) currentAngleDeg += 360f;
                currentAngleDeg = Mathf.Clamp(currentAngleDeg, 180f - maxShootAngle, 180f + maxShootAngle);
            }

            return new Vector3(Mathf.Cos(currentAngleDeg * Mathf.Deg2Rad), Mathf.Sin(currentAngleDeg * Mathf.Deg2Rad), 0f);
        }
        else
        {
            float targetAngleDeg = faceDirection > 0 ? neutralShootAngle : 180f - neutralShootAngle;
            return new Vector3(Mathf.Cos(targetAngleDeg * Mathf.Deg2Rad), Mathf.Sin(targetAngleDeg * Mathf.Deg2Rad), 0f);
        }
    }

    public void OnShootStart()
    {
        shootCollider.enabled = true;
    }

    public void OnShoot(Collider2D collider)
    {
        if (!collider.CompareTag("Ball")) return;

        BallAbility ballInRange = collider.GetComponent<BallAbility>();

        // 🔄 Updateで常に予測している最新の方向をそのまま採用して発射！
        Vector3 shootDirection = _currentPredictedDirection;

        ballInRange.Fire(shootDirection, shootForce);

        // ログ出力用
        bool isBuffered = (directionInput.action.ReadValue<Vector2>().sqrMagnitude <= 0.05f) && (Time.time - _lastInputTime <= inputBufferTime);
        Debug.Log(isBuffered ? "🎯 方向記憶シュート！" : "🍃 通常シュート！");
    }

    public void OnShootFinished()
    {
        shootCollider.enabled = false;
        StartCoroutine(CooldownCoroutine());
    }

    private IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSeconds(kickCooldownTime);
        _status.GoToNormalStateIfPossible();
    }

    // ================================================================
    // 🎨 視覚的デバッグ：Gizmo描画回路
    // ================================================================
    private void OnDrawGizmos()
    {
        // スイッチがオフ、またはアプリケーションが動いていない時は描画しない
        if (!showDebugGizmos || !Application.isPlaying) return;

        // レバーを入れている（または猶予時間中）なら「赤」、完全ニュートラルなら「緑」に色を変える
        bool isTargeted = (directionInput.action.ReadValue<Vector2>().sqrMagnitude > 0.05f) ||
                          (Time.time - _lastInputTime <= inputBufferTime);

        Gizmos.color = isTargeted ? Color.red : Color.green;

        // プレイヤーの中心位置
        Vector3 startPos = GetComponentInChildren<CollisionDetector>().transform.position;
        // 予測される方向へ線を伸ばした先の座標
        Vector3 endPos = startPos + _currentPredictedDirection * gizmoLineLength;

        // 1. メインの予測線を引く
        Gizmos.DrawLine(startPos, endPos);

        // 2. 先端に小さな球体を描画して、矢印の頭のように見せる
        Gizmos.DrawSphere(endPos, 0.15f);

        // 3. 【おまけ】狙える最大上下角度（限界突破ライン）を薄い黄色でうっすら表示
        float faceDir = Mathf.Sign(transform.localScale.x);
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // 透明度30%の黄色

        // 上限ライン
        Vector3 maxUpDir = Quaternion.Euler(0, 0, faceDir > 0 ? maxShootAngle : -maxShootAngle) * (faceDir > 0 ? Vector3.right : Vector3.left);
        Gizmos.DrawLine(startPos, startPos + maxUpDir * gizmoLineLength);

        // 下限ライン
        Vector3 maxDownDir = Quaternion.Euler(0, 0, faceDir > 0 ? -maxShootAngle : maxShootAngle) * (faceDir > 0 ? Vector3.right : Vector3.left);
        Gizmos.DrawLine(startPos, startPos + maxDownDir * gizmoLineLength);
    }
}