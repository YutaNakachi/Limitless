public class NormalBallAbility : BallAbility
{
    protected override void OnFire()
    {
        FxManager.Instance.Play("NormalBallKick", transform);
    }
}
