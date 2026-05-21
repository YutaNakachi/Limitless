using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{

    [SerializeField] private Collider2D shootCollider;
    [Header("Shoot Settings")]
    // 💡 制限したい最大角度（例: 45度なら、水平から上下に45度の範囲内だけで狙える）
    [SerializeField, Range(0f, 90f)] private float maxShootAngle = 45f;
    [SerializeField, Range(0f, 45f)] private float neutralShootAngle = 5f;
    [SerializeField] private float shootForce = 15f;
    [SerializeField] private InputActionReference directionInput;

    public void OnShootStart()
    {
        shootCollider.enabled = true;
    }

    public void OnShoot(Collider2D collider)
    {
        if (!collider.CompareTag("Ball")) return;

        BallAbility ballInRange = collider.GetComponent<BallAbility>();

        // 1. Input System から現在の方向入力を取得
        Vector2 inputDir = directionInput.action.ReadValue<Vector2>();

        Vector3 shootDirection = Vector3.zero;

        // プレイヤーの現在の向き（右なら 1、左なら -1）
        float faceDirection = Mathf.Sign(transform.localScale.x);

        // 2. キー入力が一定以上ある場合（狙いを定めているとき）
        if (inputDir.sqrMagnitude > 0.05f)
        {
            float currentAngleRad = Mathf.Atan2(inputDir.y, inputDir.x);
            float currentAngleDeg = currentAngleRad * Mathf.Rad2Deg;

            if (faceDirection > 0) // 右向きのとき
            {
                currentAngleDeg = Mathf.Clamp(currentAngleDeg, -maxShootAngle, maxShootAngle);
            }
            else // 左向きのとき
            {
                if (currentAngleDeg < 0) currentAngleDeg += 360f;
                currentAngleDeg = Mathf.Clamp(currentAngleDeg, 180f - maxShootAngle, 180f + maxShootAngle);
            }

            float finalRad = currentAngleDeg * Mathf.Deg2Rad;
            shootDirection = new Vector3(Mathf.Cos(finalRad), Mathf.Sin(finalRad), 0f);
        }
        // 3. 🔥 キー入力がない場合（ニュートラル：若干斜め上へ飛ばす）
        else
        {
            // 向き（左右）に応じて、ニュートラルの角度を決定する
            float targetAngleDeg = 0f;

            if (faceDirection > 0)
            {
                // 右向きなら、真横（0度）からプラス方向に傾ける
                targetAngleDeg = neutralShootAngle;
            }
            else
            {
                // 左向きなら、真横（180度）からマイナス方向に傾ける（上向きにするため）
                targetAngleDeg = 180f - neutralShootAngle;
            }

            // 角度から方向ベクトルを生成
            float finalRad = targetAngleDeg * Mathf.Deg2Rad;
            shootDirection = new Vector3(Mathf.Cos(finalRad), Mathf.Sin(finalRad), 0f);
        }

        // 4. 発射！
        ballInRange.Fire(shootDirection, shootForce);

        Debug.Log($"シュート！ 方向: {shootDirection} (角度: {Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg}度)");
    }

    public void OnShootFinished()
    {
        shootCollider.enabled = false;
    }
}