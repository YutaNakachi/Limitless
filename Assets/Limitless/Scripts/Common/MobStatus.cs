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


    public virtual void TakeDamage(int damage)
    {
        if (IsInvincible) return;
        if (_state == StateEnum.Die) return;

        _life -= damage;
        if (_life > 0) return;

        _state = StateEnum.Die;
        _animator.SetTrigger("Death");
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