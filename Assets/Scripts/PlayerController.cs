using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float dashSpeed = 10f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck; // 足元に配置する空のGameObject
    [SerializeField] private Vector2 checkSize = new Vector2(0.5f, 0.1f); // 判定エリアのサイズ
    [SerializeField] private LayerMask groundLayer; // Groundレイヤーだけを判定対象にする

    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private bool isGrounded;
    private bool isCrouching;

    private Vector2 moveInput;
    private Vector3 firstScale;
    private float currentVelocityX;


    void Awake()
    {
        Application.targetFrameRate = 60;
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        firstScale = transform.localScale;
        _animator.SetTrigger("Intro");
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // 左右の入力方向を特定（入力がない場合は正面など、方向を決める）
            Vector2 dashDirection = new Vector2(moveInput.x, 0).normalized;

            // 入力が完全に0の時は、キャラが向いている方向に飛ばすと親切
            if (dashDirection.sqrMagnitude < 0.01f)
            {
                dashDirection = new Vector2(transform.localScale.x, 0);
            }

            // 瞬間的な力を加える
            _rigidbody.AddForce(dashDirection * dashSpeed, ForceMode2D.Impulse);

            _animator.SetTrigger("Dash");
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        // ボタンが押された瞬間、かつ接地している場合
        if (context.started && isGrounded)
        {
            // 速度を一度リセットしてから飛ばすと、ジャンプ力が安定
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0);
            _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            _animator.SetTrigger("Jump");
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

    public void OnAttack(InputAction.CallbackContext context)
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

        if (isGrounded)
        {
            if (isCrouching)
            {
                // しゃがみ中は強制的に横移動を 0 に
                currentVelocityX = 0;
            }
            else
            {
                // 通常の移動
                currentVelocityX = moveInput.x * moveSpeed;
            }
        }
        // 空中にいるときは linearVelocity.x をいじらない（＝慣性で進む）
        else
        {
            float airControl = 5f;
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, moveInput.x * moveSpeed, airControl * Time.deltaTime);
        }

        _rigidbody.linearVelocity = new Vector2(currentVelocityX, _rigidbody.linearVelocity.y);


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
    }

    // デバッグ用に判定エリアを画面に表示する
    private void OnDrawGizmos()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, checkSize);
    }
}
