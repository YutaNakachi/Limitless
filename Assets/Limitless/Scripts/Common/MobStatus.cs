using System.Collections;
using UnityEngine;

public abstract class MobStatus : MonoBehaviour
{
    protected enum StateEnum
    {
        Normal,
        Attack,
        Die
    }

    public bool IsMovable => _state == StateEnum.Normal;
    public bool IsAttackable => _state == StateEnum.Normal;
    public bool IsDead => _state == StateEnum.Die;

    // 🔥 イベント駆動用のAction
    public System.Action<int> OnTakeDamageEvent;     // 被弾時に発行（引数: ダメージ量）
    public System.Action<GameObject> OnDeathEvent; // 死亡時に発行（引数: 死亡したGameObject）

    [field: SerializeField] public float LifeMax { get; private set; } = 10;
    [SerializeField] protected float invincibilityDuration;

    public float Life => _life;

    protected Animator _animator;
    protected StateEnum _state = StateEnum.Normal;
    private float _life;

    public bool IsInvincible { get; protected set; } = true;

    protected virtual void Start()
    {
        _life = LifeMax;
        _animator = GetComponentInChildren<Animator>();

        LifeGaugeContainer.Instance.Add(this);
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

    public void SetMaxLife(float newMaxLife)
    {
        // すでにゲームが始まって初期化された後に呼ばれた場合も考慮し、
        // 最大HPの更新と同時に、現在のHPもその値に同期する
        LifeMax = newMaxLife;
        _life = newMaxLife;
    }

    public virtual void TakeDamage(int damage)
    {
        if (IsInvincible) return;
        if (IsDead) return;

        _life -= damage;

        // 🔥 【進化】生きている・死んだに関わらず、ダメージが通ったら一律通知
        OnTakeDamageEvent?.Invoke(damage);

        if (_life > 0) return;

        _state = StateEnum.Die;
        _animator.SetTrigger("Death");

        // 🔥 【進化】誰が死んだかを引数に入れて安全に通知
        OnDeathEvent?.Invoke(this.gameObject);
        OnDie();
    }

    protected virtual void OnDie()
    {
        LifeGaugeContainer.Instance.Remove(this);
    }

    public virtual void GoToAttackStateIfPossible()
    {
        if (!IsAttackable) return;

        _state = StateEnum.Attack;
        _animator.SetTrigger("Attack");
    }

    public virtual void GoToNormalStateIfPossible()
    {
        if (_state == StateEnum.Die) return;
        _state = StateEnum.Normal;
    }
}