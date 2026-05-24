using System.Collections;
using UnityEngine;

public abstract class MobStatus : MonoBehaviour
{
    protected enum StateEnum
    {
        Normal,
        Attack,
        Knockback, // 👈 仕様：ノックバック状態を追加（行動制限用）
        Die
    }

    // 👈 仕様：ノックバック中（Knockback）も移動や攻撃を制限する
    public bool IsMovable => _state == StateEnum.Normal;
    public bool IsAttackable => _state == StateEnum.Normal;
    public bool IsDead => _state == StateEnum.Die;

    public System.Action<int> OnTakeDamageEvent;
    public System.Action<GameObject> OnDeathEvent;

    [field: SerializeField] public float LifeMax { get; private set; } = 10;
    [SerializeField] protected float invincibilityDuration;

    // 👈 仕様：ノックバックの時間・のけぞる距離（力）をインスペクターで調整可能に
    [Header("ーー ノックバック調整パラメーター ーー")]
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private LayerMask groundLayer;

    public float Life => _life;

    protected Animator _animator;
    protected StateEnum _state = StateEnum.Normal;
    private float _life;
    protected Rigidbody2D _rigidbody;
    protected SpriteRenderer _spriteRenderer;

    // 👈 仕様：初期状態は無敵にしない（敵が即座に攻撃を受け付けるため）
    public bool IsInvincible { get; protected set; } = false;

    protected virtual void Start()
    {
        _life = LifeMax;
        _animator = GetComponentInChildren<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        LifeGaugeContainer.Instance.Add(this);
    }

    public void SetMaxLife(float newMaxLife)
    {
        LifeMax = newMaxLife;
        _life = newMaxLife;
    }

    /// <summary>
    /// 👈 仕様：どこから攻撃されたかの座標（attackerPosition）を受け取る
    /// </summary>
    public virtual void TakeDamage(int damage, Vector2 attackerPosition)
    {
        if (IsInvincible) return;
        if (IsDead) return;

        _life -= damage;
        OnTakeDamageEvent?.Invoke(damage);

        if (_life > 0)
        {
            // 生きていればノックバックを発生させる
            TriggerKnockback(attackerPosition);
            return;
        }

        // 死亡処理
        _state = StateEnum.Die;
        _animator.SetTrigger("Death");
        if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero;

        OnDeathEvent?.Invoke(this.gameObject);
        OnDie();
    }

    protected virtual void OnDie()
    {
        LifeGaugeContainer.Instance.Remove(this);
    }

    /// <summary>
    /// 物理的なのけぞり処理
    /// </summary>
    private void TriggerKnockback(Vector2 attackerPosition)
    {
        if (_rigidbody == null) return;

        // 👈 仕様：すでにノックバック中なら、ダメージは受けるが連続でノックバック（上書き）はしない
        if (_state == StateEnum.Knockback) return;

        _state = StateEnum.Knockback;

        // 攻撃者とは逆の方向（ベクトル）を計算
        Vector2 knockbackDir = ((Vector2)transform.position - attackerPosition).normalized;
        Vector2 targetVelocity = knockbackDir * knockbackForce;

        // 🔥 【大修正】ノックバックの速度に対しても、壁センサーを働かせる！
        float checkDistance = targetVelocity.magnitude * 0.1f + 0.05f;
        float radius = 0.3f; // キャラクターの物理サイズ（インスペクターのコライダーに合わせて調整）

        RaycastHit2D hit = Physics2D.CircleCast(
            transform.position,
            radius,
            knockbackDir,
            checkDistance,
            groundLayer
        );

        // もし吹き飛ぶ方向に壁を検知したら、壁に沿って滑る速度（あるいは停止）に補正する
        if (hit.collider != null)
        {
            Vector2 projection = Vector2.Dot(targetVelocity, hit.normal) * hit.normal;
            targetVelocity = targetVelocity - projection; // 壁に沿う速度に減速
        }

        // 安全に補正された速度で吹き飛ばす
        _rigidbody.linearVelocity = targetVelocity;

        StartCoroutine(KnockbackRoutine());
    }

    private IEnumerator KnockbackRoutine()
    {
        yield return new WaitForSeconds(knockbackDuration);

        if (_state != StateEnum.Die)
        {
            if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero; // 吹き飛び停止
            _state = StateEnum.Normal; // 通常状態へ安全に復帰
        }
    }

    public virtual void GoToAttackStateIfPossible()
    {
        if (!IsAttackable) return;
        _state = StateEnum.Attack;
        _animator.SetTrigger("Attack");
    }

    public virtual void GoToNormalStateIfPossible()
    {
        if (_state == StateEnum.Die || _state == StateEnum.Knockback) return;
        _state = StateEnum.Normal;
    }
}