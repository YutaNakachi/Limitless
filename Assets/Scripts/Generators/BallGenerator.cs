using UnityEngine;

public class BallGenerator : ObjectGenerator
{
    [SerializeField] private GameObject redBallPrefab;
    [SerializeField] private GameObject blueBallPrefab;

    private GameObject redBall;
    private GameObject blueBall;

    public Vector3 redBallPosition;
    public Vector3 blueBallPosition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        redBall = GenerateObject(redBallPrefab, redBallPosition);
        blueBall = GenerateObject(blueBallPrefab, blueBallPosition);
    }
}
