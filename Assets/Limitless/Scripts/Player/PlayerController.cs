using Cysharp.Threading.Tasks;
using System.Collections;
using System.Threading;
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
    [SerializeField] private float doubleTapTimeLimit = 0.25f;
    [SerializeField] private int maxNumOfAirDashes = 1;
    [SerializeField] private int dashInvincibleFrames = 4;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxNumOfAirJumps = 1;
    [SerializeField] private float dashJumpForce = 1.1f;

    [Header("Smash Kick Settings")]
    [SerializeField] private float smashInputThreshold = 0.6f;
    [SerializeField] private float smashWindowTime = 0.1f;
    [SerializeField] private int smashWaitFrames = 3;

    [Header("Coyote & Buffer Settings")]
    [SerializeField] private float coyoteDuration = 0.12f;
    [SerializeField] private float jumpBufferDuration = 0.1f;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 checkSize = new Vector2(0.5f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    [Header("Wall Action Setting")]
    [SerializeField] private Transform wallCheckPoint;
    [SerializeField] private float wallCheckRadius = 0.2f;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallSlideSpeed = 2.0f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(10.0f, 12.0f);
    [SerializeField] private float wallJumpTime = 0.15f;

    [Header("One Way Platform Settings")]
    [SerializeField] private float downDoubleTapTimeLimit = 0.25f;

    [Header("Intro Settings")]
    [SerializeField] private float introDuration = 1.5f;

    [Header("Collider Settings")]
    [SerializeField] private ColliderData normalCollider;
    [SerializeField] private ColliderData crouchCollider;
    [SerializeField] private ColliderData dashCollider;

    [Header("Audio Settings Extension")]
    [SerializeField] private float landVelocityThreshold = -2.0f;

    // 内部コンポーネント・変数
    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private CapsuleCollider2D _collider;
    private BallManager _ballManager;
    private MobStatus _status;
    private PlayerShoot _playerShoot;
    private bool isGrounded;
    private bool isCrouching;
    private bool isOnDash;
    private bool isTouchingWall;
    private bool isWallSliding;
    private Vector2 moveInput;
    private Vector3 firstScale;
    private float currentVelocityX;
    private int remainingAirJumpCount;
    private int remainingAirDashCount;
    private float dashTimer;
    private float originalGravityScale;
    private float wallJumpLockTimer;
    private Vector2 _lastStickInput;
    private float _smashTimer;
    private int _smashWaitFrameCount = 0;
    private bool _isWaitingForSmash = false;

    private float _coyoteTimer;
    private float _jumpBufferTimer;

    private float lastInputTimeRight;
    private float lastInputTimeLeft;
    private bool isAxisZeroLastFrame = true;

    private float lastInputTimeDown;
    private bool isYAxisZeroLastFrame = true;
    private OneWayPlatform currentPlatform;

    private CancellationTokenSource _dashInvincibleCancelToken;

    private bool _wasGrounded;
    private bool _wasWallSliding;

    // 💡【追加】ジャンプ直後の誤接地判定を防ぐためのタイマー（秒）
    private float _jumpCooldownTimer;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider2D>();
        _ballManager = GetComponent<BallManager>();
        _status = GetComponent<MobStatus>();
        _playerShoot = GetComponent<PlayerShoot>();
    }

    void Start()
    {
        firstScale = transform.localScale;
        originalGravityScale = _rigidbody.gravityScale;
        _dashInvincibleCancelToken = new CancellationTokenSource();

        _animator.SetTrigger("Intro");
        _status.SetIntroState();

        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        yield return new WaitForSeconds(introDuration);
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

        if (!_status.IsMovable)
        {
            if (isGrounded || remainingAirDashCount > 0)
            {
                _status.GoToNormalStateIfPossible();
                Debug.Log(isGrounded ? "⚡ 地上キックをダッシュキャンセル！" : "✨ 空中キックをダッシュキャンセル！");
            }
        }

        if (_status.IsMovable && !isOnDash)
        {
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
            _jumpBufferTimer = jumpBufferDuration;
            TryExecuteJump();
        }
    }

    private void TryExecuteJump()
    {
        if (_jumpBufferTimer > 0f)
        {
            if (!_status.IsMovable)
            {
                if (_coyoteTimer > 0f || remainingAirJumpCount > 0)
                {
                    _status.GoToNormalStateIfPossible();
                    Debug.Log(_coyoteTimer > 0f ? "⚡ 地上キックをコヨーテジャンプキャンセル！" : "✨ 空中キックを2段ジャンプキャンセル！");
                }
            }

            if (_status.IsMovable)
            {
                if (isWallSliding)
                {
                    WallJump();
                    _animator.SetTrigger("Jump");
                    _jumpBufferTimer = 0f;
                    return;
                }

                if (_coyoteTimer > 0f)
                {
                    if (isOnDash && !isWallSliding)
                    {
                        isOnDash = false;
                        _rigidbody.gravityScale = originalGravityScale;

                        float jumpDashVelocityX = _rigidbody.linearVelocityX * dashJumpForce;
                        currentVelocityX = jumpDashVelocityX;
                        _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce * dashJumpForce);
                    }
                    else if (!isWallSliding)
                    {
                        currentVelocityX = _rigidbody.linearVelocityX;
                        _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce);
                    }

                    _animator.SetTrigger("Jump");
                    _coyoteTimer = 0f;
                    _jumpBufferTimer = 0f;

                    // 💡【修正】タイマーをセットして確実に空中状態を作る
                    _jumpCooldownTimer = 0.1f;
                    isGrounded = false;
                    _wasGrounded = false;
                }
                else if (remainingAirJumpCount > 0 && !isWallSliding)
                {
                    currentVelocityX = _rigidbody.linearVelocityX;
                    _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce);
                    remainingAirJumpCount--;

                    _animator.SetTrigger("Jump");
                    _jumpBufferTimer = 0f;

                    // 💡【修正】2段ジャンプ時も同様にタイマーをセット
                    _jumpCooldownTimer = 0.1f;
                    isGrounded = false;
                    _wasGrounded = false;
                }
            }
        }
    }

    public void OnCrounch(InputAction.CallbackContext context)
    {
        if (_status.IsDead || _status.IsInIntroMotion)
        {
            isCrouching = false;
            return;
        }

        if (context.performed)
        {
            if (_status.IsMovable && isGrounded)
            {
                if (!isCrouching)
                {
                    SoundManager.Instance.PlaySEAtPosition("Crouch", transform.position);
                }
                isCrouching = true;
            }
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
            if (_status.IsDead || _status.IsKnockbacking || _status.IsInIntroMotion) return;
            _isWaitingForSmash = true;
            _smashWaitFrameCount = smashWaitFrames;
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

        // 💡【修正】ジャンプ直後は物理的な接地判定をスキップして強制的に false にする
        if (_jumpCooldownTimer > 0f)
        {
            _jumpCooldownTimer -= Time.deltaTime;
            isGrounded = false;
        }
        else
        {
            isGrounded = Physics2D.OverlapBox(groundCheck.position, checkSize, 0f, groundLayer);
        }

        isTouchingWall = Physics2D.OverlapCircle(wallCheckPoint.position, wallCheckRadius, wallLayer);

        // 着地音（Land）の判定
        if (!_wasGrounded && isGrounded)
        {
            if (_rigidbody.linearVelocity.y < landVelocityThreshold)
            {
                SoundManager.Instance.PlaySEAtPosition("Land", transform.position);
            }
        }
        _wasGrounded = isGrounded;

        if (isGrounded)
        {
            _coyoteTimer = coyoteDuration;
            remainingAirJumpCount = maxNumOfAirJumps;
            remainingAirDashCount = maxNumOfAirDashes;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }

        if (_jumpBufferTimer > 0f)
        {
            _jumpBufferTimer -= Time.deltaTime;
        }

        if (_status.IsMovable && isTouchingWall && !isGrounded && moveInput.x != 0)
        {
            isWallSliding = true;
            remainingAirJumpCount = maxNumOfAirJumps;
            remainingAirDashCount = maxNumOfAirDashes;
            _coyoteTimer = 0f;
        }
        else
        {
            isWallSliding = false;
        }

        // 壁すり音（WallSliding）のループ再生制御
        if (isWallSliding)
        {
            if (!_wasWallSliding)
            {
                SoundManager.Instance.PlaySEAtPosition("WallSliding", transform.position);
            }
        }
        else
        {
            if (_wasWallSliding)
            {
                SoundManager.Instance.StopLoopSE("WallSliding");
            }
        }
        _wasWallSliding = isWallSliding;

        if (_jumpBufferTimer > 0f)
        {
            TryExecuteJump();
        }

        if (_status.IsMovable) DetectDownDoubleTap();
        DetectDoubleTapDash();
        DetectStickFlick();
        HandleKickExecution();

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

        if (!_status.IsMovable)
        {
            isWallSliding = false;

            if (!isGrounded)
            {
                _rigidbody.gravityScale = 0f;
                _rigidbody.linearVelocity = Vector2.zero;
                currentVelocityX = 0f;
            }
            else
            {
                _rigidbody.linearVelocity = new Vector2(0f, _rigidbody.linearVelocity.y);
                currentVelocityX = 0f;
            }
            return;
        }

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
        if (_isWaitingForSmash) return;

        bool canDashInput = _status.IsMovable || !_status.IsMovable;
        if (!canDashInput) return;

        if (moveInput.x > 0)
        {
            if (isAxisZeroLastFrame)
            {
                float timeSinceLastTap = Time.time - lastInputTimeRight;

                if (timeSinceLastTap <= doubleTapTimeLimit)
                {
                    if (isGrounded || remainingAirDashCount > 0)
                    {
                        if (!_status.IsMovable) _status.GoToNormalStateIfPossible();
                        StartDash(1f);
                    }
                }

                lastInputTimeRight = Time.time;
                isAxisZeroLastFrame = false;
            }
        }
        else if (moveInput.x < 0)
        {
            if (isAxisZeroLastFrame)
            {
                float timeSinceLastTap = Time.time - lastInputTimeLeft;

                if (timeSinceLastTap <= doubleTapTimeLimit)
                {
                    if (isGrounded || remainingAirDashCount > 0)
                    {
                        if (!_status.IsMovable) _status.GoToNormalStateIfPossible();
                        StartDash(-1f);
                    }
                }

                lastInputTimeLeft = Time.time;
                isAxisZeroLastFrame = false;
            }
        }
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

    private void DetectStickFlick()
    {
        if (_smashTimer > 0) _smashTimer -= Time.deltaTime;

        Vector2 delta = moveInput - _lastStickInput;

        if (delta.magnitude > smashInputThreshold && moveInput.magnitude > 0.5f)
        {
            _smashTimer = smashWindowTime;
            Debug.Log("スマッシュ検知！");
        }

        _lastStickInput = moveInput;
    }

    private void HandleKickExecution()
    {
        if (!_isWaitingForSmash) return;

        if (_smashTimer > 0f)
        {
            ExecuteKick(isSmash: true);
            return;
        }

        _smashWaitFrameCount--;

        if (_smashWaitFrameCount <= 0)
        {
            ExecuteKick(isSmash: false);
        }
    }

    private void ExecuteKick(bool isSmash)
    {
        _isWaitingForSmash = false;

        if (isOnDash)
        {
            isOnDash = false;
            _rigidbody.gravityScale = originalGravityScale;
            Debug.Log("⚠️ ダッシュ中にキックが紐づいたため、ダッシュ状態を強制解除しました。");
        }

        if (isSmash)
        {
            _smashTimer = 0f;
            Debug.Log("💥 ーー 同時押し救済でスマッシュ成立！ ーー 💥");
            _animator.SetTrigger("Kick");
            _animator.SetFloat("ShootDirection", _playerShoot.CurrentShootAngle);
            _animator.SetBool("IsSmash", isSmash);
        }
        else
        {
            _animator.SetTrigger("Kick");
            _animator.SetFloat("ShootDirection", _playerShoot.CurrentShootAngle);
            _animator.SetBool("IsSmash", isSmash);
        }

        if (_playerShoot != null)
        {
            _playerShoot.SetSmashFlag(isSmash);
        }

        _status.GoToAttackStateIfPossible();
    }

    private void StartDash(float direction)
    {
        isOnDash = true;

        SoundManager.Instance.PlaySEAtPosition("Dash", transform.position);

        TriggerDashInvincibleAsync().Forget();

        dashTimer = dashDuration;
        _rigidbody.gravityScale = 0;
        _rigidbody.linearVelocity = new Vector2(direction * dashSpeed, 0);

        if (!isGrounded)
        {
            remainingAirDashCount--;
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
        if (!isGrounded)
        {
            isCrouching = false;
        }

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
            float currentAccel = 50f;
            if (targetSpeed != 0 && Mathf.Sign(targetSpeed) != Mathf.Sign(currentVelocityX) && currentVelocityX != 0)
            {
                currentAccel = 150f;
            }

            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, currentAccel * Time.deltaTime);
        }
        else
        {
            float currentAirControl = 25f;
            if (targetSpeed != 0 && Mathf.Sign(targetSpeed) != Mathf.Sign(currentVelocityX) && currentVelocityX != 0)
            {
                currentAirControl = 60f;
            }

            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, currentAirControl * Time.deltaTime);
        }

        _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);
    }

    private async UniTaskVoid TriggerDashInvincibleAsync()
    {
        if (_status == null) return;

        _status.SetInvicible();
        Debug.Log($"🛡️ ダッシュ無敵 ON ({dashInvincibleFrames}フレーム)");

        await UniTask.DelayFrame(dashInvincibleFrames, PlayerLoopTiming.FixedUpdate, _dashInvincibleCancelToken.Token);

        if (_status != null)
        {
            _status.CancelInvicible();
            Debug.Log("❌ ダッシュ無敵 OFF");
        }
    }

    private void WallJump()
    {
        isWallSliding = false;
        float jumpDirection = -moveInput.x;

        if (moveInput.x == 0 && wallCheckPoint != null)
        {
            jumpDirection = -Mathf.Sign(transform.localScale.x);
        }

        SoundManager.Instance.PlaySEAtPosition("WallJump", transform.position);

        wallJumpLockTimer = wallJumpTime;
        currentVelocityX = wallJumpForce.x * jumpDirection;
        _rigidbody.linearVelocity = new Vector2(currentVelocityX, wallJumpForce.y);

        // 💡【修正】壁ジャンプ時も同様にタイマーをセット
        _jumpCooldownTimer = 0.1f;
        isGrounded = false;
        _wasGrounded = false;
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

    private void UpdateVisualDirection()
    {
        float inputThreshold = 0.1f;

        if (moveInput.x > inputThreshold)
        {
            transform.localScale = firstScale;
        }
        else if (moveInput.x < -inputThreshold)
        {
            transform.localScale = new Vector3(-firstScale.x, firstScale.y, firstScale.z);
        }
    }

    private void UpdateCollider(ColliderData data)
    {
        _collider.size = data.size;
        _collider.offset = data.offset;
        _collider.direction = data.direction;
    }

    private void OnDestroy()
    {
        _dashInvincibleCancelToken?.Cancel();
        _dashInvincibleCancelToken?.Dispose();
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