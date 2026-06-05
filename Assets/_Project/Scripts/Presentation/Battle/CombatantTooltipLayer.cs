using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>背包/明细/角色悬停等浮层 — 独立 Canvas，排序高于 HudChrome（手牌）。</summary>
    public static class CombatantTooltipLayer
    {
        const string LayerName = "CombatantTooltipLayer";

        /// <summary>必须高于 <see cref="BattleUiLayoutRuntimeFix.HudChromeSortOrder"/>。</summary>
        public const int OverlaySortOrder = 120;

        public static RectTransform GetOrCreate(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return null;

            var existing = battleScreenRoot.Find(LayerName) as RectTransform;
            if (existing != null)
            {
                Configure(existing);
                existing.SetAsLastSibling();
                return existing;
            }

            var go = new GameObject(LayerName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(battleScreenRoot, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Configure(rt);
            go.transform.SetAsLastSibling();
            return rt;
        }

        public static void MountToFront(RectTransform panel, Transform battleScreenRoot)
        {
            if (panel == null || battleScreenRoot == null)
                return;

            var layer = GetOrCreate(battleScreenRoot);
            panel.SetParent(layer, worldPositionStays: true);
            panel.SetAsLastSibling();
            layer.SetAsLastSibling();
        }

        static void Configure(RectTransform root)
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortOrder;

            var raycaster = root.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = true;
        }
    }
}
