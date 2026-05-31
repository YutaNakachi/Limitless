using UnityEngine;

public class BatAI : MonoBehaviour
{
    [Header("ーー 通常移動設定 ーー")]
    [SerializeField] private float chaseSpeed = 3.5f;   // 追跡時の移動速度
    [SerializeField] private LayerMask groundLayer;          // 地面や壁のレイヤー

    [Header("ーー 索敵・攻撃範囲設定 ーー")]
    [SerializeField] private float targetDetectionRadius = 6f; // プレイヤーを察知する距離（広い円）
    [SerializeField] private float attackRadius = 2.5f;        // 突進をトリガーする距離
    [SerializeField] private LayerMask playerLayer;            // プレイヤーのレイヤー

    [Header("ーー 突進（ダッシュ攻撃）設定 ーー")]
    [SerializeField] private float chargeSpeed = 8.0f;       // 突進時の圧倒的な速度
    [SerializeField] private float maxChargeDuration = 0.6f;  // 突進が強制終了するまでの最大時間（秒）
    [SerializeField] private float chargePreDelay = 0.3f;    // 突進前の「ため時間」（秒）

    // 🔥 【新設】プレイヤーを狙う際のY軸Offset（足元ではなく胸元などを狙わせる補正）
    [SerializeField] private float offsetDirectionY = 1.3f;

    [Header("ーー アニメーション設定 (任意) ーー")]
    [SerializeField] private Animator animator;               // 敵のAnimator
    [SerializeField] private string moveBoolName = "Run";     // 移動中にtrueにするBool名

    [Header("ーーデモモードーー")]
    [SerializeField] private bool demoMode = false;

    private Rigidbody2D _rigidbody;
    private Transform _targetPlayer;
    private MobStatus _status;

    // 突進・攻撃制御用の内部変数
    private bool _isAttacking = false;
    private bool _isCharging = false;
    private bool _isPreDelaying = false;

    private Vector2 _chargeDirection;
    private float _chargeTimer = 0f;
    private float _preDelayTimer = 0f;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _status = GetComponent<MobStatus>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// 💡 【便利関数】Offsetを加味したプレイヤーの「本当の狙い目座標」を返す
    /// </summary>
    private Vector2 GetTargetPositionWithOffset()
    {
        if (_targetPlayer == null) return Vector2.zero;

        // プレイヤーの足元座標に、指定されたY軸Offsetを足し算した位置を「標的」とする
        return (Vector2)_targetPlayer.position + new Vector2(0f, offsetDirectionY);
    }

    private void FixedUpdate()
    {
        // 🔥 【大修正】「死亡中」または「ノックバック中」の時だけ即リターンして物理に任せる！
        if (_status.IsDead || _status.IsKnockbacking || demoMode) return;

        // 🔄 1. インターロック解除：MobStatusがNormalに戻ったら、AIの全フラグをリセット
        if (_status.IsAttackable && _isAttacking)
        {
            _isAttacking = false;
            _isCharging = false;
            _isPreDelaying = false;
            Debug.Log("🔄 [EnemyAI] 攻撃・ため・突進フラグを完全リセットしました。");
        }

        // 🔄 2. ため時間（予備動作）中の優先ルート
        if (_isPreDelaying)
        {
            ExecutePreDelay();
            return;
        }

        // 🔄 3. 突進中の超優先ルート
        if (_isCharging)
        {
            ExecuteCharge();
            return;
        }

        // 4. 周辺にプレイヤーがいるか索敵
        SearchForPlayer();

        // 💡 【スライディング防止ブレーキ】
        if (!_status.IsMovable)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            UpdateAnimationParams(Vector2.zero);
            return;
        }

        Vector2 finalMovement = Vector2.zero;

        // プレイヤーを発見している場合
        if (_targetPlayer != null)
        {
            // 🔥 修正点：距離計算も「Offsetを加味した中心点」同士で行う
            Vector2 targetPos = GetTargetPositionWithOffset();
            float distanceToPlayer = Vector2.Distance(transform.position, targetPos);

            // 🔥 A. 攻撃範囲に入った場合 ➔ ため開始
            if (distanceToPlayer <= attackRadius)
            {
                finalMovement = Vector2.zero;
                StartAttackSequence();
            }
            // 🔥 B. 追跡範囲に入っているが、まだ遠い場合（通常の追いかけ移動）
            else
            {
                // 🔥 修正点：移動方向ベクトルもOffset座標に向けて計算
                Vector2 directionToPlayer = (targetPos - (Vector2)transform.position).normalized;
                finalMovement = directionToPlayer * chaseSpeed;

                UpdateFacingDirection(directionToPlayer.x);
            }
        }
        // プレイヤーが範囲外の場合
        else
        {
            finalMovement = Vector2.zero;
        }

