using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    private Rigidbody2D _rigidbody;
    private Vector2 moveInput;
    private bool isGrounded;

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
            _rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    void Update()
    {

    }

    void FixedUpdate()
    {
        // 左右移動の適用
        _rigidbody.linearVelocity = new Vector2(moveInput.x * moveSpeed, _rigidbody.linearVelocity.y);
    }

    // 地面判定（簡易版）
    private void OnCollisionStay2D(Collision2D collision)
    {
        isGrounded = true;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}
