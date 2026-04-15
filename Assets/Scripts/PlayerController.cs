using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D _rigidbody;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = 60;
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.dKey.isPressed)
        {
            _rigidbody.linearVelocity = Vector2.right * 5f;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            _rigidbody.linearVelocity = Vector2.left * 5f;

        }
    }
}
