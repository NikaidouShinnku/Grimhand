using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>
    /// Canvas 尺寸稳定后再应用战斗 UI 布局，避免仅调用一次时手牌/阵型/左下 HUD 未生效。
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class BattleUiBootstrap : MonoBehaviour
    {
        [SerializeField] int _followUpFrames = 6;

        bool _logged;

        void OnEnable() => StartCoroutine(ApplyWhenReady());

        IEnumerator ApplyWhenReady()
        {
            Apply(forceLog: true);

            for (var i = 0; i < _followUpFrames; i++)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                Apply(forceLog: false);
            }
        }

        void Apply(bool forceLog)
        {
            BattleUiLayoutRuntimeFix.ApplyIfNeeded(transform);

            var handPanel = GetComponentInChildren<HandPanelView>(true);
            handPanel?.ReapplyPoolLayout();

            var screen = GetComponent<BattleScreenView>();
            screen?.ApplyLateHudLayout();
            screen?.NotifyLayoutApplied();

            if (forceLog && !_logged)
            {
                _logged = true;
                Debug.Log("[Grimhand] 战斗 UI 布局已应用（Bootstrap）。");
            }
        }
    }
}
