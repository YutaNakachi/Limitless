using UnityEngine;

public class ResetStateOnExit : StateMachineBehaviour
{
    // 💡 このステート（Kickモーション）から抜けた時に、Unityが自動で確実に呼び出してくれる
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // アニメーションが付いているオブジェクト（または親）から MobStatus を取得
        MobStatus status = animator.GetComponentInParent<MobStatus>();

        if (status != null)
        {
            // 強制的に状態をNormalに戻す
            status.GoToNormalStateIfPossible();
            Debug.Log("🛡️ [StateMachineBehaviour] モーション中断を検知。ステートをNormalに強制復帰しました。");
        }
    }
}