        _rigidbody.linearVelocity = finalMovement;
        UpdateAnimationParams(finalMovement);
    }

    /// <summary>
    /// 最初の攻撃シーケンス（構え・ため）を開始する
    /// </summary>
    private void StartAttackSequence()
    {
        if (_isAttacking) return;

        _isAttacking = true;
        _isPreDelaying = true;
        _preDelayTimer = 0f;

        // 🔥 修正点：Offsetを加味した位置に向けて向きを合わせる
        Vector2 targetPos = GetTargetPositionWithOffset();
        Vector2 dirToPlayer = (targetPos - (Vector2)transform.position).normalized;
        UpdateFacingDirection(dirToPlayer.x);

        _status.GoToAttackStateIfPossible();

        Debug.Log("⏳ 敵が構えた！ ため時間（予備動作）スタート...");
    }

    /// <summary>
    /// ため時間中の毎フレームの処理
    /// </summary>
    private void ExecutePreDelay()
    {
        _preDelayTimer += Time.fixedDeltaTime;

        if (_targetPlayer != null)
        {
            // 🔥 修正点：ため中の視線追従もOffset位置を狙う
            Vector2 targetPos = GetTargetPositionWithOffset();
            Vector2 dirToPlayer = (targetPos - (Vector2)transform.position).normalized;
            UpdateFacingDirection(dirToPlayer.x);
        }

        _rigidbody.linearVelocity = Vector2.zero;
        UpdateAnimationParams(Vector2.zero);

        if (_preDelayTimer >= chargePreDelay)
        {
            _isPreDelaying = false;
            StartChargePhysics();
        }
    }

    /// <summary>
    /// ため時間が終わり、物理的な突進（加速）を開始する瞬間
    /// </summary>
    private void StartChargePhysics()
    {
        _isCharging = true;
        _chargeTimer = 0f;

        if (_targetPlayer != null)
        {
            // 🔥 修正点：ためが終わった瞬間の「Offsetを加味した最新のプレイヤー座標」を強烈ロックオン！
            Vector2 targetPos = GetTargetPositionWithOffset();
            _chargeDirection = (targetPos - (Vector2)transform.position).normalized;
        }
        else
        {
            _chargeDirection = new Vector2(Mathf.Sign(transform.localScale.x), 0f);
        }

        UpdateFacingDirection(_chargeDirection.x);
        Debug.Log($"⚡ 狙い確定（高さ補正あり）！ 突進開始！ 方向: {_chargeDirection}");
    }

    /// <summary>
    /// 突進運動の実効ロジック
    /// </summary>
    private void ExecuteCharge()
    {
        _chargeTimer += Time.fixedDeltaTime;

        // 🚧 壁衝突センサー（スライドを加味した速度を受け取る）
        Vector2 finalVelocity = _chargeDirection * chargeSpeed;

        // ⏳ タイムアウトチェック
        if (_chargeTimer >= maxChargeDuration)
        {
            StopChargingPhysics();
            return;
        }

        _rigidbody.linearVelocity = finalVelocity;
        UpdateAnimationParams(finalVelocity);
    }

    private void StopChargingPhysics()
    {
        _isCharging = false;
        _rigidbody.linearVelocity = Vector2.zero;
        UpdateAnimationParams(Vector2.zero);
    }

    private void SearchForPlayer()
    {
        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, targetDetectionRadius, playerLayer);
        _targetPlayer = playerCollider != null ? playerCollider.transform : null;
    }

    private void UpdateFacingDirection(float horizontalMovement)
    {
        if (Mathf.Abs(horizontalMovement) > 0.05f)
        {
            float direction = Mathf.Sign(horizontalMovement);
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction, transform.localScale.y, transform.localScale.z);
        }
    }

    private void UpdateAnimationParams(Vector2 currentVelocity)
    {
        if (animator != null)
        {
            bool isMoving = currentVelocity.sqrMagnitude > 0.01f;
            animator.SetBool(moveBoolName, isMoving);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, targetDetectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}