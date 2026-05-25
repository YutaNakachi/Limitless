using System.Collections;
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
    [SerializeField] private int maxNumOfAirDashes = 1;         // 🔥 【追加】空中ダッシュの最大許容回数

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

    [Header("One Way Platform Settings")]
    [SerializeField] private float downDoubleTapTimeLimit = 0.25f; // 下2回押しとして認める時間（秒）

    [Header("Intro Settings")]
    [SerializeField] private float introDuration = 1.5f;

    [Header("Collider Settings")]
    [SerializeField] private ColliderData normalCollider;
    [SerializeField] private ColliderData crouchCollider;
    [SerializeField] private ColliderData dashCollider;

    // 内部コンポーネント・変数
    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private CapsuleCollider2D _collider;
    private BallManager _ballManager;
    private MobStatus _status;
    private bool isGrounded;
    private bool isCrouching;
    private bool isOnDash;
    private bool isTouchingWall;
    private bool isWallSliding;
    private Vector2 moveInput;
    private Vector3 firstScale;
    private float currentVelocityX;
    private int remainingAirJumpCount;
    private int remainingAirDashCount; // 🔥 【追加】空中ダッシュの残り回数
    private float dashTimer;
    private float originalGravityScale; // 元の重力を保存する変数
    private float wallJumpLockTimer;    // 追加：壁ジャンプ直後の操作ロックタイマー

    // ダブルタップ判定用変数
    private float lastInputTimeRight;    // 右入力が最後に押された時間
    private float lastInputTimeLeft;     // 左入力が最後に押された時間
    private bool isAxisZeroLastFrame = true; // 前フレームで入力が0（ニュートラル）だったか

    // OneWayPlatformをすり抜け判定用変数
    private float lastInputTimeDown; // 下入力が最後に押された時間
    private bool isYAxisZeroLastFrame = true; // 連打判定のために「一度レバーをニュートラルに戻したか」
    private OneWayPlatform currentPlatform;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider2D>();
        _ballManager = GetComponent<BallManager>();
        _status = GetComponent<MobStatus>();
    }

    void Start()
    {
        firstScale = transform.localScale;
        originalGravityScale = _rigidbody.gravityScale;

        // 🔥 アニメーションを再生し、同時にステートを「Intro（行動不可）」にする
        _animator.SetTrigger("Intro");
        _status.SetIntroState();

        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // 💡 イントロアニメーションの長さ（秒）だけ待つ
        yield return new WaitForSeconds(introDuration);

        // 通常状態に戻して、動けるようにする！
        _status.GoToNormalStateIfPossible();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (_status.IsDead || _status.IsInIntroMotion) return;

        // 🔥 【進化】地上キック中、または空中キック中にダッシュが押されたら強制キャンセル
        // ※ただし、空中キックキャンセルの場合も「空中ダッシュ可能か」を事前にチェックする
        if (!_status.IsMovable)
        {
            if (isGrounded || remainingAirDashCount > 0)
            {
                _status.GoToNormalStateIfPossible();
                Debug.Log(isGrounded ? "⚡ 地上キックをダッシュキャンセル！" : "✨ 空中キックをダッシュキャンセル！");
            }
        }

        // ステートがNormal（移動可能）な場合のみダッシュを開始
        if (_status.IsMovable && !isOnDash)
        {
            // 🔥 地上か、あるいは空中ダッシュの残り回数がある場合のみ発動
            if (isGrounded || remainingAirDashCount > 0)
            {
                float direction = moveInput.x != 0 ? Mathf.Sign(moveInput.x) : Mathf.Sign(transform.localScale.x);
                StartDash(direction);
            }
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (_status.IsDead || _status.IsInIntroMotion) return;

        if (context.performed)
        {
            // 🔥 【進化】キック中（!IsMovable）のときの特殊ジャンプ割り込み判定
            if (!_status.IsMovable)
            {
                // 地上キック中、または（空中キック中かつ2段ジャンプ可能）ならキャンセル許可
                if (isGrounded || remainingAirJumpCount > 0)
                {
                    _status.GoToNormalStateIfPossible();
                    Debug.Log(isGrounded ? "⚡ 地上キックをジャンプキャンセル！" : "✨ 空中キックを2段ジャンプキャンセル！");
                }
            }

            // ステートがNormal（移動可能）である場合のみジャンプ処理を通す
            if (_status.IsMovable)
            {
                if (isGrounded || remainingAirJumpCount > 0)
                {
                    if (isOnDash && isGrounded && !isWallSliding)
                    {
                        isOnDash = false;
                        _rigidbody.gravityScale = originalGravityScale;

                        // ダッシュの勢いを計算
                        float jumpDashVelocityX = _rigidbody.linearVelocityX * dashJumpForce;
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
                        // 空中でのジャンプ（2段ジャンプなど）
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
    }

    public void OnCrounch(InputAction.CallbackContext context)
    {
        if (_status.IsDead || _status.IsInIntroMotion) return;

        if (!_status.IsMovable) return;

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
            if (_ballManager.isReloading) return;

            _status.GoToAttackStateIfPossible();
        }
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (_status.IsDead || _status.IsInIntroMotion) return;

        if (context.performed && !_ballManager.isReloading)
        {
            _ballManager.RefillEmptySlots();
        }
    }

    private void Update()
    {
        if (wallJumpLockTimer > 0) wallJumpLockTimer -= Time.deltaTime;

        // 🔥 地上にいるときはジャンプ回数とダッシュ回数をリセット
        if (isGrounded)
        {
            remainingAirJumpCount = maxNumOfAirJumps;
            remainingAirDashCount = maxNumOfAirDashes;
        }

        isGrounded = Physics2D.OverlapBox(groundCheck.position, checkSize, 0f, groundLayer);
        isTouchingWall = Physics2D.OverlapCircle(wallCheckPoint.position, wallCheckRadius, wallLayer);

        if (_status.IsMovable && isTouchingWall && !isGrounded && moveInput.x != 0)
        {
            isWallSliding = true;
            // 🔥 壁摺り下がり時も回数をリセット（壁キックからのリトライをスムーズにするため）
            remainingAirJumpCount = maxNumOfAirJumps;
            remainingAirDashCount = maxNumOfAirDashes;
        }
        else
        {
            isWallSliding = false;
        }

        if (_status.IsMovable) DetectDownDoubleTap();

        // 毎フレーム、方向キーのダブルタップを監視（キック中の割り込みも内部で判定）
        DetectDoubleTapDash();

        if (_status.IsMovable) UpdateVisualDirection();

        _animator.SetFloat("MoveSpeed", _rigidbody.linearVelocity.magnitude);
        _animator.SetBool("IsGrounded", isGrounded);
        _animator.SetBool("IsCrouching", isCrouching);
        _animator.SetBool("IsOnDash", isOnDash);
        _animator.SetBool("IsOnWallSliding", isWallSliding);
    }

    private void FixedUpdate()
    {
        if (_status.IsDead || _status.IsKnockbacking || _status.IsInIntroMotion) return;


        // キック中（Attackステート）の物理制御インターロック
        if (!_status.IsMovable)
        {
            isWallSliding = false;

            if (!isGrounded)
            {
                // A. 空中キック：重力を完全にゼロにし、その場に完全ロック（ピタッと滞空）
                _rigidbody.gravityScale = 0f;
                _rigidbody.linearVelocity = Vector2.zero;
                currentVelocityX = 0f;
            }
            else
            {
                // B. 地上キック：横移動の慣性だけを強制ストップ
                _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
                currentVelocityX = 0f;
            }
            return;
        }

        // キック状態やダッシュから通常に戻ってきた際、重力を安全復帰
        if (_rigidbody.gravityScale == 0f && !isOnDash)
        {
            _rigidbody.gravityScale = originalGravityScale;
        }

        if (isOnDash)
        {
            HandleDash();
        }
        else
        {
            if (wallJumpLockTimer <= 0)
            {
                HandleNormalMovement();
            }
            else
            {
                _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);
            }
        }

        wallSliding();
    }

    private void DetectDoubleTapDash()
    {
        if (isOnDash) return;
        if (_status.IsDead || _status.IsInIntroMotion) return;

        // 🔥 通常状態、または「キック中（地上・空中問わず）」であればダブルタップダッシュを許可
        bool canDashInput = _status.IsMovable || !_status.IsMovable;
        if (!canDashInput) return;

        // 1. 右方向の入力
        if (moveInput.x > 0)
        {
            if (isAxisZeroLastFrame)
            {
                float timeSinceLastTap = Time.time - lastInputTimeRight;

                if (timeSinceLastTap <= doubleTapTimeLimit)
                {
                    // 🔥 空中であればダッシュ残弾数がある場合のみ実行
                    if (isGrounded || remainingAirDashCount > 0)
                    {
                        if (!_status.IsMovable) _status.GoToNormalStateIfPossible(); // キックキャンセル
                        StartDash(1f);
                    }
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
                    // 🔥 空中であればダッシュ残弾数がある場合のみ実行
                    if (isGrounded || remainingAirDashCount > 0)
                    {
                        if (!_status.IsMovable) _status.GoToNormalStateIfPossible(); // キックキャンセル
                        StartDash(-1f);
                    }
                }

                lastInputTimeLeft = Time.time;
                isAxisZeroLastFrame = false;
            }
        }
        // 3. 入力なし
        else
        {
            isAxisZeroLastFrame = true;
        }
    }

    private void DetectDownDoubleTap()
    {
        if (moveInput.y < -0.5f)
        {
            if (isYAxisZeroLastFrame)
            {
                float timeSinceLastTap = Time.time - lastInputTimeDown;

                if (timeSinceLastTap <= downDoubleTapTimeLimit)
                {
                    if (currentPlatform != null)
                    {
                        currentPlatform.PassThrough(_collider);
                        currentPlatform = null;
                    }
                }

                lastInputTimeDown = Time.time;
                isYAxisZeroLastFrame = false;
            }
        }
        else
        {
            isYAxisZeroLastFrame = true;
        }
    }

    private void StartDash(float direction)
    {
        isOnDash = true;
        dashTimer = dashDuration;
        _rigidbody.gravityScale = 0;
        _rigidbody.linearVelocity = new Vector2(direction * dashSpeed, 0);

        // 🔥 【追加】空中ダッシュだった場合は残弾数を減らす
        if (!isGrounded)
        {
            remainingAirDashCount--;
        }
    }

    private void UpdateVisualDirection()
    {
        float threshold = 0.1f;

        if (_rigidbody.linearVelocityX > threshold && moveInput.x >= 0)
        {
            transform.localScale = firstScale;
        }
        else if (_rigidbody.linearVelocityX < -threshold && moveInput.x <= 0)
        {
            transform.localScale = new Vector3(-firstScale.x, firstScale.y, firstScale.z);
        }
    }

    private void HandleDash()
    {
        dashTimer -= Time.deltaTime;
        UpdateCollider(dashCollider);
        _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0);

        if (dashTimer <= 0)
        {
            isOnDash = false;
            _rigidbody.gravityScale = originalGravityScale;
        }
    }

    private void HandleNormalMovement()
    {
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
            float groundAccel = 50f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, groundAccel * Time.deltaTime);
        }
        else
        {
            float airControl = 25f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, airControl * Time.deltaTime);
        }

        _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);
    }

    private void WallJump()
    {
        isWallSliding = false;
        float jumpDirection = -moveInput.x;

        if (moveInput.x == 0 && wallCheckPoint != null)
        {
            jumpDirection = -Mathf.Sign(transform.localScale.x);
        }

        wallJumpLockTimer = wallJumpTime;
        currentVelocityX = wallJumpForce.x * jumpDirection;
        _rigidbody.linearVelocity = new Vector2(currentVelocityX, wallJumpForce.y);
    }

    private void wallSliding()
    {
        if (isWallSliding)
        {
            if (_rigidbody.linearVelocity.y < -wallSlideSpeed)
            {
                _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, -wallSlideSpeed);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("OneWayPlatform"))
        {
            currentPlatform = collision.gameObject.GetComponent<OneWayPlatform>();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("OneWayPlatform"))
        {
            currentPlatform = null;
        }
    }

    private void UpdateCollider(ColliderData data)
    {
        _collider.size = data.size;
        _collider.offset = data.offset;
        _collider.direction = data.direction;
    }

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