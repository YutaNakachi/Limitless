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


    public float Life => _life;

    protected Animator _animator;
    protected StateEnum _state = StateEnum.Normal;
    private float _life;
    protected Rigidbody2D _rigidbody;
    protected SpriteRenderer _spriteRenderer;

    // 👈 仕様：初期状態は無敵にしない（敵が即座に攻撃を受け付けるため）
    public bool IsInvincible { get; protected set; } = false;

    // 💡【追加】カメラキャッシュ用の変数
    private Camera _mainCamera;

    protected virtual void Start()
    {
        _life = LifeMax;
        _animator = GetComponentInChildren<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 💡【追加】メインカメラをキャッシュ
        _mainCamera = Camera.main;

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
        // 💡【追加】もともとの無敵フラグが立っているか、もしくはカメラ外ならダメージを通さない
        if (IsInvincible || IsOutOfCamera()) return;
        if (IsDead) return;

        _life -= damage;
        _animator.SetTrigger("Hit");
        SoundManager.Instance.PlaySEAtPosition("Hit", transform.position);

        FxManager.Instance.Play("Damaged", transform);

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
        SoundManager.Instance.PlaySEAtPosition("Death", transform.position);

        OnDeathEvent?.Invoke(this.gameObject);
        OnDie();
    }

    /// <summary>
    /// ❤️【新規追加】体力を指定量だけ安全に回復する（最大値を超えない）
    /// </summary>
    public virtual void Heal(int healAmount)
    {
        if (IsDead) return;

        // 体力を加算し、最大値（LifeMax）を超えないように制限する
        _life = Mathf.Min(LifeMax, _life + healAmount);

        Debug.Log($"❤️ {gameObject.name} が {healAmount} 回復！ 現在のHP: {_life}/{LifeMax}");
    }

    // 💡【追加】カメラの外にいるかどうかを判定するプライベートメソッド
    public bool IsOutOfCamera()
    {
        if (_mainCamera == null) return false;

        Vector3 viewPos = _mainCamera.WorldToViewportPoint(transform.position);

        // 画面の上下左右の外側に出ているかをチェック
        return viewPos.x < 0f || viewPos.x > 1f || viewPos.y < 0f || viewPos.y > 1f;
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

    /// <summary>
    /// 🛠️【新規追加】術式「蒼」などの継続的な吸引によって、強制的に行動不能ステートにする
    /// </summary>
    public void ForceApplyPullState()
    {
        if (IsDead) return;

        // すでにノックバック中などの場合はコルーチン等による復帰を防ぐために
        // ステートを強制上書きして、AI移動（IsMovable）を完全に封じます
        _state = StateEnum.Knockback;
    }

    /// <summary>
    /// 🛠️【新規追加】術式「蒼」などの強制拘束ステートを完全に解除し、通常状態へ引き戻す
    /// </summary>
    public void ForceResetToNormalState()
    {
        if (IsDead) return; // 死亡している場合は流石に除外

        _state = StateEnum.Normal;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero; // 変な慣性を残さないようにピタッと止める
        }
    }
}