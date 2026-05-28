using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class CollisionDetector : MonoBehaviour
{
    [Serializable]
    public class TriggerEvent : UnityEvent<Collider2D> { }

    [Header("🛸 Trigger Events (すり抜け / 敵 / エリアなど)")]
    [SerializeField] private TriggerEvent onTriggerEnter = new TriggerEvent();
    [SerializeField] private TriggerEvent onTriggerStay = new TriggerEvent();

    [Header("🧱 Collision Events (物理衝突 / 壁 / 地面など)")]
    [SerializeField] private TriggerEvent onCollisionEnter = new TriggerEvent();
    [SerializeField] private TriggerEvent onCollisionStay = new TriggerEvent();

    // ==========================================
    // 1. トリガー検知ルート
    // ==========================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        onTriggerEnter.Invoke(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        onTriggerStay.Invoke(other);
    }

    // ==========================================
    // 2. 物理衝突検知ルート（イベントを明確に分離）
    // ==========================================
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 物理衝突の相手コライダーを、Collision専用イベントに飛ばす
        onCollisionEnter.Invoke(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        onCollisionStay.Invoke(collision.collider);
    }
}