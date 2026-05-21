using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("ーー 移動設定 ーー")]
    [SerializeField] private float chaseSpeed = 3.5f;   // 追跡時の移動速度
    [SerializeField] private float wallCheckDistance = 0.5f; // 壁センサーの長さ
    [SerializeField] private LayerMask groundLayer;          // 地面や壁のレイヤー

    [Header("ーー 索敵・攻撃範囲設定 ーー")]
    [SerializeField] private float targetDetectionRadius = 6f; // プレイヤーを察知して追尾する距離（広い円）
    [SerializeField] private float attackRadius = 1.5f;          // 攻撃モーションに移行する距離（狭い円）
    [SerializeField] private LayerMask playerLayer;            // プレイヤーのレイヤー

    [Header("ーー アニメーション設定 (任意) ーー")]
    [SerializeField] private Animator animator;               // 敵のAnimator
    [SerializeField] private string attackTriggerName = "Attack"; // 攻撃時に呼ぶTrigger名
    [SerializeField] private string moveBoolName = "Run";     // 移動中にtrueにするBool名

    private Rigidbody2D _rigidbody;
    private Transform _targetPlayer; // ロックオンしたプレイヤー
    private bool _isAttacking = false; // 攻撃モーション中かどうかのフラグ

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void FixedUpdate()
    {
        // 1. 周辺にプレイヤーがいるか索敵
        SearchForPlayer();

        Vector2 finalMovement = Vector2.zero;

        // プレイヤーを発見している場合
        if (_targetPlayer != null)
        {
            // プレイヤーとの距離を計算
            float distanceToPlayer = Vector2.Distance(transform.position, _targetPlayer.position);

            // 🔥 A. 攻撃範囲（超近距離）に入った場合
            if (distanceToPlayer <= attackRadius)
            {
                // 速度をゼロにしてその場に立ち止まる
                finalMovement = Vector2.zero;

                // 向きだけは常にプレイヤーの方に合わせる
                Vector2 dir = (_targetPlayer.position - transform.position).normalized;
                UpdateFacingDirection(dir.x);

                // 攻撃モーションを呼び出す
                TriggerAttack();
            }
            // 🔥 B. 追跡範囲に入っているが、攻撃範囲外の場合（追尾移動）
            else
            {
                _isAttacking = false; // 攻撃範囲から出たらフラグを下げる

                // プレイヤーへ向かう直線ベクトル
                Vector2 directionToPlayer = (_targetPlayer.position - transform.position).normalized;
                finalMovement = directionToPlayer * chaseSpeed;

                // 向きを移動方向に合わせる
                UpdateFacingDirection(directionToPlayer.x);

                // 壁・地面へのめり込み防止センサー（インターロック回路）
                finalMovement = CheckWallCollision(finalMovement);
            }
        }
        // プレイヤーが範囲外の場合（待機状態）
        else
        {
            _isAttacking = false;
            finalMovement = Vector2.zero; // その場に静止
        }

        // 物理演算として速度を適用
        _rigidbody.linearVelocity = finalMovement;

        // アニメーション用のパラメータ更新
        UpdateAnimationParams(finalMovement);
    }

    /// <summary>
    /// 円の範囲内にプレイヤーがいるか検知する
    /// </summary>
    private void SearchForPlayer()
    {
        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, targetDetectionRadius, playerLayer);

        if (playerCollider != null)
        {
            _targetPlayer = playerCollider.transform;
        }
        else
        {
            _targetPlayer = null;
        }
    }

    /// <summary>
    /// 壁へのめり込みを検知して速度を制御する
    /// </summary>
    private Vector2 CheckWallCollision(Vector2 currentVelocity)
    {
        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            Vector2 moveDir = currentVelocity.normalized;
            RaycastHit2D hit = Physics2D.Linecast(
                transform.position,
                (Vector2)transform.position + moveDir * wallCheckDistance,
                groundLayer
            );

            if (hit.collider != null)
            {
                return Vector2.zero; // 壁があったらストップ
            }
        }
        return currentVelocity;
    }

    /// <summary>
    /// 攻撃モーションのトリガーを発火
    /// </summary>
    private void TriggerAttack()
    {
        if (_isAttacking) return; // すでに攻撃中なら何度も呼ばない（連打防止）

        _isAttacking = true;

        if (animator != null)
        {
            animator.SetTrigger(attackTriggerName);
        }

        Debug.Log("🎯 敵の攻撃モーション開始！");
    }

    /// <summary>
    /// 移動方向（X軸）に応じて敵のグラフィックの向きを反転させる
    /// </summary>
    private void UpdateFacingDirection(float horizontalMovement)
    {
        if (Mathf.Abs(horizontalMovement) > 0.05f)
        {
            float direction = Mathf.Sign(horizontalMovement);
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction, transform.localScale.y, transform.localScale.z);
        }
    }

    /// <summary>
    /// アニメーターの移動フラグを更新
    /// </summary>
    private void UpdateAnimationParams(Vector2 currentVelocity)
    {
        if (animator != null)
        {
            // 速度が一定以上あれば移動中(true)、なければ静止(false)
            bool isMoving = currentVelocity.sqrMagnitude > 0.01f;
            animator.SetBool(moveBoolName, isMoving);
        }
    }

    /// <summary>
    /// Unityエディタ上に索敵範囲（赤）と攻撃範囲（黄）、壁センサー（青）を可視化
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 索敵範囲（広い円：赤）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, targetDetectionRadius);

        // 攻撃範囲（狭い円：黄）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        // 壁検知センサー（青）
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * wallCheckDistance);
        Gizmos.DrawLine(transform.position, transform.position - transform.right * wallCheckDistance);
        Gizmos.DrawLine(transform.position, transform.position + transform.up * wallCheckDistance);
        Gizmos.DrawLine(transform.position, transform.position - transform.up * wallCheckDistance);
    }
}