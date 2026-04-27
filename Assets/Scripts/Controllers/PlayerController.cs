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

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private int maxNumOfAirJumps = 1;
    [SerializeField] private float dashJumpForce = 1.1f;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck; // 足元に配置する空のGameObject
    [SerializeField] private Vector2 checkSize = new Vector2(0.5f, 0.1f); // 判定エリアのサイズ
    [SerializeField] private LayerMask groundLayer; // Groundレイヤーだけを判定対象にする

    [Header("Collider Settings")]
    [SerializeField] private ColliderData normalCollider;
    [SerializeField] private ColliderData crouchCollider;
    [SerializeField] private ColliderData dashCollider;

    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private CapsuleCollider2D _collider;
    private bool isGrounded;
    private bool isCrouching;
    private bool isOnDash;

    private Vector2 moveInput;
    private Vector3 firstScale;
    private float currentVelocityX;
    private int remainingAirJumpCount;
    private float dashTimer;
    private float originalGravityScale; // 元の重力を保存する変数

    void Awake()
    {
        Application.targetFrameRate = 60;
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<CapsuleCollider2D>();
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
            isOnDash = true;
            dashTimer = dashDuration;

            // ダッシュ中は重力を0に
            _rigidbody.gravityScale = 0;

            // Y方向の速度を完全に殺し、真横だけの速度をセットする
            float direction = moveInput.x != 0 ? Mathf.Sign(moveInput.x) : Mathf.Sign(transform.localScale.x);
            _rigidbody.linearVelocity = new Vector2(direction * dashSpeed, 0);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (isGrounded || remainingAirJumpCount > 0)
            {
                if (isOnDash && isGrounded)
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
                else if (isGrounded)
                {
                    // 通常ジャンプ
                    currentVelocityX = _rigidbody.linearVelocityX;
                    _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce);
                }
                else
                {
                    // 空中でのジャンプ
                    currentVelocityX = _rigidbody.linearVelocityX;
                    _rigidbody.linearVelocity = new Vector2(currentVelocityX, jumpForce);
                    remainingAirJumpCount--;
                }

                _animator.SetTrigger("Jump");
            }
        }
    }

    public void OnCrounch(InputAction.CallbackContext context)
    {
        if (context.started && isGrounded)
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
        if (context.started)
        {
            _animator.SetTrigger("Kick");
        }
    }

    void Update()
    {
        // 地面判定の実行
        // 指定した範囲(checkSize)内に、GroundレイヤーのColliderがあるかチェック
        isGrounded = Physics2D.OverlapBox(groundCheck.position, checkSize, 0f, groundLayer);

        // ジャンプ回数復活
        if (isGrounded) remainingAirJumpCount = maxNumOfAirJumps;

        if (isOnDash)
        {
            HandleDash();
        }
        else
        {
            HandleNormalMovement();
        }

        // プレイヤーの向きを変更
        if (_rigidbody.linearVelocityX > 0)
        {
            transform.localScale = firstScale;
        }
        else if (_rigidbody.linearVelocityX < 0)
        {
            transform.localScale = new Vector3(-firstScale.x, firstScale.y, firstScale.z);
        }

        // AnimatorのParameterにStatusを渡す
        _animator.SetFloat("MoveSpeed", _rigidbody.linearVelocity.magnitude);
        _animator.SetBool("IsGrounded", isGrounded);
        _animator.SetBool("IsCrouching", isCrouching);
        _animator.SetBool("IsOnDash", isOnDash);
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
            float airControl = 5f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, airControl * Time.deltaTime);
        }

        _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);
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
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, checkSize);
    }
}
