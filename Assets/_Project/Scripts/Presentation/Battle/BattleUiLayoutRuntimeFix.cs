using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>修复旧版 Setup 生成的错误 RectTransform（负高度、按钮出屏）。</summary>
    public static class BattleUiLayoutRuntimeFix
    {
        public static void ApplyIfNeeded(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return;

            FixHandArea(battleScreenRoot.Find("HandArea"));

            var battlefield = battleScreenRoot.Find("Battlefield") as RectTransform;
            if (battlefield == null)
                return;

            var playerRow = battlefield.Find("PlayerRow") as RectTransform;
            var enemyRow = battlefield.Find("EnemyRow") as RectTransform;
            if (playerRow == null || enemyRow == null)
                return;

            if (!NeedsFix(battlefield, playerRow))
                return;

            Debug.Log("[Grimhand] 检测到旧版战斗 UI 布局，正在自动修复。");

            PinTopHeight(battleScreenRoot.Find("HUD"), 0, 0, 0, 72);
            PinTopHeight(battlefield, 16, 16, 80, 420);
            FillVerticalBand(enemyRow, 8, 8, 0.54f, 1f);
            FillVerticalBand(playerRow, 8, 8, 0f, 0.46f);

            PinTopHeight(battleScreenRoot.Find("EnemyIntentPanel"), 16, 16, 508, 64);
            PinTopHeight(battleScreenRoot.Find("SelectedQueuePanel"), 16, 16, 580, 64);
            PinTopHeight(battleScreenRoot.Find("TargetPromptPanel"), 16, 16, 652, 48);
            PinBottomHeight(battleScreenRoot.Find("HandArea"), 16, 16, 96, 196);
            FixActionBar(battleScreenRoot.Find("ActionBar"));

            LayoutRebuilder.ForceRebuildLayoutImmediate(battlefield);
            var rootRt = battleScreenRoot as RectTransform;
            if (rootRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
        }

        static bool NeedsFix(RectTransform battlefield, RectTransform playerRow)
        {
            if (playerRow.sizeDelta.y < 0f)
                return true;
            if (battlefield.sizeDelta.y < 300f && battlefield.rect.height < 300f)
                return true;
            return playerRow.rect.height < 20f;
        }

        static void FixHandArea(Transform handArea)
        {
            if (handArea == null)
                return;

            var scroll = handArea.Find("HandScroll") as RectTransform;
            if (scroll != null)
            {
                scroll.anchorMin = Vector2.zero;
                scroll.anchorMax = Vector2.one;
                scroll.offsetMin = new Vector2(0f, 0f);
                scroll.offsetMax = new Vector2(0f, -32f);
            }

            var label = handArea.Find("HandCount");
            if (label is RectTransform labelRt)
            {
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot = new Vector2(0f, 1f);
                labelRt.anchoredPosition = new Vector2(8f, -6f);
                labelRt.sizeDelta = new Vector2(260f, 24f);
            }
        }

        static void FixActionBar(Transform actionBar)
        {
            if (actionBar == null)
                return;

            PinBottomHeight(actionBar, 16, 16, 8, 80);

            var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = 12;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            foreach (Transform child in actionBar)
            {
                var rt = child as RectTransform;
                if (rt == null)
                    continue;

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                var le = child.GetComponent<LayoutElement>();
                if (le == null)
                    le = child.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 200;
                le.preferredHeight = 52;
                le.minWidth = 140;
            }
        }

        static void PinTopHeight(Transform t, float left, float right, float fromTop, float height)
        {
            if (t == null)
                return;

            var rt = t as RectTransform ?? t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -fromTop - height);
            rt.offsetMax = new Vector2(-right, -fromTop);
        }

        static void PinBottomHeight(Transform t, float left, float right, float fromBottom, float height)
        {
            if (t == null)
                return;

            var rt = t as RectTransform ?? t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, fromBottom);
            rt.offsetMax = new Vector2(-right, fromBottom + height);
        }

        static void FillVerticalBand(RectTransform rt, float left, float right, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(left, 4f);
            rt.offsetMax = new Vector2(-right, -4f);
        }
    }
}
