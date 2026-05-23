using UnityEngine;

public class PlayerStatus : MobStatus
{
    [SerializeField] private Collider2D shootCollider;

    protected override void Start()
    {
        base.Start();

        // 🔥 自分がダメージを受けた時に、自動で無敵化ルーチンが走るようにイベントを登録
        OnTakeDamageEvent += (damage) =>
        {
            if (!IsDead && invincibilityDuration > 0f)
            {
                StartCoroutine(StartInvincibilityRoutine());
            }
        };
    }

    public override void GoToAttackStateIfPossible()
    {
        if (!IsAttackable) return;

        _state = StateEnum.Attack;
        _animator.SetTrigger("Kick");
    }

    public override void GoToNormalStateIfPossible()
    {
        if (_state == StateEnum.Die || _state == StateEnum.Knockback) return;
        _state = StateEnum.Normal;
        if (shootCollider != null) shootCollider.enabled = false;
    }

    protected override void OnDie()
    {
        base.OnDie();
        Debug.Log("GameOver");
    }
}