using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MobStatus))]
public class MobAttack : MonoBehaviour
{
    [SerializeField] private int attackPower = 2;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private Collider2D attackCollider;
    [SerializeField] private AudioSource swingSound;

    private MobStatus _status;

    private void Awake()
    {
        _status = GetComponent<MobStatus>();
    }

    public void AttackIfPossible()
    {
        if (!_status.IsAttackable) return;

        _status.GoToAttackStateIfPossible();
    }

    public void OnAttackStart()
    {
        attackCollider.enabled = true;

        if (swingSound != null)
        {
            swingSound.pitch = Random.Range(0.7f, 1.3f);
            swingSound.Play();
        }
    }

    public void OnHitAttack(Collider2D collider)
    {
        MobStatus targetMob = collider.GetComponent<MobStatus>();
        if (targetMob == null) return;

        targetMob.TakeDamage(attackPower, transform.position);
    }

    public void OnAttackFinished()
    {
        attackCollider.enabled = false;
        StartCoroutine(CooldownCoroutine());
    }

    private IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSeconds(attackCooldown);
        _status.GoToNormalStateIfPossible();
    }
}
