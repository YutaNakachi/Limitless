using System.Collections;
using UnityEngine;

public class EnemyStatus : MobStatus
{
    // 👈 仕様：「敵のノックバックには無敵時間なし」
    // EnemyStatus側には何も追加の無敵イベントを登録しないため、親クラスの「初期状態無敵なし(IsInvincible=false)」が維持されます。

    protected override void OnDie()
    {
        base.OnDie();
        StartCoroutine(DestroyCoroutine());
    }

    private IEnumerator DestroyCoroutine()
    {
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }
}