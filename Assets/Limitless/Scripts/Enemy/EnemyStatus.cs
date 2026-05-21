using System.Collections;
using UnityEngine;

public class EnemyStatus : MobStatus
{
    [Header("Enemy Common Settings")]
    [SerializeField] protected int scoreValue = 10;
    [SerializeField] protected int attackPower = 10;
    [SerializeField] protected GameObject deathEffectPrefab;


    // Enemy共通の死亡時処理
    protected override void OnDie()
    {
        base.OnDie();
        StartCoroutine(DestroyCoroutine());
    }

    private IEnumerator DestroyCoroutine()
    {
        yield return new WaitForSeconds(3);
        Destroy(gameObject);
    }
}