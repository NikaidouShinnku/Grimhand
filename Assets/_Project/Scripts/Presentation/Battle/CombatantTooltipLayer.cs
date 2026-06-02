using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>角色悬停详情框的全局顶层容器，避免被其他立绘 sibling 遮挡。</summary>
    public static class CombatantTooltipLayer
    {
        const string LayerName = "CombatantTooltipLayer";
        const int SortOrder = 250;

        public static RectTransform GetOrCreate(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return null;

            var existing = battleScreenRoot.Find(LayerName) as RectTransform;
            if (existing != null)
            {
                existing.SetAsLastSibling();
                return existing;
            }

            var go = new GameObject(LayerName, typeof(RectTransform));
            go.transform.SetParent(battleScreenRoot, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortOrder;

            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

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
    }
}
