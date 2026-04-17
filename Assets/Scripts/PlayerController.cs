using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck; // 足元に配置する空のGameObject
    [SerializeField] private Vector2 checkSize = new Vector2(0.5f, 0.1f); // 判定エリアのサイズ
    [SerializeField] private LayerMask groundLayer; // Groundレイヤーだけを判定対象にする

    private Rigidbody2D _rigidbody;
    private bool isGrounded;

    private Vector2 moveInput;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        Application.targetFrameRate = 60;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        // 入力値を読み込む（Vector2のx成分が左右移動）
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        // ボタンが押された瞬間、かつ接地している場合
        if (context.started && isGrounded)
        {
            // 速度を一度リセットしてから飛ばすと、ジャンプ力が安定
            _rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocity.x, 0);
            _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    void Update()
    {

    }

    void FixedUpdate()
    {
        // 地面判定の実行
        // 指定した範囲(checkSize)内に、GroundレイヤーのColliderがあるかチェック
        isGrounded = Physics2D.OverlapBox(groundCheck.position, checkSize, 0f, groundLayer);

        // 左右移動の適用
        _rigidbody.linearVelocity = new Vector2(moveInput.x * moveSpeed, _rigidbody.linearVelocity.y);
    }

    // デバッグ用に判定エリアを画面に表示する
    private void OnDrawGizmos()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, checkSize);
    }
}
