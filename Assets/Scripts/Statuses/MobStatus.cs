using System.Collections;
using UnityEngine;

public abstract class MobStatus : MonoBehaviour
{
    [Header("Base Status")]
    [SerializeField] protected int maxHp = 10;
    protected int currentHp;

    [SerializeField] protected float invincibilityDuration;

    public bool IsInvincible { get; protected set; } = true;

    protected virtual void Start()
    {
        currentHp = maxHp;
        StartCoroutine(StartInvincibilityRoutine());
    }

    // 無敵タイマー
    protected virtual IEnumerator StartInvincibilityRoutine()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        IsInvincible = false;

        OnInvincibilityEnd();
    }

    // 無敵が終わったら見た目を変える演出など
    protected virtual void OnInvincibilityEnd() { }


    public virtual void TakeDamage(int damage)
    {
        if (IsInvincible) return;

        currentHp -= damage;
        Debug.Log($"{gameObject.name} に {damage} のダメージ！ 残りHP: {currentHp}");

        if (currentHp <= 0)
        {
            OnDie();
        }
    }

    protected abstract void OnDie();
}