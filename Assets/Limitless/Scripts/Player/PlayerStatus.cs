using DG.Tweening; // 👈 DOTweenを使用
using System.Collections;
using UnityEngine;

public class PlayerStatus : MobStatus
{
    [SerializeField] private Collider2D shootCollider;

    public bool isOnMurasaki = false;

    protected override void Start()
    {
        base.Start();

        // 👈 仕様：プレイヤーの被弾時のみ、無敵時間と視覚的変化（点滅）を適用する
        OnTakeDamageEvent += (damage) =>
        {
            if (!IsDead && invincibilityDuration > 0f)
            {
                StartCoroutine(PlayerInvincibilityRoutine());
            }
        };
    }

    /// <summary>
    /// 👈 仕様：プレイヤー専用の「点滅を伴う」無敵時間コルーチン（DOTween使用）
    /// </summary>
    private IEnumerator PlayerInvincibilityRoutine()
    {
        IsInvincible = true;

        // DOTweenで半透明(0.3)と不透明(1.0)を高速往復（Yoyo）させて点滅を表現
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOFade(0.3f, 0.1f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject); // メモリリーク防止の安全装置
        }

        yield return new WaitForSeconds(invincibilityDuration);

        // 無敵時間が終わったら点滅を安全に終了して元の見た目に戻す
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill();
            _spriteRenderer.DOFade(1.0f, 0f);
        }

        IsInvincible = false;
    }

    public override void GoToAttackStateIfPossible()
    {
        if (!IsAttackable) return;
        _state = StateEnum.Attack;
    }

    public override void GoToNormalStateIfPossible()
    {
        if (shootCollider != null) shootCollider.enabled = false;
        if (_state == StateEnum.Die || _state == StateEnum.Knockback || isOnMurasaki) return;
        _state = StateEnum.Normal;
    }

    protected override void OnDie()
    {
        base.OnDie();

        StartCoroutine(DeathCoroutine());
    }

    private IEnumerator DeathCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
        FxManager.Instance.Play("PlayerGameOver", transform);
        _animator.SetTrigger("Death");
        _animator.SetBool("IsDead", IsDead);
        SoundManager.Instance.PlaySEAtPosition("PlayerDeath", transform.position);

        Debug.Log("GameOver");
    }
}