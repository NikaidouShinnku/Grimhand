using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>修复旧版 Setup 生成的错误 RectTransform，并将全宽 PlanningBar 拆成左右两角。</summary>
    public static class BattleUiLayoutRuntimeFix
    {
        const float CardRowBottom = 8f;
        const float CardRowHeight = 248f;
        const float PlanningRowBottom = 264f;
        const float PlanningRowHeight = 84f;
        const float PlanningActionsHeight = 148f;

        public static void ApplyIfNeeded(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return;

            ApplySplitPlanningBar(battleScreenRoot);
            ApplyBottomRowLayout(battleScreenRoot);
            ApplyStageDrawOrders(battleScreenRoot);
            RaiseBattleStages(battleScreenRoot);
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

            PinTopHeight(battleScreenRoot.Find("TargetPromptPanel"), 16, 16, 652, 48);
            FixActionBar(battleScreenRoot.Find("ActionBar"));

            LayoutRebuilder.ForceRebuildLayoutImmediate(battlefield);
            var rootRt = battleScreenRoot as RectTransform;
            if (rootRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);
        }

        static void ApplyBottomRowLayout(Transform battleScreenRoot)
        {
            if (battleScreenRoot.Find("PlayerStage") == null)
                return;

            var queue = battleScreenRoot.Find("SelectedQueuePanel") as RectTransform;
            if (queue != null)
                PinBottomLeft(queue, 12f, CardRowBottom, 320f, CardRowHeight);

            var intent = battleScreenRoot.Find("EnemyIntentPanel") as RectTransform;
            if (intent != null)
                PinBottomRight(intent, 12f, CardRowBottom, 360f, CardRowHeight);

            var hand = battleScreenRoot.Find("HandArea") as RectTransform;
            if (hand != null)
                PinBottomHeight(hand, 348f, 380f, CardRowBottom, CardRowHeight);

            var info = battleScreenRoot.Find("PlanningInfoLeft") as RectTransform
                ?? battleScreenRoot.Find("PlanningBar") as RectTransform;
            if (info != null && info.name == "PlanningInfoLeft")
                PinBottomLeft(info, 12f, PlanningRowBottom, 320f, PlanningRowHeight);

            var actions = battleScreenRoot.Find("PlanningActionsRight") as RectTransform;
            if (actions != null)
                PinBottomRight(actions, 12f, PlanningRowBottom, 660f, PlanningActionsHeight);

            ApplyPortraitScale(battleScreenRoot);
            FixEnergyIcons(battleScreenRoot);
            EnsurePlanningInfoLayout(battleScreenRoot);
            CombatantTooltipLayer.GetOrCreate(battleScreenRoot);
        }

        static void EnsurePlanningInfoLayout(Transform battleScreenRoot)
        {
            var info = battleScreenRoot.Find("PlanningInfoLeft") ?? battleScreenRoot.Find("PlanningBar");
            if (info == null)
                return;

            var title = info.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(1f, 1f);
                title.pivot = new Vector2(0f, 1f);
                title.offsetMin = new Vector2(12f, -28f);
                title.offsetMax = new Vector2(-8f, -8f);
            }

            var subtitle = info.Find("Subtitle") as RectTransform;
            if (subtitle != null)
            {
                subtitle.gameObject.SetActive(true);
                subtitle.anchorMin = new Vector2(0f, 1f);
                subtitle.anchorMax = new Vector2(1f, 1f);
                subtitle.pivot = new Vector2(0f, 1f);
                subtitle.offsetMin = new Vector2(12f, -52f);
                subtitle.offsetMax = new Vector2(-8f, -32f);

                var subtitleText = subtitle.GetComponent<Text>();
                if (subtitleText != null)
                {
                    subtitleText.fontSize = Mathf.Max(subtitleText.fontSize, 16);
                    subtitleText.alignment = TextAnchor.UpperLeft;
                }
            }

            var energyRow = info.Find("EnergyRow") as RectTransform;
            if (energyRow != null)
            {
                energyRow.anchorMin = new Vector2(0f, 0f);
                energyRow.anchorMax = new Vector2(1f, 0f);
                energyRow.pivot = new Vector2(0f, 0f);
                energyRow.offsetMin = new Vector2(12f, 4f);
                energyRow.offsetMax = new Vector2(-8f, 28f);

                var layout = energyRow.GetComponent<HorizontalLayoutGroup>();
                if (layout != null)
                    layout.childAlignment = TextAnchor.MiddleLeft;
            }
        }

        static void FixEnergyIcons(Transform battleScreenRoot)
        {
            var planningInfo = battleScreenRoot.Find("PlanningInfoLeft") ?? battleScreenRoot.Find("PlanningBar");
            var energyRow = planningInfo?.Find("EnergyRow");

            foreach (var icon in battleScreenRoot.GetComponentsInChildren<Image>(true))
            {
                if (icon.gameObject.name != "EnergyIcon")
                    continue;

                if (energyRow != null && icon.transform.parent != energyRow)
                    icon.transform.SetParent(energyRow, false);

                BattleScreenView.FixEnergyIconLayout(icon);
            }
        }

        static void ApplyPortraitScale(Transform battleScreenRoot)
        {
            foreach (var slot in battleScreenRoot.GetComponentsInChildren<CombatantSlotView>(true))
                slot.ApplyPortraitScaleFromRuntime();
        }

        static void ApplySplitPlanningBar(Transform battleScreenRoot)
        {
            if (battleScreenRoot.Find("PlanningInfoLeft") != null)
                return;

            var planningBar = battleScreenRoot.Find("PlanningBar") as RectTransform;
            if (planningBar == null)
                return;

            Debug.Log("[Grimhand] 拆分 PlanningBar 为左右两角布局。");

            var title = planningBar.Find("Title");
            var energyIcon = planningBar.Find("EnergyIcon");
            var actionBar = planningBar.Find("ActionBar");
            var queuePanel = planningBar.Find("SelectedQueuePanel");

            planningBar.name = "PlanningInfoLeft";
            PinBottomLeft(planningBar, 12f, PlanningRowBottom, 320f, PlanningRowHeight);

            if (queuePanel != null)
            {
                queuePanel.SetParent(battleScreenRoot, false);
                PinBottomLeft(queuePanel as RectTransform, 12f, CardRowBottom, 320f, CardRowHeight);
            }

            if (actionBar != null)
            {
                var actionsGo = new GameObject("PlanningActionsRight", typeof(RectTransform), typeof(Image));
                actionsGo.transform.SetParent(battleScreenRoot, false);
                var actionsImg = actionsGo.GetComponent<Image>();
                actionsImg.color = new Color(0.1f, 0.11f, 0.15f, 0.9f);
                PinBottomRight(actionsGo.GetComponent<RectTransform>(), 12f, PlanningRowBottom, 660f, PlanningActionsHeight);

                actionBar.SetParent(actionsGo.transform, false);
                StretchFull(actionBar as RectTransform, 8f, 8f, -8f, -8f);
            }

            EnsureEnergyRow(planningBar, energyIcon, title);
            RepositionTitle(title as RectTransform);
            RepositionSubtitle(planningBar.Find("Subtitle") as RectTransform);
        }

        static void RepositionSubtitle(RectTransform subtitle)
        {
            if (subtitle == null)
                return;

            subtitle.gameObject.SetActive(true);
            subtitle.anchorMin = new Vector2(0f, 1f);
            subtitle.anchorMax = new Vector2(1f, 1f);
            subtitle.pivot = new Vector2(0f, 1f);
            subtitle.offsetMin = new Vector2(12f, -52f);
            subtitle.offsetMax = new Vector2(-8f, -32f);
        }

        static void ApplyStageDrawOrders(Transform battleScreenRoot)
        {
            ApplyStageDrawOrder(battleScreenRoot.Find("PlayerStage"));
            ApplyStageDrawOrder(battleScreenRoot.Find("EnemyStage"));
        }

        static void ApplyStageDrawOrder(Transform stage)
        {
            if (stage == null)
                return;

            SetSlotSiblingIndex(stage, "Slot_Back", 0);
            SetSlotSiblingIndex(stage, "Slot_Middle", 1);
            SetSlotSiblingIndex(stage, "Slot_Front", 2);
        }

        static void SetSlotSiblingIndex(Transform stage, string slotName, int index)
        {
            var slot = stage.Find(slotName);
            if (slot != null)
                slot.SetSiblingIndex(index);
        }

        static void EnsureEnergyRow(RectTransform planningInfo, Transform energyIcon, Transform title)
        {
            var energyRow = planningInfo.Find("EnergyRow") as RectTransform;
            if (energyRow == null)
            {
                var rowGo = new GameObject("EnergyRow", typeof(RectTransform));
                rowGo.transform.SetParent(planningInfo, false);
                energyRow = rowGo.GetComponent<RectTransform>();
                energyRow.anchorMin = new Vector2(0f, 0f);
                energyRow.anchorMax = new Vector2(1f, 0f);
                energyRow.pivot = new Vector2(0f, 0f);
                energyRow.offsetMin = new Vector2(12f, 4f);
                energyRow.offsetMax = new Vector2(-8f, 28f);

                var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
            }

            if (energyIcon != null)
            {
                energyIcon.SetParent(energyRow, false);
                var le = energyIcon.GetComponent<LayoutElement>() ?? energyIcon.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 28f;
                le.preferredHeight = 28f;
                le.minWidth = 28f;
                le.minHeight = 28f;
            }

            var energyValue = energyRow.Find("EnergyValue")?.GetComponent<Text>();
            if (energyValue == null)
            {
                var textGo = new GameObject("EnergyValue", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(energyRow, false);
                energyValue = textGo.GetComponent<Text>();
                energyValue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                energyValue.fontSize = 24;
                energyValue.fontStyle = FontStyle.Bold;
                energyValue.color = Color.white;
                energyValue.alignment = TextAnchor.MiddleLeft;
                textGo.AddComponent<LayoutElement>().minWidth = 72f;
            }
        }

        static void RepositionTitle(RectTransform title)
        {
            if (title == null)
                return;

            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0f, 1f);
            title.offsetMin = new Vector2(12f, -28f);
            title.offsetMax = new Vector2(-8f, -8f);
        }

        static void RaiseBattleStages(Transform battleScreenRoot)
        {
            RaiseStage(battleScreenRoot.Find("PlayerStage") as RectTransform);
            RaiseStage(battleScreenRoot.Find("EnemyStage") as RectTransform);
        }

        static void RaiseStage(RectTransform stage)
        {
            if (stage == null)
                return;

            var min = stage.anchorMin;
            if (min.y >= 0.36f)
                return;

            stage.anchorMin = new Vector2(min.x, 0.36f);
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
                scroll.offsetMax = new Vector2(0f, -28f);
            }

            var label = handArea.Find("HandCount");
            if (label is RectTransform labelRt)
            {
                labelRt.anchorMin = new Vector2(0.5f, 1f);
                labelRt.anchorMax = new Vector2(0.5f, 1f);
                labelRt.pivot = new Vector2(0.5f, 1f);
                labelRt.anchoredPosition = new Vector2(0f, -4f);
                labelRt.sizeDelta = new Vector2(280f, 24f);
            }
        }

        static void FixActionBar(Transform actionBar)
        {
            if (actionBar == null)
                return;

            var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = 10;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        static void StretchFull(RectTransform rt, float left, float bottom, float right, float top)
        {
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        static void PinBottomLeft(RectTransform rt, float left, float fromBottom, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(left, fromBottom);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void PinBottomRight(RectTransform rt, float right, float fromBottom, float width, float height)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-right, fromBottom);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void PinBottomHeight(RectTransform rt, float left, float right, float fromBottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, fromBottom);
            rt.offsetMax = new Vector2(-right, fromBottom + height);
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

        static void FillVerticalBand(RectTransform rt, float left, float right, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(left, 4f);
            rt.offsetMax = new Vector2(-right, -4f);
        }
    }
}
