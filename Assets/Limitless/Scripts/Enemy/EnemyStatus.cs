using System.Collections;
using UnityEngine;

public class EnemyStatus : MobStatus
{
    protected override void OnDie()
    {
        base.OnDie(); // 親のOnDeathEvent等がここで走るためマネージャーへ通知が届きます
        StartCoroutine(DestroyCoroutine());
    }

    private IEnumerator DestroyCoroutine()
    {
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }
}