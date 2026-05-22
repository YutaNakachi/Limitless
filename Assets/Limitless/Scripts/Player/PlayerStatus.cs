using UnityEngine;

public class PlayerStatus : MobStatus
{
    [SerializeField] private Collider2D shootCollider;

    public override void GoToAttackStateIfPossible()
    {
        if (!IsAttackable) return;

        _state = StateEnum.Attack;
        _animator.SetTrigger("Kick");
    }

    public override void GoToNormalStateIfPossible()
    {
        if (_state == StateEnum.Die) return;
        _state = StateEnum.Normal;
        shootCollider.enabled = false;
    }

    protected override void OnDie()
    {
        Debug.Log("GameOver");
    }
}
