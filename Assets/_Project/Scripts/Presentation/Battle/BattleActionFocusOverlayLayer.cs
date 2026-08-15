using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>
    /// 出牌居中立绘抬层：独立 Canvas（高于 HudChrome），避免在带缩放的槽位上挂嵌套 Canvas 导致体型跳动。
    /// </summary>
    public static class BattleActionFocusOverlayLayer
    {
        const string LayerName = "BattleActionFocusOverlayLayer";

        /// <summary>高于 HudChrome(45) 与临时抬层控件(65)。</summary>
        public const int SortOrder = 80;

        public static RectTransform GetOrCreate(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return null;

            var existing = battleScreenRoot.Find(LayerName) as RectTransform;
            if (existing != null)
            {
                Configure(existing);
                return existing;
            }

            var go = new GameObject(LayerName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(battleScreenRoot, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            Configure(rt);
            return rt;
        }

        public static Transform FindBattleScreenRoot(Transform from)
        {
            var t = from;
            while (t != null)
            {
                if (t.GetComponent<BattleScreenView>() != null)
                    return t;
                t = t.parent;
            }

            return null;
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
    }
}
