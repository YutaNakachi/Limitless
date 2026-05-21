using System.Collections;
using UnityEngine;

public class OneWayPlatform : MonoBehaviour
{
    private Collider2D _platformCollider;

    void Awake()
    {
        _platformCollider = GetComponent<CompositeCollider2D>();
    }

    // 🔥 プレイヤー側から直接このメソッドを呼び出す
    public void PassThrough(Collider2D playerCollider)
    {
        StartCoroutine(PassThroughCoroutine(playerCollider));
    }

    private IEnumerator PassThroughCoroutine(Collider2D playerCollider)
    {
        // 衝突判定を一時的に無効化
        Physics2D.IgnoreCollision(playerCollider, _platformCollider, true);

        // 完全にすり抜けるまで待つ（少し長めの0.3秒に調整）
        yield return new WaitForSeconds(0.3f);

        // 衝突判定を元に戻す
        if (playerCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, _platformCollider, false);
        }
    }
}