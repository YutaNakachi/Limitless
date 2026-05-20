using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct ColliderData
{
    public Vector2 size;
    public Vector2 offset;
    public CapsuleDirection2D direction;
}

public class PlayerController : MonoBehaviour
{
    [Header("Walking Speed")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float doubleTapTimeLimit = 0.25f; // 2回押しとして認める時間（秒）

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxNumOfAirJumps = 1;
    [SerializeField] private float dashJumpForce = 1.1f;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck; // 足元に配置する空のGameObject
    [SerializeField] private Vector2 checkSize = new Vector2(0.5f, 0.1f); // 判定エリアのサイズ
    [SerializeField] private LayerMask groundLayer; // Groundレイヤーだけを判定対象にする

    [Header("Wall Action Setting")]
    [SerializeField] private Transform wallCheckPoint; // プレイヤーの側面に配置した空のGameObject
    [SerializeField] private float wallCheckRadius = 0.2f; // 判定の円の大きさ
    [SerializeField] private LayerMask wallLayer; // 先ほど作った「Wall」レイヤーを指定
    [SerializeField] private float wallSlideSpeed = 2.0f; // 壁をずり落ちる最高速度
    [SerializeField] private Vector2 wallJumpForce = new Vector2(10.0f, 12.0f); // 壁ジャンプのパワー (x: 反発, y: 上昇)
    [SerializeField] private float wallJumpTime = 0.15f; // 壁ジャンプ後の慣性

    [Header("Collider Settings")]
    [SerializeField] private ColliderData normalCollider;
    [SerializeField] private ColliderData crouchCollider;
    [SerializeField] private ColliderData dashCollider;

    // 内部コンポーネント・変数
    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private CapsuleCollider2D _collider;
    private BallManager _ballManager;
    private bool isGrounded;
    private bool isCrouching;
    private bool isOnDash;
    private bool isTouchingWall;
    private bool isWallSliding;
    private Vector2 moveInput;
    private Vector3 firstScale;
    private float currentVelocityX;
    private int remainingAirJumpCount;
    private float dashTimer;
    private float originalGravityScale; // 元の重力を保存する変数
    private float wallJumpLockTimer;    // 追加：壁ジャンプ直後の操作ロックタイマー

    // ダブルタップ判定用の変数
    private float lastInputTimeRight;    // 右入力が最後に押された時間
    private float lastInputTimeLeft;     // 左入力が最後に押された時間
    private bool isAxisZeroLastFrame = true; // 前フレームで入力が0（ニュートラル）だったか

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider2D>();
        _ballManager = GetComponent<BallManager>();
    }

