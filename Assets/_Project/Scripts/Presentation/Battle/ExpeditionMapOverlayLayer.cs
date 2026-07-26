using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>
    /// 远征地图专用 Canvas 层：必须高于 HudChrome（手牌/出牌）与 CombatantTooltipLayer。
    /// </summary>
    public static class ExpeditionMapOverlayLayer
    {
        const string LayerName = "ExpeditionMapOverlayLayer";

        /// <summary>高于手牌(45)、按钮提升(65)、角色浮层(120)。</summary>
        public const int SortOrder = 500;

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

        public static void MountToFront(RectTransform overlay, Transform battleScreenRoot)
        {
            if (overlay == null || battleScreenRoot == null)
                return;

            var layer = GetOrCreate(battleScreenRoot);
            if (overlay.parent != layer)
                overlay.SetParent(layer, worldPositionStays: false);

            StretchFull(overlay);
            overlay.SetAsLastSibling();
            layer.SetAsLastSibling();
            Configure(layer);

            // 去掉子级多余 Canvas，避免与层 Canvas 抢排序导致仍落在手牌之下
            var nested = overlay.GetComponent<Canvas>();
            if (nested != null)
                Object.Destroy(nested);
            var nestedRaycaster = overlay.GetComponent<GraphicRaycaster>();
            if (nestedRaycaster != null)
                Object.Destroy(nestedRaycaster);
        }

        public static void BringLayerToFront(Transform battleScreenRoot)
        {
            var layer = battleScreenRoot != null
                ? battleScreenRoot.Find(LayerName) as RectTransform
                : null;
            if (layer == null || !layer.gameObject.activeInHierarchy)
                return;

            Configure(layer);
            layer.SetAsLastSibling();
        }

        static void Configure(RectTransform root)
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortOrder;

            if (root.GetComponent<GraphicRaycaster>() == null)
                root.gameObject.AddComponent<GraphicRaycaster>();
        }

        static void StretchFull(RectTransform rt)
        {
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
