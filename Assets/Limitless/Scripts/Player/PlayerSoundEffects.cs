using UnityEngine;

public class PlayerSoundEffects : MonoBehaviour
{
    public void PlayRunSound()
    {
        SoundManager.Instance.PlaySEAtPosition("Run", transform.position);

    }

    public void PlayJumpSound()
    {
        SoundManager.Instance.PlaySEAtPosition("Jump", transform.position);
    }

    public void PlayIntroSound()
    {
        SoundManager.Instance.PlaySEAtPosition("Intro", transform.position);
    }

    public void PlayKickSwingSound()
    {
        SoundManager.Instance.PlaySEAtPosition("KickSwing", transform.position);
    }
}
