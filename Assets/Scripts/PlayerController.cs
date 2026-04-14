using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = 60;
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.dKey.isPressed)
        {
            this.transform.Translate(0.5f, 0, 0);
        }

        if (Keyboard.current.aKey.isPressed)
        {
            this.transform.Translate(-0.5f, 0, 0);
        }

    }
}
