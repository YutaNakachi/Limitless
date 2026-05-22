using UnityEngine;

public class PlayerStatus : MobStatus
{
    public override void GoToAttackStateIfPossible()
    {
        if (!IsAttackable) return;

        _state = StateEnum.Attack;
        _animator.SetTrigger("Kick");
    }

    protected override void OnDie()
    {
        Debug.Log("GameOver");
    }
}
