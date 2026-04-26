using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private float shootForce = 15f;
    [SerializeField] private Collider2D shootCollider;

    public void OnShootStart()
    {
        shootCollider.enabled = true;
    }

    public void OnShoot(Collider2D collider)
    {
        if (!collider.CompareTag("Ball")) return;

        BallController ballInRange = collider.GetComponent<BallController>();

        Vector2 direction = (ballInRange.transform.position - transform.position).normalized;
        ballInRange.ShotBall(direction, shootForce);

        Debug.Log("ナイスシュート！");
    }

    public void OnShootFinished()
    {
        shootCollider.enabled = false;
    }
}