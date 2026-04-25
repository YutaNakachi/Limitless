using System;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class CollisionDetector : MonoBehaviour
{
    // 引数に Collider2D を渡すように定義
    [Serializable]
    public class TriggerEvent : UnityEvent<Collider2D> { }

    [SerializeField] private TriggerEvent onTriggerEnter = new TriggerEvent();
    [SerializeField] private TriggerEvent onTriggerStay = new TriggerEvent();

    // 2D用のイベントメソッド
    private void OnTriggerEnter2D(Collider2D other)
    {
        onTriggerEnter.Invoke(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        onTriggerStay.Invoke(other);
    }
}