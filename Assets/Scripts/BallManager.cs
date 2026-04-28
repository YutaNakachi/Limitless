using UnityEngine;

public class BallManager : MonoBehaviour
{
    private Rigidbody2D _rigidbody;
    private BallController _ball;

    [SerializeField] PlayerController _playerController;

    [SerializeField] private float orbitRadius = 2f;
    [SerializeField] private float rotateSpeed = 100f;
    private float currentAngle;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _ball = GetComponent<BallController>();
        GetComponent<Collider2D>().enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        currentAngle -= rotateSpeed * Time.deltaTime;
        UpdateBallPositions();
    }
    void UpdateBallPositions()
    {
        // 各ボールの角度を均等に割り振る（360度 / ボール数）

        float rad = currentAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orbitRadius;
        Orbit(_playerController.transform.position + offset + new Vector3(0, _playerController.GetComponent<CapsuleCollider2D>().size.y / 2 - 0.2f, 0));
    }

    public void Orbit(Vector3 targetPosition)
    {
        transform.position = targetPosition;
    }
}
