using UnityEngine;

public class GhostController : MonoBehaviour
{
    [SerializeField] private float ghostSpeed = 2f;    // 移動の速さ
    [SerializeField] private float frequency = 0.5f; // ゆらぎの細かさ

    private Rigidbody2D _rigidbody;
    private float seedX;
    private float seedY;

    private bool isInvincible;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        // 個体ごとに動きをズラすためのシード値
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
    }

    void FixedUpdate()
    {
        // 時間経過とシード値に基づいて滑らかな乱数(0〜1)を取得
        // Mathf.PerlinNoise(x, y)
        float noiseX = Mathf.PerlinNoise(Time.time * frequency + seedX, 0) * 2 - 1; // -1〜1に変換
        float noiseY = Mathf.PerlinNoise(0, Time.time * frequency + seedY) * 2 - 1;

        Vector2 movement = new Vector2(noiseX, noiseY) * ghostSpeed;

        // 物理演算として速度を適用
        _rigidbody.linearVelocity = movement;
    }
}
