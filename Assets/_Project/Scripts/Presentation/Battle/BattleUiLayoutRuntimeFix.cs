using Grimhand.Battle.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗 HUD 布局：底部独立 Canvas 层 + 比例锚点，4K/1080p 一致。</summary>
    public static class BattleUiLayoutRuntimeFix
    {
        public const float HandCardScale = 1.34f;
        public const float CardBaseWidth = 168f;
        public const float CardBaseHeight = 236f;

        const float RefWidth = 1920f;
        const float RefHeight = 1080f;

        const float CardRowBottom = 8f;
        const float HandLabelHeight = 20f;
        const float RowGap = 8f;

        const float IntentPanelWidth = 360f;
        const float IntentPanelRight = 12f;
        const float IntentPanelHeight = 248f;

        const float InventoryButtonSize = 96f;
        const float InventoryGap = 8f;
        const float TurnLogButtonGap = 8f;
        const float MapButtonGap = 8f;
        const float CodexButtonGap = 12f;
        const float PresentationSpeedButtonGap = 12f;
        const float PresentationSpeedButtonSize = 96f;
        const float SettingsButtonSize = 96f;
        const float SettingsButtonGap = 12f;
        const float EnergyHudWidth = 168f;
        const float EnergyHudHeight = 72f;

        const float ExpeditionPanelWidth = 320f;
        const float ExpeditionPanelHeight = 84f;
        const float PlanningActionsWidth = 360f;
        const float PlanningActionsHeight = 112f;
        const float PlanningActionButtonWidth = PlanningActionButtonStyle.DefaultWidth;
        const float PlanningActionsAboveIntentGap = 10f;

        const float ActionOrderBarTop = 8f;
        const float ActionOrderBarHeight = 220f;
        const float ActionOrderBarLeft = 120f;
        // 右侧留给加速按钮（含独立 overlay canvas），避免意图条盖住
        const float ActionOrderBarRight = 140f;
        public const float ActionOrderBarMiniCardScale = 0.66f;

        const float StageBottom = 0.19f;
        const float StageTop = 0.78f;
        const int HudChromeSortOrder = 45;

        public static int HudChromeSortOrderValue => HudChromeSortOrder;

        /// <summary>设置/加速等关键控件提到更高 Canvas，避免被意图条挡住点击。</summary>
        public static void PromoteHudControlOverlay(RectTransform control)
        {
            if (control == null)
                return;

            var canvas = control.GetComponent<Canvas>();
            if (canvas == null)
                canvas = control.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = HudChromeSortOrder + 20;

            if (control.GetComponent<GraphicRaycaster>() == null)
                control.gameObject.AddComponent<GraphicRaycaster>();
        }

        public static float ScaledCardWidth => CardBaseWidth * HandCardScale;
        public static float ScaledCardHeight => CardBaseHeight * HandCardScale;
        public static float ScaledOrderBarCardWidth => CardBaseWidth * ActionOrderBarMiniCardScale;
        public static float ScaledOrderBarCardHeight => CardBaseHeight * ActionOrderBarMiniCardScale;
        static float CardRowHeight => ScaledCardHeight + HandLabelHeight + 2f;
        static float HandRightInset => 24f;
        static float EnergyHudLeft => InventoryGap;
        // 能量靠左下角后，侧栏按钮叠在能量上方；手牌需避开更宽的能量区
        static float UtilityStackBottom => CardRowBottom + EnergyHudHeight + InventoryGap;
        static float HandLeftInset =>
            Mathf.Max(InventoryButtonSize + InventoryGap, EnergyHudLeft + EnergyHudWidth) + 12f;
        static float UpperRowBottom => CardRowBottom + CardRowHeight + RowGap;
        // 出牌/空过放在手牌行上方，避免与加宽后的手牌区重叠
        static float PlanningActionsBottom => CardRowBottom + CardRowHeight + RowGap;

        public static void ApplyIfNeeded(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return;

            var chromeRoot = EnsureHudChromeRoot(battleScreenRoot);

            ApplySplitPlanningBar(battleScreenRoot);
            ApplyStageClusterLayout(battleScreenRoot);
            ApplyBottomRowLayout(chromeRoot != null ? chromeRoot : battleScreenRoot);
            ApplyFormationRowLayout(battleScreenRoot);
            ApplyStageDrawOrders(battleScreenRoot);
            ApplyPortraitScale(battleScreenRoot);
            FixHandArea(FindChrome(chromeRoot, battleScreenRoot, "HandArea"));
            ApplyHandCardScale(battleScreenRoot);
            EnsureBottomHudDrawOrder(battleScreenRoot, chromeRoot);

            var rootRt = battleScreenRoot as RectTransform;
            if (rootRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRt);

            if (chromeRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(chromeRoot);
        }

        public static Transform GetHudChromeRoot(Transform battleScreenRoot) =>
            battleScreenRoot?.Find("HudChromeRoot");

        static Transform FindChrome(Transform chromeRoot, Transform battleScreenRoot, string name)
        {
            if (chromeRoot != null)
            {
                var underChrome = chromeRoot.Find(name);
                if (underChrome != null)
                    return underChrome;
            }

            return battleScreenRoot.Find(name);
        }

        static RectTransform EnsureHudChromeRoot(Transform battleScreenRoot)
        {
            var existing = battleScreenRoot.Find("HudChromeRoot") as RectTransform;
            if (existing != null)
            {
                ConfigureHudChromeRoot(existing);
                ReparentChromePanels(battleScreenRoot, existing);
                existing.SetAsLastSibling();
                return existing;
            }

            var go = new GameObject("HudChromeRoot", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(battleScreenRoot, false);
            var root = go.GetComponent<RectTransform>();
            ConfigureHudChromeRoot(root);
            ReparentChromePanels(battleScreenRoot, root);
            root.SetAsLastSibling();
            return root;
        }

        static void ConfigureHudChromeRoot(RectTransform root)
        {
            StretchFull(root, 0f, 0f, 0f, 0f);

            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = HudChromeSortOrder;

            var raycaster = root.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = true;
        }

        static void ReparentChromePanels(Transform battleScreenRoot, RectTransform chromeRoot)
        {
            ReparentIfFound(battleScreenRoot, chromeRoot, "HandArea");
            ReparentIfFound(battleScreenRoot, chromeRoot, "EnemyIntentPanel");
            ReparentIfFound(battleScreenRoot, chromeRoot, "PlanningInfoLeft");
            ReparentIfFound(battleScreenRoot, chromeRoot, "PlanningActionsRight");
            ReparentIfFound(battleScreenRoot, chromeRoot, "SelectedQueuePanel");
            ReparentIfFound(battleScreenRoot, chromeRoot, "EnergyHud");
            ReparentIfFound(battleScreenRoot, chromeRoot, "InventoryButton");
            ReparentIfFound(battleScreenRoot, chromeRoot, "TurnLogButton");
            ReparentIfFound(battleScreenRoot, chromeRoot, "MapButton");
            ReparentIfFound(battleScreenRoot, chromeRoot, "CodexButton");
            ReparentIfFound(battleScreenRoot, chromeRoot, "DummyPlayButton");
            ReparentIfFound(battleScreenRoot, chromeRoot, "PresentationSpeedButton");
            ReparentIfFound(battleScreenRoot, chromeRoot, "BattleSettingsButton");
        }

        static void ReparentIfFound(Transform battleScreenRoot, RectTransform chromeRoot, string name)
        {
            var node = battleScreenRoot.Find(name);
            if (node == null || node.parent == chromeRoot)
                return;

            node.SetParent(chromeRoot, false);
        }

        static void ApplyStageClusterLayout(Transform battleScreenRoot)
        {
            SetStageRect(battleScreenRoot.Find("PlayerStage") as RectTransform, 0.02f, StageBottom, 0.42f, StageTop);
            SetStageRect(battleScreenRoot.Find("EnemyStage") as RectTransform, 0.58f, StageBottom, 0.98f, StageTop);
        }

        static void SetStageRect(RectTransform stage, float xMin, float yMin, float xMax, float yMax)
        {
            if (stage == null)
                return;

            stage.anchorMin = new Vector2(xMin, yMin);
            stage.anchorMax = new Vector2(xMax, yMax);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = Vector2.zero;
            stage.offsetMin = Vector2.zero;
            stage.offsetMax = Vector2.zero;
        }

        static void ApplyBottomRowLayout(Transform layoutRoot)
        {
            if (layoutRoot.Find("HandArea") == null && layoutRoot.parent != null)
                return;

            var queue = layoutRoot.Find("SelectedQueuePanel");
            if (queue != null)
                queue.gameObject.SetActive(false);

            var intent = layoutRoot.Find("EnemyIntentPanel") as RectTransform;
            if (intent != null)
            {
                // 旧右下意图灰框永久关闭；意图改由顶部 ActionOrderBar 显示
                intent.gameObject.SetActive(false);
                StripNestedCanvas(intent.gameObject);
                var intentBg = intent.GetComponent<Image>();
                if (intentBg != null)
                {
                    intentBg.color = Color.clear;
                    intentBg.raycastTarget = false;
                }
            }

            var info = layoutRoot.Find("PlanningInfoLeft") as RectTransform
                ?? layoutRoot.Find("PlanningBar") as RectTransform;
            if (info != null && info.name == "PlanningInfoLeft")
            {
                StripNestedCanvas(info.gameObject);
                info.gameObject.SetActive(false);
            }

            var actions = layoutRoot.Find("PlanningActionsRight") as RectTransform;
            if (actions != null)
            {
                StripNestedCanvas(actions.gameObject);
                PinBottomRight(actions, IntentPanelRight, PlanningActionsBottom, PlanningActionsWidth, PlanningActionsHeight);
                var actionsBg = actions.GetComponent<Image>();
                if (actionsBg != null)
                {
                    actionsBg.color = Color.clear;
                    actionsBg.raycastTarget = false;
                }

                FixActionBar(actions.Find("ActionBar"));
            }

            var hand = layoutRoot.Find("HandArea") as RectTransform;
            if (hand != null)
            {
                var handBg = hand.GetComponent<Image>();
                if (handBg != null)
                {
                    handBg.color = Color.clear;
                    handBg.raycastTarget = false;
                }
            }

            var battleScreenRoot = layoutRoot.parent != null ? layoutRoot.parent : layoutRoot;
            ApplyPortraitScale(battleScreenRoot);
            EnsurePlanningInfoLayout(battleScreenRoot);

            var orderBar = layoutRoot.Find("ActionOrderBar") as RectTransform;
            if (orderBar != null)
                LayoutActionOrderBar(orderBar);
        }

        public static void LayoutActionOrderBar(RectTransform bar)
        {
            if (bar == null)
                return;

            PinTopHeight(bar, ActionOrderBarLeft, ActionOrderBarRight, ActionOrderBarTop, ActionOrderBarHeight);
        }

        static void StripNestedCanvas(GameObject panel)
        {
            if (panel == null || panel.name == "HudChromeRoot")
                return;

            var raycaster = panel.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                Object.Destroy(raycaster);

            var canvas = panel.GetComponent<Canvas>();
            if (canvas != null)
                Object.Destroy(canvas);
        }

        static void ApplyFormationRowLayout(Transform battleScreenRoot)
        {
            ApplyStageRow(battleScreenRoot.Find("PlayerStage"), TeamSide.Player);
            ApplyStageRow(battleScreenRoot.Find("EnemyStage"), TeamSide.Enemy);
        }

        static void ApplyStageRow(Transform stage, TeamSide team)
        {
            if (stage == null)
                return;

            const float rowMinY = 0.08f;
            const float rowMaxY = 0.92f;

            if (team == TeamSide.Player)
            {
                SetSlotBand(stage, "Slot_Back", 0.04f, 0.32f, rowMinY, rowMaxY);
                SetSlotBand(stage, "Slot_Middle", 0.36f, 0.64f, rowMinY, rowMaxY);
                SetSlotBand(stage, "Slot_Front", 0.68f, 0.96f, rowMinY, rowMaxY);
            }
            else
            {
                SetSlotBand(stage, "Slot_Front", 0.04f, 0.32f, rowMinY, rowMaxY);
                SetSlotBand(stage, "Slot_Middle", 0.36f, 0.64f, rowMinY, rowMaxY);
                SetSlotBand(stage, "Slot_Back", 0.68f, 0.96f, rowMinY, rowMaxY);
            }
        }

        static void SetSlotBand(Transform stage, string slotName, float xMin, float xMax, float yMin, float yMax)
        {
            var slot = stage.Find(slotName) as RectTransform;
            if (slot == null)
                return;

            slot.anchorMin = new Vector2(xMin, yMin);
            slot.anchorMax = new Vector2(xMax, yMax);
            slot.offsetMin = Vector2.zero;
            slot.offsetMax = Vector2.zero;
        }

        static void EnsurePlanningInfoLayout(Transform battleScreenRoot)
        {
            var info = battleScreenRoot.Find("HudChromeRoot/PlanningInfoLeft")
                ?? battleScreenRoot.Find("PlanningInfoLeft")
                ?? battleScreenRoot.Find("PlanningBar");
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

            var energyRow = info.Find("EnergyRow");
            if (energyRow != null)
                energyRow.gameObject.SetActive(false);
        }

        public static void LayoutEnergyHud(RectTransform energyHud)
        {
            if (energyHud == null)
                return;

            StripNestedCanvas(energyHud.gameObject);
            // 水晶靠左下角
            PinBottomLeft(energyHud, EnergyHudLeft, CardRowBottom, EnergyHudWidth, EnergyHudHeight);
            var bg = energyHud.GetComponent<Image>();
            if (bg != null)
                bg.raycastTarget = false;
        }

        /// <summary>确保意图条在设置/加速等可点按钮之下，避免遮挡。</summary>
        public static void EnsureInteractiveHudAboveActionOrderBar(Transform chromeRoot)
        {
            if (chromeRoot == null)
                return;

            var orderBar = chromeRoot.Find("ActionOrderBar");
            if (orderBar != null)
                orderBar.SetSiblingIndex(0);

            BringToFront(chromeRoot, "BattleSettingsButton");
            BringToFront(chromeRoot, "PresentationSpeedButton");
            BringToFront(chromeRoot, "CodexButton");
            BringToFront(chromeRoot, "DummyPlayButton");
            BringToFront(chromeRoot, "MonsterSpawnButton");
            BringToFront(chromeRoot, "InventoryButton");
            BringToFront(chromeRoot, "TurnLogButton");
            BringToFront(chromeRoot, "MapButton");
            BringToFront(chromeRoot, "PlanningActionsRight");
            BringToFront(chromeRoot, "EnergyHud");

            PromoteHudControlOverlay(chromeRoot.Find("BattleSettingsButton") as RectTransform);
            PromoteHudControlOverlay(chromeRoot.Find("PresentationSpeedButton") as RectTransform);
            PromoteHudControlOverlay(chromeRoot.Find("InventoryButton") as RectTransform);
            PromoteHudControlOverlay(chromeRoot.Find("TurnLogButton") as RectTransform);
            PromoteHudControlOverlay(chromeRoot.Find("MapButton") as RectTransform);
        }

        static void BringToFront(Transform chromeRoot, string name)
        {
            var node = chromeRoot.Find(name);
            if (node != null)
                node.SetAsLastSibling();
        }

        public static void LayoutInventoryButton(RectTransform inventoryButton)
        {
            if (inventoryButton == null)
                return;

            PinBottomLeft(inventoryButton, InventoryGap, UtilityStackBottom, InventoryButtonSize, InventoryButtonSize);
        }

        public static void LayoutTurnLogButton(RectTransform turnLogButton)
        {
            if (turnLogButton == null)
                return;

            var fromBottom = UtilityStackBottom + InventoryButtonSize + TurnLogButtonGap;
            PinBottomLeft(turnLogButton, InventoryGap, fromBottom, InventoryButtonSize, InventoryButtonSize);
        }

        public static void LayoutMapButton(RectTransform mapButton)
        {
            if (mapButton == null)
                return;

            var fromBottom = UtilityStackBottom + InventoryButtonSize + TurnLogButtonGap
                             + InventoryButtonSize + MapButtonGap;
            PinBottomLeft(mapButton, InventoryGap, fromBottom, InventoryButtonSize, InventoryButtonSize);
        }

        public static void LayoutCodexButton(RectTransform codexButton)
        {
            if (codexButton == null)
                return;

            // 设置按钮占左上角，图鉴紧挨其右
            var left = SettingsButtonGap + SettingsButtonSize + CodexButtonGap;
            PinTopLeft(codexButton, left, CodexButtonGap, InventoryButtonSize, InventoryButtonSize);
        }

        public static void LayoutDummyPlayButton(RectTransform dummyPlayButton, RectTransform codexButton = null)
        {
            if (dummyPlayButton == null)
                return;

            if (codexButton != null && codexButton.parent != null)
                dummyPlayButton.SetParent(codexButton.parent, false);

            var codexWidth = InventoryButtonSize;
            var dummyWidth = InventoryButtonSize + 40f;
            var left = SettingsButtonGap + SettingsButtonSize + CodexButtonGap + codexWidth + CodexButtonGap;
            PinTopLeft(dummyPlayButton, left, CodexButtonGap, dummyWidth, InventoryButtonSize);
        }

        public static void LayoutMonsterSpawnButton(RectTransform monsterButton, RectTransform dummyPlayButton = null)
        {
            if (monsterButton == null)
                return;

            if (dummyPlayButton != null && dummyPlayButton.parent != null)
                monsterButton.SetParent(dummyPlayButton.parent, false);

            var monsterWidth = InventoryButtonSize + 40f;
            var left = SettingsButtonGap + SettingsButtonSize + CodexButtonGap + InventoryButtonSize
                       + CodexButtonGap + (InventoryButtonSize + 40f) + CodexButtonGap;
            if (dummyPlayButton != null)
                left = SettingsButtonGap + SettingsButtonSize + CodexButtonGap + InventoryButtonSize
                       + CodexButtonGap + dummyPlayButton.sizeDelta.x + CodexButtonGap;

            PinTopLeft(monsterButton, left, CodexButtonGap, monsterWidth, InventoryButtonSize);
        }

        public static void LayoutPresentationSpeedButton(RectTransform speedButton)
        {
            if (speedButton == null)
                return;

            PinTopRight(
                speedButton,
                PresentationSpeedButtonGap,
                PresentationSpeedButtonGap,
                PresentationSpeedButtonSize,
                PresentationSpeedButtonSize);
        }

        public static void LayoutSettingsButton(RectTransform settingsButton)
        {
            if (settingsButton == null)
                return;

            PinTopLeft(
                settingsButton,
                SettingsButtonGap,
                SettingsButtonGap,
                SettingsButtonSize,
                SettingsButtonSize);
        }

        public static void RefreshBottomHud(Transform battleScreenRoot)
        {
            if (battleScreenRoot == null)
                return;

            var chromeRoot = EnsureHudChromeRoot(battleScreenRoot);
            LayoutEnergyHud(chromeRoot.Find("EnergyHud") as RectTransform);
            LayoutInventoryButton(chromeRoot.Find("InventoryButton") as RectTransform);
            LayoutTurnLogButton(chromeRoot.Find("TurnLogButton") as RectTransform);
            LayoutMapButton(chromeRoot.Find("MapButton") as RectTransform);
            LayoutCodexButton(chromeRoot.Find("CodexButton") as RectTransform);
            LayoutDummyPlayButton(
                chromeRoot.Find("DummyPlayButton") as RectTransform,
                chromeRoot.Find("CodexButton") as RectTransform);
            LayoutPresentationSpeedButton(chromeRoot.Find("PresentationSpeedButton") as RectTransform);
            LayoutSettingsButton(chromeRoot.Find("BattleSettingsButton") as RectTransform);
            FixHandArea(chromeRoot.Find("HandArea"));
            ApplyBottomRowLayout(chromeRoot);
            EnsureInteractiveHudAboveActionOrderBar(chromeRoot);
            EnsureBottomHudDrawOrder(battleScreenRoot, chromeRoot);
        }

        static void ApplyPortraitScale(Transform battleScreenRoot)
        {
            foreach (var slot in battleScreenRoot.GetComponentsInChildren<CombatantSlotView>(true))
                slot.ApplyPortraitScaleFromRuntime();
        }

        static void ApplySplitPlanningBar(Transform battleScreenRoot)
        {
            if (battleScreenRoot.Find("PlanningInfoLeft") != null
                || battleScreenRoot.Find("HudChromeRoot/PlanningInfoLeft") != null)
                return;

            var planningBar = battleScreenRoot.Find("PlanningBar") as RectTransform;
            if (planningBar == null)
                return;

            Debug.Log("[Grimhand] 拆分 PlanningBar 为左右两角布局。");

            var energyIcon = planningBar.Find("EnergyIcon");
            var actionBar = planningBar.Find("ActionBar");
            var queuePanel = planningBar.Find("SelectedQueuePanel");

            planningBar.name = "PlanningInfoLeft";
            PinBottomLeft(planningBar, 12f, UpperRowBottom, ExpeditionPanelWidth, ExpeditionPanelHeight);

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
                actionsImg.color = new Color(0.1f, 0.11f, 0.15f, 0.94f);
                PinBottomRight(actionsGo.GetComponent<RectTransform>(), 12f, PlanningActionsBottom, PlanningActionsWidth, PlanningActionsHeight);

                actionBar.SetParent(actionsGo.transform, false);
                StretchFull(actionBar as RectTransform, 8f, 8f, -8f, -8f);
            }

            EnsureEnergyRow(planningBar, energyIcon, planningBar.Find("Title"));
            RepositionTitle(planningBar.Find("Title") as RectTransform);
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
                energyRow.offsetMin = new Vector2(12f, 8f);
                energyRow.offsetMax = new Vector2(-8f, 36f);

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
                le.preferredWidth = 32f;
                le.preferredHeight = 32f;
                le.minWidth = 32f;
                le.minHeight = 32f;
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

        static void FixHandArea(Transform handArea)
        {
            if (handArea == null)
                return;

            var handRt = handArea as RectTransform;
            if (handRt != null)
                PinBottomHeight(handRt, HandLeftInset, HandRightInset, CardRowBottom, CardRowHeight);

            var scroll = handArea.Find("HandScroll") as RectTransform;
            if (scroll != null)
            {
                scroll.anchorMin = Vector2.zero;
                scroll.anchorMax = Vector2.one;
                scroll.pivot = new Vector2(0.5f, 0.5f);
                scroll.anchoredPosition = Vector2.zero;
                scroll.sizeDelta = Vector2.zero;
                scroll.offsetMin = Vector2.zero;
                scroll.offsetMax = new Vector2(0f, -HandLabelHeight);
            }

            var viewport = handArea.Find("HandScroll/Viewport") as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;

                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null)
                    viewportImage.color = Color.clear;

                var legacyMask = viewport.GetComponent<Mask>();
                if (legacyMask != null)
                    legacyMask.enabled = false;

                if (viewport.GetComponent<RectMask2D>() == null)
                    viewport.gameObject.AddComponent<RectMask2D>();
            }

            var content = handArea.Find("HandScroll/Viewport/Content");
            if (content != null)
            {
                var contentRt = content.GetComponent<RectTransform>();
                if (contentRt != null)
                {
                    contentRt.anchorMin = new Vector2(0f, 0f);
                    contentRt.anchorMax = new Vector2(0f, 1f);
                    contentRt.pivot = new Vector2(0f, 0.5f);
                    contentRt.anchoredPosition = Vector2.zero;
                    contentRt.sizeDelta = new Vector2(0f, 0f);
                }

                var layout = content.GetComponent<HorizontalLayoutGroup>();
                if (layout != null)
                {
                    layout.spacing = 2;
                    layout.padding = new RectOffset(0, 0, 0, 0);
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    layout.childControlWidth = false;
                    layout.childControlHeight = false;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                }

                var fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
            }

            var label = handArea.Find("HandCount");
            if (label is RectTransform labelRt)
            {
                labelRt.SetAsLastSibling();
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.pivot = new Vector2(0.5f, 1f);
                labelRt.anchoredPosition = Vector2.zero;
                labelRt.offsetMin = new Vector2(0f, -HandLabelHeight);
                labelRt.offsetMax = new Vector2(0f, 0f);

                var labelText = label.GetComponent<Text>();
                if (labelText != null)
                    labelText.alignment = TextAnchor.MiddleCenter;
            }
        }

        static void ApplyHandCardScale(Transform battleScreenRoot)
        {
            var hand = FindChrome(battleScreenRoot.Find("HudChromeRoot"), battleScreenRoot, "HandArea");
            if (hand == null)
                return;

            foreach (var cardView in hand.GetComponentsInChildren<CardView>(true))
                CardView.ApplyHandPresentationScale(cardView, HandCardScale);
        }

        static void EnsureBottomHudDrawOrder(Transform battleScreenRoot, Transform chromeRoot)
        {
            if (chromeRoot != null)
                chromeRoot.SetAsLastSibling();

            var tooltip = battleScreenRoot.Find("CombatantTooltipLayer");
            if (tooltip != null)
                tooltip.SetAsLastSibling();
        }

        public static void FixActionBarPublic(Transform actionBar) => FixActionBar(actionBar);

        static void FixActionBar(Transform actionBar)
        {
            if (actionBar == null)
                return;

            var restart = actionBar.Find("RestartButton");
            if (restart != null)
                restart.gameObject.SetActive(false);

            var skip = actionBar.Find("SkipButton");
            var confirm = actionBar.Find("ConfirmButton");
            if (skip != null)
            {
                skip.gameObject.SetActive(true);
                skip.SetSiblingIndex(0);
            }

            if (confirm != null)
            {
                confirm.gameObject.SetActive(true);
                confirm.SetSiblingIndex(1);
            }

            foreach (Transform child in actionBar)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                var rt = child as RectTransform;
                if (rt != null)
                    rt.sizeDelta = new Vector2(PlanningActionButtonWidth, PlanningActionButtonStyle.HeightForWidth(PlanningActionButtonWidth));

                var le = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = PlanningActionButtonWidth;
                le.minWidth = PlanningActionButtonWidth;
                le.preferredHeight = PlanningActionButtonStyle.HeightForWidth(PlanningActionButtonWidth);
                le.minHeight = PlanningActionButtonStyle.HeightForWidth(PlanningActionButtonWidth);
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;
            }

            var layout = actionBar.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = 8;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutRebuilder.ForceRebuildLayoutImmediate(actionBar as RectTransform);
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

        static void PinTopRight(RectTransform rt, float right, float fromTop, float width, float height)
        {
            rt.anchorMin = new Vector2(1f - (right + width) / RefWidth, 1f - (fromTop + height) / RefHeight);
            rt.anchorMax = new Vector2(1f - right / RefWidth, 1f - fromTop / RefHeight);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void PinTopLeft(RectTransform rt, float left, float fromTop, float width, float height)
        {
            rt.anchorMin = new Vector2(left / RefWidth, 1f - (fromTop + height) / RefHeight);
            rt.anchorMax = new Vector2((left + width) / RefWidth, 1f - fromTop / RefHeight);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void PinBottomLeft(RectTransform rt, float left, float fromBottom, float width, float height)
        {
            rt.anchorMin = new Vector2(left / RefWidth, fromBottom / RefHeight);
            rt.anchorMax = new Vector2((left + width) / RefWidth, (fromBottom + height) / RefHeight);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void PinBottomRight(RectTransform rt, float right, float fromBottom, float width, float height)
        {
            rt.anchorMin = new Vector2(1f - (right + width) / RefWidth, fromBottom / RefHeight);
            rt.anchorMax = new Vector2(1f - right / RefWidth, (fromBottom + height) / RefHeight);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void PinBottomHeight(RectTransform rt, float left, float right, float fromBottom, float height)
        {
            rt.anchorMin = new Vector2(left / RefWidth, fromBottom / RefHeight);
            rt.anchorMax = new Vector2(1f - right / RefWidth, (fromBottom + height) / RefHeight);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void PinTopHeight(RectTransform rt, float left, float right, float fromTop, float height)
        {
            rt.anchorMin = new Vector2(left / RefWidth, 1f - (fromTop + height) / RefHeight);
            rt.anchorMax = new Vector2(1f - right / RefWidth, 1f - fromTop / RefHeight);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
