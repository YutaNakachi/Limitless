using UnityEngine;

public class PlayerStatus : MobStatus
{
    protected override void OnDie()
    {
        Debug.Log("GameOver");
    }
}
