using System.Collections;
using UnityEngine;

public abstract class MobStatus : MonoBehaviour
{
    protected enum StateEnum
    {
        Normal,
        Attack,
        Knockback, // 👈 仕様：ノックバック状態を追加（行動制限用）
        Intro,
        Die
    }

    // 👈 仕様：ノックバック中（Knockback）も移動や攻撃を制限する
    public bool IsMovable => _state == StateEnum.Normal;
    public bool IsAttackable => _state == StateEnum.Normal;
    public bool IsKnockbacking => _state == StateEnum.Knockback;
    public bool IsInIntroMotion => _state == StateEnum.Intro;
    public bool IsDead => _state == StateEnum.Die;

    public System.Action<int> OnTakeDamageEvent;
    public System.Action<GameObject> OnDeathEvent;

    [field: SerializeField] public float LifeMax { get; private set; } = 10;
    [SerializeField] protected float invincibilityDuration;

    // 👈 仕様：ノックバックの時間・のけぞる距離（力）をインスペクターで調整可能に
    [Header("ーー ノックバック調整パラメーター ーー")]
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private float knockbackForce = 12f;

    [Header("ーー 連続ヒットストップ制御 ーー")]
    [Tooltip("連続ヒットとみなす猶予時間。この時間内に次のヒットが来たらヒットストップをスキップします")]
    [SerializeField] private float comboResetDuration = 0.5f;

    private float _lastHitTime = -999f; // 最後にダメージを受けた時間
    private bool _isComboHitting = false; // 現在連続ヒット中かどうかのフラグ

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

    public void SetIntroState()
    {
        _state = StateEnum.Intro;
    }

    public void SetMaxLife(float newMaxLife)
    {
        LifeMax = newMaxLife;
        _life = newMaxLife;
    }

    public void SetInvicible()
    {
        IsInvincible = true;
    }

    public void CancelInvicible()
    {
        IsInvincible = false;
    }

    /// <summary>
    /// 👈 仕様：どこから攻撃されたかの座標（attackerPosition）を受け取る
    /// </summary>
    public virtual void TakeDamage(int damage, Vector2 attackerPosition)
    {
        if (IsInvincible) return;
        if (IsDead) return;

        _life -= damage;
        _animator.SetTrigger("Hit");

        // 🛠️ 1. 連続ヒットの判定チェック
        // 現在の時間と、最後に当たった時間の差が設定値（0.5秒）以内なら連続ヒットとみなす
        if (Time.time - _lastHitTime <= comboResetDuration)
        {
            _isComboHitting = true;
        }
        else
        {
            // 時間が空いていれば、連続ヒット状態は終了（リセット）している
            _isComboHitting = false;
        }

        // 最後に当たった時間を現在時刻に更新
        _lastHitTime = Time.time;

        // 2. 実際のダメージ処理（既存の処理）
        // hp -= damage; などの処理...

        // 🛠️ 3. ヒットストップの実行制御
        if (!_isComboHitting)
        {
            // 🔥 1回目（単発、またはコンボの初撃）だけヒットストップを走らせる！
            FxManager.Instance.Play("Damaged", transform);
            Debug.Log($"💥 初撃ヒット！ヒットストップを実行します。 (Damage: {damage})");
        }
        else
        {
            // 2回目以降の連続ヒット時は、ヒットストップをスキップしてログだけ出す
            Debug.Log($"⚔️ 連続ヒット中につきヒットストップをスキップ: (Damage: {damage})");
        }

        OnTakeDamageEvent?.Invoke(damage);

        if (_life > 0)
        {
            // 生きていればノックバックを発生させる
            TriggerKnockback(attackerPosition);
            return;
        }

        // 死亡処理
        _state = StateEnum.Die;
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

        // すでにノックバック中なら、ダメージは受けるが連続でノックバックはしない
        if (_state == StateEnum.Knockback) return;

        _state = StateEnum.Knockback;

        // 🔥 1. 衝突前の慣性を完全にリセット（これがないと突進の力と相殺して吹き飛びません）
        _rigidbody.linearVelocity = Vector2.zero;

        // 攻撃者とは逆の方向（ベクトル）を計算
        Vector2 knockbackDir = ((Vector2)transform.position - attackerPosition).normalized;

        // 🔥 2. AddForce の Impulse モードで瞬間的に「ドンッ！」とのけぞらせる
        _rigidbody.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);

        StartCoroutine(KnockbackRoutine());
    }

    private IEnumerator KnockbackRoutine()
    {
        yield return new WaitForSeconds(knockbackDuration);

        if (_state != StateEnum.Die)
        {
            // ノックバック時間が終了したら、物理的な吹き飛びの勢いをピタッと止める
            if (_rigidbody != null) _rigidbody.linearVelocity = Vector2.zero;

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