    void Start()
    {
        firstScale = transform.localScale;
        originalGravityScale = _rigidbody.gravityScale;
        _animator.SetTrigger("Intro");
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed && !isOnDash)
        {
            float direction = moveInput.x != 0 ? Mathf.Sign(moveInput.x) : Mathf.Sign(transform.localScale.x);
            StartDash(direction);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (isGrounded || remainingAirJumpCount > 0)
            {
                if (isOnDash && isGrounded && !isWallSliding)
                {
                    isOnDash = false;
                    _rigidbody.gravityScale = originalGravityScale;

                    // ダッシュの勢いを計算（例：10 * 1.5 = 15）
                    float jumpDashVelocityX = _rigidbody.linearVelocityX * dashJumpForce;

                    // 変数に代入
                    currentVelocityX = jumpDashVelocityX;

                    // 物理的な速度も即座に更新
                    _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce * dashJumpForce);
                }
                else if (isGrounded && !isWallSliding)
                {
                    // 通常ジャンプ
                    currentVelocityX = _rigidbody.linearVelocityX;
                    _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce);
                }
                else if (!isWallSliding)
                {
                    // 空中でのジャンプ
                    currentVelocityX = _rigidbody.linearVelocityX;
                    _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce);
                    remainingAirJumpCount--;
                }

                _animator.SetTrigger("Jump");
            }

            if (isWallSliding)
            {
                WallJump();
                _animator.SetTrigger("Jump");
            }
        }
    }

    public void OnCrounch(InputAction.CallbackContext context)
    {
        if (context.performed && isGrounded)
        {
            isCrouching = true;
        }

        if (context.canceled)
        {
            isCrouching = false;
        }
    }

    public void OnKick(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _animator.SetTrigger("Kick");
        }
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (context.performed && !_ballManager.isReloading)
        {
            _ballManager.RefillEmptySlots();
        }
    }

    private void Update()
    {
        // 各種タイマーの減算
        if (wallJumpLockTimer > 0) wallJumpLockTimer -= Time.deltaTime;

        // ジャンプ回数復活
        if (isGrounded) remainingAirJumpCount = maxNumOfAirJumps;

        // 地面判定の実行
        // 指定した範囲(checkSize)内に、GroundレイヤーのColliderがあるかチェック
        isGrounded = Physics2D.OverlapBox(groundCheck.position, checkSize, 0f, groundLayer);

        // 壁の検出 (Physics2D.OverlapCircle を使用)
        // プレイヤーの向きに合わせて wallCheckPoint の位置を調整するか、左右両方に配置してチェックします
        isTouchingWall = Physics2D.OverlapCircle(wallCheckPoint.position, wallCheckRadius, wallLayer);

        // 壁スライディングの条件チェック
        // 「壁に触れていて」「地面に接していなくて」「壁の方向に入力が入っている」ときに発動
        if (isTouchingWall && !isGrounded && moveInput.x != 0)
        {
            isWallSliding = true;
            remainingAirJumpCount = maxNumOfAirJumps;
        }
        else
        {
            isWallSliding = false;
        }

        // 毎フレーム、方向キーのダブルタップを監視
        DetectDoubleTapDash();

        // Playerの描画向きを更新
        UpdateVisualDirection();

        // AnimatorのParameterにStatusを渡す
        _animator.SetFloat("MoveSpeed", _rigidbody.linearVelocity.magnitude);
        _animator.SetBool("IsGrounded", isGrounded);
        _animator.SetBool("IsCrouching", isCrouching);
        _animator.SetBool("IsOnDash", isOnDash);
        _animator.SetBool("IsOnWallSliding", isWallSliding);
    }

    private void FixedUpdate()
    {
        if (isOnDash)
        {
            HandleDash();
        }
        else
        {
            // タイマーが切れている時だけ、通常の左右移動処理を行う
            if (wallJumpLockTimer <= 0)
            {
                HandleNormalMovement();
            }
            else
            {
                // ロック中は、WallJumpで設定した反発速度をそのまま維持して物理に渡す
                _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);
            }
        }

        wallSliding();
    }

    /// <summary>
    /// 方向キーの2回連続入力をデジタルに検知するロジック
    /// </summary>
    private void DetectDoubleTapDash()
    {
        if (isOnDash) return;

        // 1. 右方向の入力
        if (moveInput.x > 0)
        {
            if (isAxisZeroLastFrame) // 前のフレームでキーが離れていた（＝今新しく押された）
            {
                float timeSinceLastTap = Time.time - lastInputTimeRight;

                if (timeSinceLastTap <= doubleTapTimeLimit)
                {
                    StartDash(1f); // 右ダッシュ
                }

                lastInputTimeRight = Time.time;
                isAxisZeroLastFrame = false;
            }
        }
        // 2. 左方向の入力
        else if (moveInput.x < 0)
        {
            if (isAxisZeroLastFrame)
            {
                float timeSinceLastTap = Time.time - lastInputTimeLeft;

                if (timeSinceLastTap <= doubleTapTimeLimit)
                {
                    StartDash(-1f); // 左ダッシュ
                }

                lastInputTimeLeft = Time.time;
                isAxisZeroLastFrame = false;
            }
        }
        // 3. 入力なし（ニュートラル）
        else
        {
            isAxisZeroLastFrame = true; // キーが離されたので、次回の「1回目」を許可する
        }
    }

    /// <summary>
    /// ダッシュの開始処理（共通化してカプセル化）
    /// </summary>
    private void StartDash(float direction)
    {
        isOnDash = true;
        dashTimer = dashDuration;
        _rigidbody.gravityScale = 0;
        _rigidbody.linearVelocity = new Vector2(direction * dashSpeed, 0);
    }

    private void UpdateVisualDirection()
    {
        // プレイヤーの向きを変更
        if (_rigidbody.linearVelocityX > 0)
        {
            transform.localScale = firstScale;
        }
        else if (_rigidbody.linearVelocityX < 0)
        {
            transform.localScale = new Vector3(-firstScale.x, firstScale.y, firstScale.z);
        }
    }

    private void HandleDash()
    {
        dashTimer -= Time.deltaTime;

        // Collider形状を変更
        UpdateCollider(dashCollider);

        // ダッシュ中は毎フレーム Y 速度を 0 に固定
        _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0);

        if (dashTimer <= 0)
        {
            isOnDash = false;

            // 重力を元に戻す
            _rigidbody.gravityScale = originalGravityScale;
        }
    }

    private void HandleNormalMovement()
    {
        // 目標とする速度を決定
        float targetSpeed;
        if (isCrouching)
        {
            UpdateCollider(crouchCollider);
            targetSpeed = 0;
        }
        else
        {
            UpdateCollider(normalCollider);
            targetSpeed = moveInput.x * moveSpeed;
        }


        if (isGrounded)
        {
            // 地面にいるとき：素早く目標速度に合わせる
            float groundAccel = 50f; // 地面の食いつき
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, groundAccel * Time.deltaTime);
        }
        else
        {
            // 空中にいるとき：ゆっくり目標速度に合わせる
            float airControl = 25f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, airControl * Time.deltaTime);
        }

        _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);
    }

    private void WallJump()
    {
        isWallSliding = false;

        // 壁とは「逆の方向」を割り出す
        float jumpDirection = -moveInput.x;

        // もし入力が0なら、直前のlocalScale（向いている方向）の逆へ跳ぶ安全設計
        if (moveInput.x == 0 && wallCheckPoint != null)
        {
            jumpDirection = -Mathf.Sign(transform.localScale.x);
        }

        // ロックタイマーをセット（0.15秒〜0.2秒あたりが気持ちいいです）
        wallJumpLockTimer = wallJumpTime;

        // 瞬間的な力を計算し、移動速度の基準となる currentVelocityX も上書き同期する（超重要）
        currentVelocityX = wallJumpForce.x * jumpDirection;
        _rigidbody.linearVelocity = new Vector2(currentVelocityX, wallJumpForce.y);
    }

    private void wallSliding()
    {
        // 壁スライディング中の速度制御
        if (isWallSliding)
        {
            // 下方向への落下速度を wallSlideSpeed に制限（めり込み防止Continuousと相性◎）
            if (_rigidbody.linearVelocity.y < -wallSlideSpeed)
            {
                _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, -wallSlideSpeed);
            }
        }
    }

    // Collider形状を切り替えるためのメソッド
    private void UpdateCollider(ColliderData data)
    {
        _collider.size = data.size;
        _collider.offset = data.offset;
        _collider.direction = data.direction;
    }

    // デバッグ用に判定エリアを画面に表示する
    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, checkSize);
        }

        if (wallCheckPoint != null)
        {
            Gizmos.color = isTouchingWall ? Color.green : Color.red;
            Gizmos.DrawWireSphere(wallCheckPoint.position, wallCheckRadius);
        }
    }
}
