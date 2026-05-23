using DG.Tweening;
using System.Collections;
using UnityEngine;

public abstract class MobStatus : MonoBehaviour
{
    protected enum StateEnum
    {
        Normal,
        Attack,
        Knockback, // 🔥 ノックバック状態を追加
        Die
    }

    // 🔥 ノックバック中も移動や攻撃を制限する
    public bool IsMovable => _state == StateEnum.Normal;
    public bool IsAttackable => _state == StateEnum.Normal;
    public bool IsDead => _state == StateEnum.Die;

    // イベント駆動用のAction
    public System.Action<int> OnTakeDamageEvent;
    public System.Action<GameObject> OnDeathEvent;

    [field: SerializeField] public float LifeMax { get; private set; } = 10;
    [SerializeField] protected float invincibilityDuration;

    [Header("ーー ノックバック設定 ーー")]
    [SerializeField] private float knockbackDuration = 0.2f; // 吹き飛んでいる時間
    [SerializeField] private float knockbackForce = 12f;    // 吹き飛ぶ強さ

    public float Life => _life;

    protected Animator _animator;
    private SpriteRenderer _spriteRenderer;
    protected StateEnum _state = StateEnum.Normal;
    private float _life;
    protected Rigidbody2D _rb; // 物理演算用の参照

    public bool IsInvincible { get; protected set; } = true;

    protected virtual void Start()
    {
        _life = LifeMax;
        _animator = GetComponentInChildren<Animator>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>(); // ここで自動取得

        LifeGaugeContainer.Instance.Add(this);
        StartCoroutine(StartInvincibilityRoutine());
    }

    // 無敵タイマー
    protected virtual IEnumerator StartInvincibilityRoutine()
    {
        IsInvincible = true;

        // 🔥 DOTweenで点滅を開始
        // 引数: (目標にするアルファ値, 1回のフェードにかける秒数)
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(0.3f, 0.1f)
                .SetLoops(-1, LoopType.Yoyo) // -1で無限ループ、Yoyoで「行って戻る」
                .SetLink(gameObject);        // オブジェクトが消滅した時にTweenも自動破棄する安全装置
        }

        // 無敵時間分だけ待つ
        yield return new WaitForSeconds(invincibilityDuration);

        // 🔥 無敵時間が終わったら点滅を止めて、元の不透明度(1.0)に綺麗に戻す
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill(); // 動いているDOFadeループを強制終了
            _spriteRenderer.DOFade(1.0f, 0f); // 瞬時に元の不透明度に戻す
        }

        IsInvincible = false;
        OnInvincibilityEnd();
    }

    protected virtual void OnInvincibilityEnd() { }

    public void SetMaxLife(float newMaxLife)
    {
        LifeMax = newMaxLife;
        _life = newMaxLife;
    }

    /// <summary>
    /// 🔥 【大改造】引数に attackerPosition を追加
    /// </summary>
    public virtual void TakeDamage(int damage, Vector2 attackerPosition)
    {
        if (IsInvincible) return;
        if (IsDead) return;

        _life -= damage;

        // ダメージ通知
        OnTakeDamageEvent?.Invoke(damage);

        if (_life > 0)
        {
            // 生きていればノックバックを発動（追撃時はスキップする安全設計）
            TriggerKnockback(attackerPosition);
            return;
        }

        // 死亡処理
        _state = StateEnum.Die;
        _animator.SetTrigger("Death");
        if (_rb != null) _rb.linearVelocity = Vector2.zero; // 死亡時はその場に止める

        OnDeathEvent?.Invoke(this.gameObject);
        OnDie();
    }

    protected virtual void OnDie()
    {
        LifeGaugeContainer.Instance.Remove(this);
    }

    /// <summary>
    /// 🔥 【新設】ノックバックの物理トリガー（多重起動防止ガード付き）
    /// </summary>
    private void TriggerKnockback(Vector2 attackerPosition)
    {
        if (_rb == null) return;

        // すでにノックバック中なら、速度の上書きやコルーチンの多重起動をスキップ
        if (_state == StateEnum.Knockback) return;

        _state = StateEnum.Knockback;

        // 攻撃者とは逆の方向（ベクトル）を計算
        Vector2 knockbackDir = ((Vector2)transform.position - attackerPosition).normalized;

        // 瞬間的な速度変化を与える
        _rb.linearVelocity = knockbackDir * knockbackForce;

        // 復帰ルーチンを開始
        StartCoroutine(KnockbackRoutine());
    }

    private IEnumerator KnockbackRoutine()
    {
        yield return new WaitForSeconds(knockbackDuration);

        if (_state != StateEnum.Die)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero; // 吹き飛びの慣性をピタッと止める
            _state = StateEnum.Normal; // 通常状態に安全に復帰
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
        // 🔥 ノックバック中も通常状態へのリセットをガードする
        if (_state == StateEnum.Die || _state == StateEnum.Knockback) return;
        _state = StateEnum.Normal;
    }
}