using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Collider2D shootCollider;

    [Header("ーー シュート設定（基本） ーー")]
    [SerializeField] private float shootForce = 15f;
    [SerializeField, Range(0f, 45f)] private float neutralShootAngle = 5f;
    [SerializeField] private float kickCooldownTime = 0.2f;

    [Header("ーー スマッシュキック設定 ーー")]
    [SerializeField] private float smashShootForceMultiplier = 1.5f; // 通常シュートの1.5倍の威力

    // 🔥 【進化】地上での上下制限をそれぞれ独立
    [Header("ーー シュート設定（地上での角度制限） ーー")]
    [SerializeField, Range(0f, 90f), Tooltip("地上で【上】に狙える最大角度（90°で真上）")]
    private float groundMaxUpAngle = 45f;

    [SerializeField, Range(0f, 90f), Tooltip("地上で【下】に狙える最大角度（90°で真下）")]
    private float groundMaxDownAngle = 15f; // 地上は少し浅めにする等の調整が可能

    // 🔥 【進化】空中での上下制限をそれぞれ独立
    [Header("ーー シュート設定（空中での角度制限） ーー")]
    [SerializeField, Range(0f, 90f), Tooltip("空中で【上】に狙える最大角度（90°で真上）")]
    private float airMaxUpAngle = 45f;

    [SerializeField, Range(0f, 90f), Tooltip("空中で【下】に狙える最大角度（90°で真下）")]
    private float airMaxDownAngle = 90f; // 空中なら真下（90°）まで叩き込める！

    [Header("ーー 入力バッファ設定 ーー")]
    [SerializeField] private float inputBufferTime = 0.1f;

    [Header("ーー 参照 ーー")]
    [SerializeField] private InputActionReference directionInput;

    [Header("ーー デバッグ設定 ーー")]
    [SerializeField] private bool showDebugGizmos = true; // ギズモの表示・非表示スイッチ
    [SerializeField] private float gizmoLineLength = 2.0f; // デバッグ線の長さ

    private MobStatus _status;
    private PlayerController _playerController; // 接地状態（isGrounded）を借りるために参照を追加

    private Vector2 _lastValidInputDirection = Vector2.zero;
    private float _lastInputTime = -999f;
    private Vector3 _currentPredictedDirection = Vector3.right; // 🧠 現在予測されるシュート方向（Gizmo用）

    /// <summary>
    /// 🎯 現在狙っている方向の角度（正面: 0, 真上: 90, 真下: -90）を取得するプロパティ
    /// </summary>
    public float CurrentShootAngle
    {
        get
        {
            // プレイヤーの現在の正面方向（右向きなら Vector3.right、左向きなら Vector3.left）
            Vector3 forwardDirection = transform.localScale.x > 0 ? Vector3.right : Vector3.left;

            // 正面方向ベクトルと、予測されている弾道ベクトルの「なす角」を計算
            float angle = Vector3.Angle(forwardDirection, _currentPredictedDirection);

            // 弾道が「上向き」か「下向き」かで符号を決定する
            // 弾道の Y 成分がプラスなら上向き（+）、マイナスなら下向き（-）
            return _currentPredictedDirection.y >= 0f ? angle : -angle;
        }
    }


    private bool _hasShotThisAction = false;
    private bool _isSmashKickActive = false; // 今回のキックがスマッシュかどうかを記憶

    private void Awake()
    {
        _status = GetComponent<MobStatus>();
        _playerController = GetComponent<PlayerController>(); // 参照を自動取得
    }

    /// <summary>
    /// 現在のプレイヤーが地上にいるかどうかを安全に判定するプロパティ
    /// </summary>
    private bool IsPlayerGrounded()
    {
        if (_playerController != null)
        {
            return System.Convert.ToBoolean(_playerController.GetType()
                .GetField("isGrounded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_playerController) ?? true);
        }
        return true;
    }

    /// <summary>
    /// 💡 PlayerControllerからスマッシュフラグを受け取るための公開メソッド
    /// </summary>
    public void SetSmashFlag(bool isSmash)
    {
        _isSmashKickActive = isSmash;
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

        // デバッグログで角度を表示
        Debug.Log($"現在狙っている角度: {CurrentShootAngle:F1}°");
    }

    /// <summary>
    /// 現在のレバー状態とバッファから、弾道を予測する内部ロジック
    /// </summary>
    private Vector3 PredictShootDirection()
    {
        float faceDirection = Mathf.Sign(transform.localScale.x);
        bool isDirectionTargeted = (directionInput.action.ReadValue<Vector2>().sqrMagnitude > 0.05f) ||
                                   (Time.time - _lastInputTime <= inputBufferTime);

        // 🔥 現在の状態に応じて上下それぞれの限界角度を動的に切り替える
        float maxUpAngle = IsPlayerGrounded() ? groundMaxUpAngle : airMaxUpAngle;
        float maxDownAngle = IsPlayerGrounded() ? groundMaxDownAngle : airMaxDownAngle;

        if (isDirectionTargeted)
        {
            Vector2 finalInput = _lastValidInputDirection;
            float currentAngleDeg = Mathf.Atan2(finalInput.y, finalInput.x) * Mathf.Rad2Deg;

            if (faceDirection > 0)
            {
                // 右向き時：上方向はプラス（0 〜 maxUpAngle）、下方向はマイナス（-maxDownAngle 〜 0）
                currentAngleDeg = Mathf.Clamp(currentAngleDeg, -maxDownAngle, maxUpAngle);
            }
            else
            {
                // 左向き時：Atan2の返す角度を0〜360°に補正して判定しやすくする
                if (currentAngleDeg < 0) currentAngleDeg += 360f;

                // 左向き時の基準は180°。上方向はマイナス（180 - maxUpAngle）、下方向はプラス（180 + maxDownAngle）
                currentAngleDeg = Mathf.Clamp(currentAngleDeg, 180f - maxUpAngle, 180f + maxDownAngle);
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
        _hasShotThisAction = false;
        shootCollider.enabled = true;
    }

    public void OnShoot(Collider2D collider)
    {
        // すでにこのアクション内で何かを蹴っていたら、2個目以降は絶対に処理しない
        if (_hasShotThisAction) return;

        if (!collider.CompareTag("Ball")) return;

        BallAbility ballInRange = collider.GetComponent<BallAbility>();

        // Updateで常に予測している最新の方向をそのまま採用して発射！
        Vector3 shootDirection = _currentPredictedDirection;

        // 💡 スマッシュなら弾速（Force）をアップさせる（必要なければそのまま shootForce でOKです）
        float finalForce = _isSmashKickActive ? shootForce * smashShootForceMultiplier : shootForce;

        // 💡 ボールのFireメソッドに、現在のスマッシュ状態を渡す！
        ballInRange.Fire(shootDirection, finalForce, _isSmashKickActive);

        // 1個蹴ったので、フラグを立ててロックする！
        _hasShotThisAction = true;

        // ログ出力用
        bool isBuffered = (directionInput.action.ReadValue<Vector2>().sqrMagnitude <= 0.05f) && (Time.time - _lastInputTime <= inputBufferTime);

        if (_isSmashKickActive)
        {
            Debug.Log("💥 スマッシュシュート！！（Ball側で威力・演出処理）");
        }
        else
        {
            Debug.Log(isBuffered ? "🎯 方向記憶シュート！" : "🍃 通常シュート！");
        }
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
        if (!showDebugGizmos || !Application.isPlaying) return;

        bool isTargeted = (directionInput.action.ReadValue<Vector2>().sqrMagnitude > 0.05f) ||
                          (Time.time - _lastInputTime <= inputBufferTime);

        Gizmos.color = isTargeted ? Color.red : Color.green;

        CollisionDetector detector = GetComponentInChildren<CollisionDetector>();
        if (detector == null) return;
        Vector3 startPos = detector.transform.position;

        Vector3 endPos = startPos + _currentPredictedDirection * gizmoLineLength;

        // 1. メインの予測線を引く
        Gizmos.DrawLine(startPos, endPos);
        Gizmos.DrawSphere(endPos, 0.15f);

        // 2. 【進化】非対称な限界角度を取得して可動限界ガイド線を引く
        float maxUpAngle = IsPlayerGrounded() ? groundMaxUpAngle : airMaxUpAngle;
        float maxDownAngle = IsPlayerGrounded() ? groundMaxDownAngle : airMaxDownAngle;
        float faceDir = Mathf.Sign(transform.localScale.x);

        // 現在地上の設定なら「薄い黄色」、空中なら「薄い水色」
        Gizmos.color = IsPlayerGrounded() ? new Color(1f, 1f, 0f, 0.3f) : new Color(0f, 1f, 1f, 0.3f);

        // 上限ライン（MaxUpAngleを適用）
        Vector3 maxUpDir = Quaternion.Euler(0, 0, faceDir > 0 ? maxUpAngle : -maxUpAngle) * (faceDir > 0 ? Vector3.right : Vector3.left);
        Gizmos.DrawLine(startPos, startPos + maxUpDir * gizmoLineLength);

        // 下限ライン（MaxDownAngleを適用）
        Vector3 maxDownDir = Quaternion.Euler(0, 0, faceDir > 0 ? -maxDownAngle : maxDownAngle) * (faceDir > 0 ? Vector3.right : Vector3.left);
        Gizmos.DrawLine(startPos, startPos + maxDownDir * gizmoLineLength);
    }
}