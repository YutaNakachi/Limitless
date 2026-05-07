using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private Collider2D shootCollider;
    [SerializeField] private float shootForce = 15f;
    [SerializeField] private float angle = 45.0f; // 飛ばしたい角度（度）
    private float rad => angle * Mathf.Deg2Rad; // 度をラジアンに変換

    public void OnShootStart()
    {
        shootCollider.enabled = true;
    }

    public void OnShoot(Collider2D collider)
    {
        if (!collider.CompareTag("Ball")) return;

        BallController ballInRange = collider.GetComponent<BallController>();

        //Vector2 direction = (ballInRange.transform.position - transform.position).normalized;

        // プレイヤーよりボールが左なら -1、右なら 1 を掛ける
        float side = (ballInRange.transform.position.x < transform.position.x) ? -1f : 1f;

        // X = Cos, Y = Sin で方向ベクトルを作る
        Vector3 direction = new Vector3(Mathf.Cos(rad) * side, Mathf.Sin(rad));

        ballInRange.ShotBall(direction, shootForce);

        Debug.Log("ナイスキック！");
    }

    public void OnShootFinished()
    {
        shootCollider.enabled = false;
    }
}