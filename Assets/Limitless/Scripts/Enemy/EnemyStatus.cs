using System.Collections;
using UnityEngine;

public class EnemyStatus : MobStatus
{
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