using System.Collections.Generic;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleScreenView : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Image hudEnergyIcon;
        [SerializeField] Text energyValueText;

        [Header("Battlefield")]
        [SerializeField] CombatantSlotView[] playerSlots = new CombatantSlotView[3];
        [SerializeField] CombatantSlotView[] enemySlots = new CombatantSlotView[3];

        [Header("Panels")]
        [SerializeField] HandPanelView handPanel;
        [SerializeField] Text enemyIntentText;
        [SerializeField] GameObject enemyIntentPanel;
        [SerializeField] Text selectedQueueText;
        [SerializeField] GameObject selectedQueuePanel;
        [SerializeField] Text targetPromptText;
        [SerializeField] GameObject targetPromptPanel;

        [Header("Actions")]
        [SerializeField] Button confirmButton;
        [SerializeField] Button skipButton;
        [SerializeField] Button restartButton;
        [SerializeField] Text restartButtonLabel;

        [Header("Tooltip")]
        [SerializeField] GameObject keywordTooltipPanel;
        [SerializeField] Text keywordTooltipText;

        [Header("Expedition Overlay")]
        [SerializeField] GameObject expeditionOverlay;
        [SerializeField] GameObject routeSelectPanel;
        [SerializeField] Transform routeButtonRoot;
        [SerializeField] Button routeButtonPrefab;
        [SerializeField] Text routeHeaderText;
        [SerializeField] GameObject runEndPanel;
        [SerializeField] Text runEndTitleText;
        [SerializeField] Text runEndBodyText;
        [SerializeField] Button runRestartButton;

        readonly List<Button> _routeButtons = new();
        BattleActiveCardBanner _activeCardBanner;
        BattleInventoryPanelView _inventoryPanel;
        Button _inventoryButton;
        Text _inventoryFallbackLabel;

        BattleSession _session;
        System.Func<bool> _presentationBusy;
        CardVisualCatalogSO _catalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        static readonly FormationSlot[] SlotOrder =
        {
            FormationSlot.Front,
            FormationSlot.Middle,
            FormationSlot.Back
        };

        public void Initialize(
            BattleSession session,
            CardVisualCatalogSO catalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();

            ConfigureBattlefieldSlots();

            confirmButton.onClick.AddListener(() => _session?.CommitPlan());
            skipButton.onClick.AddListener(() => _session?.SkipTurn());
            restartButton.onClick.AddListener(() => _session?.RestartRunOrBattle());
            runRestartButton.onClick.AddListener(() => _session?.RestartRunOrBattle());

            foreach (var slot in playerSlots)
                slot?.SetSelectHandler(id => _session?.AssignTarget(id));
            foreach (var slot in enemySlots)
                slot?.SetSelectHandler(id => _session?.AssignTarget(id));

            HideKeywordTooltip();
            ConfigureKeywordTooltipRaycast();
            ApplyTypographyPolish();
            ResolveHudReferences();
            BattleUiLayoutRuntimeFix.ApplyIfNeeded(transform);
            EnsurePlanningEnergyHud();
            EnsureInventoryHud();
            ApplyPlanningButtonIcons();
            CombatantTooltipLayer.GetOrCreate(transform);
            EnsureActiveCardBanner();

            if (GetComponent<BattleUiBootstrap>() == null)
                gameObject.AddComponent<BattleUiBootstrap>();
        }

        public void SetPresentationBusyCheck(System.Func<bool> check) => _presentationBusy = check;

        void EnsureActiveCardBanner()
        {
            if (_activeCardBanner != null || handPanel == null)
                return;

            var prefab = handPanel.CardPrefab;
            if (prefab == null)
                return;

            _activeCardBanner = gameObject.AddComponent<BattleActiveCardBanner>();
            _activeCardBanner.Initialize(
                _session,
                prefab,
                _catalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                transform);
        }

        public void ShowActiveCard(int cardInstanceId) =>
            _activeCardBanner?.Show(cardInstanceId);

        public void HideActiveCard() =>
            _activeCardBanner?.Hide();

        void ApplyPlanningButtonIcons()
        {
            if (restartButton != null)
                restartButton.gameObject.SetActive(false);

            if (_uiIcons == null)
                return;

            PlanningActionButtonStyle.Apply(confirmButton, _uiIcons.ConfirmPlayIcon, "出牌");
            PlanningActionButtonStyle.Apply(skipButton, _uiIcons.SkipIcon, "空过");

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.transform.SetSiblingIndex(0);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.transform.SetSiblingIndex(1);
            }

            var actions = transform.Find("HudChromeRoot/PlanningActionsRight")
                ?? transform.Find("PlanningActionsRight");
            BattleUiLayoutRuntimeFix.FixActionBarPublic(actions?.Find("ActionBar"));
        }

        void ResolveHudReferences()
        {
            if (energyValueText == null)
            {
                energyValueText = transform.Find("HudChromeRoot/EnergyHud/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("EnergyHud/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("PlanningInfoLeft/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("PlanningBar/EnergyRow/EnergyValue")?.GetComponent<Text>();
            }

            if (hudEnergyIcon == null)
            {
                hudEnergyIcon = transform.Find("HudChromeRoot/EnergyHud/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("EnergyHud/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("PlanningInfoLeft/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("PlanningBar/EnergyIcon")?.GetComponent<Image>();
            }

            if (titleText == null)
            {
                titleText = transform.Find("HudChromeRoot/PlanningInfoLeft/Title")?.GetComponent<Text>()
                    ?? transform.Find("PlanningInfoLeft/Title")?.GetComponent<Text>()
                    ?? transform.Find("PlanningBar/Title")?.GetComponent<Text>();
            }
        }

        Transform HudRoot => BattleUiLayoutRuntimeFix.GetHudChromeRoot(transform) ?? transform;

        void ApplyTypographyPolish()
        {
            if (titleText != null)
                titleText.fontSize = Mathf.Max(titleText.fontSize, 24);
            if (subtitleText != null)
                subtitleText.fontSize = Mathf.Max(subtitleText.fontSize, 16);
            if (enemyIntentText != null)
                enemyIntentText.fontSize = Mathf.Max(enemyIntentText.fontSize, 17);
            if (targetPromptText != null)
                targetPromptText.fontSize = Mathf.Max(targetPromptText.fontSize, 19);
            if (selectedQueueText != null)
                selectedQueueText.fontSize = Mathf.Max(selectedQueueText.fontSize, 14);
            if (keywordTooltipText != null)
                keywordTooltipText.fontSize = Mathf.Max(keywordTooltipText.fontSize, 16);
            if (energyValueText != null)
                energyValueText.fontSize = Mathf.Max(energyValueText.fontSize, 24);
            FixEnergyIconLayout(hudEnergyIcon);
        }

        public static void FixEnergyIconLayout(Image icon)
        {
            if (icon == null)
                return;

            var le = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 32f;
            le.preferredHeight = 32f;
            le.minWidth = 32f;
            le.minHeight = 32f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var rt = icon.rectTransform;
            rt.sizeDelta = new Vector2(32f, 32f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        void EnsurePlanningEnergyHud()
        {
            ResolveHudReferences();

            var info = transform.Find("PlanningInfoLeft") ?? transform.Find("PlanningBar");
            var legacyGoldRow = info?.Find("GoldRow");
            if (legacyGoldRow != null)
                Destroy(legacyGoldRow.gameObject);

            var legacyEnergyRow = info?.Find("EnergyRow") as RectTransform;

            var energyHud = HudRoot.Find("EnergyHud") as RectTransform;
            if (energyHud == null)
            {
                var hudGo = new GameObject("EnergyHud", typeof(RectTransform), typeof(Image));
                hudGo.transform.SetParent(HudRoot, false);
                energyHud = hudGo.GetComponent<RectTransform>();
                var hudBg = hudGo.GetComponent<Image>();
                hudBg.color = new Color(0.1f, 0.11f, 0.15f, 0.92f);
                hudBg.raycastTarget = false;
            }

            var energyRow = energyHud.Find("EnergyRow") as RectTransform;
            if (energyRow == null)
            {
                if (legacyEnergyRow != null)
                {
                    energyRow = legacyEnergyRow;
                    energyRow.SetParent(energyHud, false);
                }
                else
                {
                    var rowGo = new GameObject("EnergyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                    rowGo.transform.SetParent(energyHud, false);
                    energyRow = rowGo.GetComponent<RectTransform>();
                }
            }

            energyRow.anchorMin = Vector2.zero;
            energyRow.anchorMax = Vector2.one;
            energyRow.pivot = new Vector2(0.5f, 0.5f);
            energyRow.offsetMin = new Vector2(8f, 6f);
            energyRow.offsetMax = new Vector2(-8f, -6f);
            energyRow.gameObject.SetActive(true);

            var layout = energyRow.GetComponent<HorizontalLayoutGroup>()
                ?? energyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (hudEnergyIcon == null && legacyEnergyRow != null)
                hudEnergyIcon = legacyEnergyRow.Find("EnergyIcon")?.GetComponent<Image>();
            if (energyValueText == null && legacyEnergyRow != null)
                energyValueText = legacyEnergyRow.Find("EnergyValue")?.GetComponent<Text>();

            if (hudEnergyIcon != null)
            {
                hudEnergyIcon.transform.SetParent(energyRow, false);
                hudEnergyIcon.transform.SetAsFirstSibling();
                hudEnergyIcon.gameObject.SetActive(true);
                FixEnergyIconLayout(hudEnergyIcon);
            }

            if (energyValueText != null)
            {
                if (energyValueText.transform.parent != energyRow)
                    energyValueText.transform.SetParent(energyRow, false);
                energyValueText.gameObject.SetActive(true);
                energyValueText.fontSize = Mathf.Max(energyValueText.fontSize, 24);
                energyValueText.fontStyle = FontStyle.Bold;
                energyValueText.color = Color.white;
            }

            if (legacyEnergyRow != null && legacyEnergyRow != energyRow)
                legacyEnergyRow.gameObject.SetActive(false);

            BattleUiLayoutRuntimeFix.LayoutEnergyHud(energyHud);
            LayoutRebuilder.ForceRebuildLayoutImmediate(energyRow);
        }

        void EnsureInventoryHud()
        {
            if (_inventoryButton == null)
            {
                var go = new GameObject("InventoryButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(HudRoot, false);

                var img = go.GetComponent<Image>();
                img.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
                img.raycastTarget = true;

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(go.transform, false);
                _inventoryFallbackLabel = labelGo.GetComponent<Text>();
                _inventoryFallbackLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _inventoryFallbackLabel.fontSize = 18;
                _inventoryFallbackLabel.fontStyle = FontStyle.Bold;
                _inventoryFallbackLabel.alignment = TextAnchor.MiddleCenter;
                _inventoryFallbackLabel.color = new Color(0.92f, 0.94f, 0.98f, 1f);
                _inventoryFallbackLabel.text = "包";
                _inventoryFallbackLabel.raycastTarget = false;
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                _inventoryButton = go.GetComponent<Button>();
                _inventoryButton.targetGraphic = img;
                _inventoryButton.onClick.AddListener(ToggleInventoryPanel);

                _inventoryPanel = gameObject.AddComponent<BattleInventoryPanelView>();
                _inventoryPanel.Initialize(_session, transform);
            }

            ApplyLateHudLayout();
        }

        public void ApplyLateHudLayout()
        {
            EnsurePlanningEnergyHud();
            ApplyInventoryButtonLayout();
            ApplyPlanningButtonIcons();
            BattleUiLayoutRuntimeFix.RefreshBottomHud(transform);
        }

        public void NotifyLayoutApplied()
        {
            handPanel?.ReapplyPoolLayout();
            _inventoryPanel?.Refresh();

            var info = transform.Find("HudChromeRoot/PlanningInfoLeft") ?? transform.Find("PlanningInfoLeft");
            var intent = transform.Find("HudChromeRoot/EnemyIntentPanel") ?? transform.Find("EnemyIntentPanel");
            var actions = transform.Find("HudChromeRoot/PlanningActionsRight") ?? transform.Find("PlanningActionsRight");
            info?.gameObject.SetActive(false);
            intent?.gameObject.SetActive(true);
            actions?.gameObject.SetActive(true);
            EnsurePlanningEnergyHud();
            ApplyPlanningButtonIcons();
            BattleUiLayoutRuntimeFix.RefreshBottomHud(transform);
            _activeCardBanner?.Relayout();
        }

        void EnsurePlanningChromeVisible()
        {
            var info = transform.Find("HudChromeRoot/PlanningInfoLeft") ?? transform.Find("PlanningInfoLeft");
            var intent = transform.Find("HudChromeRoot/EnemyIntentPanel") ?? transform.Find("EnemyIntentPanel");
            var actions = transform.Find("HudChromeRoot/PlanningActionsRight") ?? transform.Find("PlanningActionsRight");

            info?.gameObject.SetActive(false);
            intent?.gameObject.SetActive(true);
            actions?.gameObject.SetActive(true);
        }

        void ApplyInventoryButtonLayout()
        {
            if (_inventoryButton == null)
                return;

            var rt = _inventoryButton.transform as RectTransform;
            BattleUiLayoutRuntimeFix.LayoutInventoryButton(rt);

            var img = _inventoryButton.GetComponent<Image>();
            var icon = _uiIcons != null ? _uiIcons.InventoryIcon : null;
            if (icon != null)
            {
                img.sprite = icon;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
                if (_inventoryFallbackLabel != null)
                    _inventoryFallbackLabel.enabled = false;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
                if (_inventoryFallbackLabel != null)
                    _inventoryFallbackLabel.enabled = true;
            }

        }

        void ToggleInventoryPanel()
        {
            _inventoryPanel?.Toggle();
            if (_inventoryPanel != null && _inventoryPanel.IsOpen)
                _inventoryPanel.Refresh();
        }

        void ConfigureBattlefieldSlots()
        {
            for (var i = 0; i < playerSlots.Length && i < SlotOrder.Length; i++)
                playerSlots[i]?.Configure(SlotOrder[i], TeamSide.Player, "我方", mirror: false);
            for (var i = 0; i < enemySlots.Length && i < SlotOrder.Length; i++)
                enemySlots[i]?.Configure(SlotOrder[i], TeamSide.Enemy, "敌方", mirror: true);
        }

        void ConfigureKeywordTooltipRaycast()
        {
            if (keywordTooltipPanel == null)
                return;

            foreach (var graphic in keywordTooltipPanel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        public void Refresh()
        {
            if (_session?.Engine == null)
                return;

            var state = _session.Engine.State;
            var draft = _session.Engine.Draft;
            var expeditionBlocks = _session.ExpeditionBlocksInput;

            RefreshHud(state);
            RefreshBattlefield(state, draft);
            RefreshActionTimeline(state, draft);
            RefreshTargetPrompt(state, draft);
            RefreshHand(state);
            RefreshActions(state, expeditionBlocks);
            RefreshExpeditionOverlay();
            _inventoryPanel?.Refresh();
            EnsurePlanningChromeVisible();
        }

        void RefreshHud(BattleState state)
        {
            ResolveHudReferences();

            if (hudEnergyIcon != null && _uiIcons != null)
            {
                hudEnergyIcon.sprite = _uiIcons.EnergyIcon;
                hudEnergyIcon.enabled = true;
                hudEnergyIcon.preserveAspect = true;
                hudEnergyIcon.color = _uiIcons.EnergyIcon != null ? Color.white : new Color(0.75f, 0.55f, 1f, 1f);
                FixEnergyIconLayout(hudEnergyIcon);
            }

            if (energyValueText != null)
            {
                energyValueText.gameObject.SetActive(true);
                energyValueText.text = $"{state.EnergyCurrent}/{state.EnergyMax}";
                energyValueText.color = Color.white;
            }

            var energyRow = hudEnergyIcon != null ? hudEnergyIcon.transform.parent as RectTransform : null;
            if (energyRow != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(energyRow);

            if (_inventoryButton != null)
                ApplyInventoryButtonLayout();

            if (titleText != null)
                titleText.gameObject.SetActive(false);

            if (subtitleText != null)
                subtitleText.gameObject.SetActive(false);
        }

        void RefreshBattlefield(BattleState state, PlanningDraft draft)
        {
            var awaiting = draft.AwaitingTargetCardId;
            CardInstanceState awaitingCard = null;
            CombatantState owner = null;
            List<CombatantState> validTargets = null;

            if (awaiting != null)
            {
                awaitingCard = state.GetCard(awaiting.Value);
                if (awaitingCard != null)
                {
                    var ownerId = PositionRules.GetOwnerCombatantId(state, awaitingCard);
                    owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                    validTargets = CardRules.GetValidTargetCandidates(state, awaitingCard, owner);
                }
            }

            RefreshSlotRow(enemySlots, state, awaitingCard != null, validTargets, _session.PresentationSnapshot, showExpBar: false);
            RefreshSlotRow(playerSlots, state, awaitingCard != null, validTargets, _session.PresentationSnapshot,
                showExpBar: _session.IsExpeditionMode);
        }

        void RefreshSlotRow(
            CombatantSlotView[] slots,
            BattleState state,
            bool targetMode,
            List<CombatantState> validTargets,
            PresentationSnapshot presentation,
            bool showExpBar)
        {
            if (slots == null)
                return;

            foreach (var slot in slots)
                slot?.Refresh(state, targetMode, validTargets, _characterVisuals, _uiIcons, presentation, showExpBar);
        }

        void RefreshActionTimeline(BattleState state, PlanningDraft draft)
        {
            selectedQueuePanel?.SetActive(false);

            if (enemyIntentPanel == null)
                return;

            var planning = state.Phase == TurnPhase.Planning;
            enemyIntentPanel.SetActive(planning);
            if (!planning || enemyIntentText == null)
                return;

            var hasPlayerCards = draft != null && draft.SelectedQueue.Count > 0;
            var hasEnemyIntents = state.EnemyIntents.Count > 0;
            if (!hasPlayerCards && !hasEnemyIntents)
            {
                enemyIntentText.text = "【敌方意图】\n（暂无）";
                return;
            }

            if (hasPlayerCards)
            {
                var lines = BattleUiFormatters.BuildActionOrderSummary(
                    state, draft, _session.Engine.PreviewResolutionSteps());
                enemyIntentText.text = "【行动顺序】\n" + string.Join("\n", lines);
                return;
            }

            var intentLines = new List<string> { "【敌方意图】" };
            var order = 1;
            foreach (var intent in state.EnemyIntents)
            {
                var card = state.GetCard(intent.CardInstanceId);
                if (card == null)
                    continue;

                var owner = !string.IsNullOrEmpty(intent.OwnerCombatantId)
                    ? state.GetCombatant(intent.OwnerCombatantId)
                    : null;
                if (owner == null)
                {
                    var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                    owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                }

                var actorName = owner != null ? owner.DisplayName : "敌";
                if (intent.IsHidden)
                    intentLines.Add($"#{order} ? ({actorName})");
                else
                {
                    var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
                    intentLines.Add($"#{order} {card.DisplayName} 费{card.Cost} {effect} ({actorName})");
                }

                order++;
            }

            enemyIntentText.text = string.Join("\n", intentLines);
        }

        void RefreshSelectedQueue(BattleState state, PlanningDraft draft)
        {
            selectedQueuePanel?.SetActive(false);
        }

        void RefreshTargetPrompt(BattleState state, PlanningDraft draft)
        {
            if (targetPromptPanel == null)
                return;

            var awaiting = draft.AwaitingTargetCardId;
            var show = state.Phase == TurnPhase.Planning && awaiting != null;
            targetPromptPanel.SetActive(show);
            if (!show || targetPromptText == null)
                return;

            var card = state.GetCard(awaiting.Value);
            if (card == null)
            {
                targetPromptText.text = "请点击高亮单位选择目标";
                return;
            }

            var side = CardRules.GetRequiredTargetPick(card);
            var sideLabel = side switch
            {
                TargetPickSide.Ally => "队友",
                TargetPickSide.Enemy => "敌人",
                _ => "目标"
            };
            targetPromptText.text =
                $"已选「{card.DisplayName}」— 点击高亮的{sideLabel}（再点卡牌取消）";
        }

        void RefreshHand(BattleState state)
        {
            handPanel?.Refresh(
                state,
                _session,
                _catalog,
                _uiIcons,
                _characterVisuals,
                _definitions,
                id =>
                {
                    _session.ToggleCard(id);
                    Refresh();
                },
                ShowKeywordTooltip,
                HideKeywordTooltip);
        }

        void RefreshActions(BattleState state, bool expeditionBlocks)
        {
            var planning = state.Phase == TurnPhase.Planning
                && !expeditionBlocks
                && !(_presentationBusy?.Invoke() ?? false);

            if (enemyIntentPanel != null && state.Phase == TurnPhase.Planning)
                enemyIntentPanel.SetActive(true);

            var actionsRoot = transform.Find("HudChromeRoot/PlanningActionsRight")
                ?? transform.Find("PlanningActionsRight");
            if (actionsRoot != null)
                actionsRoot.gameObject.SetActive(true);

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = planning && _session.Engine.Draft.SelectedQueue.Count > 0;
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.interactable = planning;
            }

            ApplyPlanningButtonIcons();
        }

        void RefreshExpeditionOverlay()
        {
            if (expeditionOverlay == null || !_session.IsExpeditionMode)
            {
                expeditionOverlay?.SetActive(false);
                return;
            }

            var phase = _session.Expedition.Run.Phase;
            var show = phase != ExpeditionPhase.InBattle && !(_presentationBusy?.Invoke() ?? false);
            expeditionOverlay.SetActive(show);
            if (!show)
                return;

            routeSelectPanel.SetActive(phase == ExpeditionPhase.RouteSelect);
            runEndPanel.SetActive(phase is ExpeditionPhase.RunComplete or ExpeditionPhase.RunFailed);

            if (phase == ExpeditionPhase.RouteSelect)
                RefreshRouteButtons();
            else if (phase == ExpeditionPhase.RunComplete)
            {
                runEndTitleText.text = "远征完成";
                runEndBodyText.text =
                    $"三场战斗全胜。\n{BattleUiFormatters.FormatPartySummary(_session.Expedition.Run.Party, _session.Expedition.Run.Gold)}";
            }
            else if (phase == ExpeditionPhase.RunFailed)
            {
                runEndTitleText.text = "远征失败";
                runEndBodyText.text = "队伍无法继续。可重开远征再试。";
            }
        }

        void RefreshRouteButtons()
        {
            foreach (var b in _routeButtons)
            {
                if (b != null)
                    Destroy(b.gameObject);
            }

            _routeButtons.Clear();

            var routes = _session.Expedition.Run.PendingRoutes;
            routeHeaderText.text =
                $"选择前进路线（已完成 {_session.Expedition.Run.BattlesWon}/{_session.Expedition.Run.TargetBattleCount} 场）\n" +
                $"本场 +{_session.Expedition.Run.LastGoldReward} 金币\n" +
                BattleUiFormatters.FormatPartySummary(
                    _session.Expedition.Run.Party,
                    _session.Expedition.Run.Gold);

            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var index = i;
                var btn = Instantiate(routeButtonPrefab, routeButtonRoot);
                btn.gameObject.SetActive(true);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text =
                        $"{route.DisplayName}\n[{BattleUiFormatters.DescribeNodeType(route.NodeType)}]\n\n{route.Description}";
                }

                btn.onClick.AddListener(() => _session.SelectRoute(index));
                _routeButtons.Add(btn);
            }
        }

        void ShowKeywordTooltip(CardInstanceState card, RectTransform anchor)
        {
            // 卡牌悬停已在牌面展示描述；额外 tooltip 容易引发布局抖动，暂不弹出。
            HideKeywordTooltip();
        }

        void PositionTooltipAboveCard(RectTransform panel, RectTransform anchor)
        {
            var canvasRt = panel.GetComponentInParent<Canvas>()?.transform as RectTransform;
            if (canvasRt == null)
                return;

            var anchorCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);
            var anchorCenterX = (anchorCorners[0].x + anchorCorners[2].x) * 0.5f;
            var anchorTopY = anchorCorners[1].y;
            var anchorBottomY = anchorCorners[0].y;

            var canvasCorners = new Vector3[4];
            canvasRt.GetWorldCorners(canvasCorners);
            var canvasLeft = canvasCorners[0].x;
            var canvasRight = canvasCorners[2].x;
            var canvasTop = canvasCorners[1].y;
            var canvasBottom = canvasCorners[0].y;
            const float handReserved = 300f;

            panel.pivot = new Vector2(0.5f, 0f);
            panel.position = new Vector3(anchorCenterX, anchorTopY + 14f, panel.position.z);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            ClampTooltipX(panel, canvasLeft, canvasRight);

            var panelCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners);

            if (panelCorners[1].y > canvasTop - 8f)
            {
                panel.pivot = new Vector2(0.5f, 1f);
                panel.position = new Vector3(panel.position.x, anchorBottomY - 14f, panel.position.z);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
                panel.GetWorldCorners(panelCorners);
            }

            if (panelCorners[0].y < canvasBottom + handReserved)
            {
                panel.pivot = new Vector2(0.5f, 0f);
                panel.position = new Vector3(panel.position.x, canvasBottom + handReserved + 8f, panel.position.z);
                ClampTooltipX(panel, canvasLeft, canvasRight);
            }
        }

        static void ClampTooltipX(RectTransform panel, float canvasLeft, float canvasRight)
        {
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            var shift = 0f;
            if (corners[2].x > canvasRight - 12f)
                shift = canvasRight - 12f - corners[2].x;
            else if (corners[0].x < canvasLeft + 12f)
                shift = canvasLeft + 12f - corners[0].x;

            if (Mathf.Abs(shift) > 0.01f)
                panel.position += new Vector3(shift, 0f, 0f);
        }

        public void HideKeywordTooltip()
        {
            if (keywordTooltipPanel != null)
                keywordTooltipPanel.SetActive(false);
        }

        public System.Collections.Generic.IEnumerable<CombatantPortraitView> AllPortraitViews()
        {
            foreach (var slot in playerSlots)
            {
                if (slot?.PortraitView != null)
                    yield return slot.PortraitView;
            }

            foreach (var slot in enemySlots)
            {
                if (slot?.PortraitView != null)
                    yield return slot.PortraitView;
            }
        }

        public void BeginPlanningIdleLoops()
        {
            foreach (var slot in playerSlots)
                slot?.PortraitView?.BeginPlanningIdle();
            foreach (var slot in enemySlots)
                slot?.PortraitView?.BeginPlanningIdle();
        }

        public void StopAllPortraitIdleLoops()
        {
            foreach (var view in AllPortraitViews())
                view?.StopIdleLoop();
        }

        public Vector3 GetDuelCenterWorldPosition()
        {
            var playerFeet = GetTeamFeetReference(playerSlots);
            var enemyFeet = GetTeamFeetReference(enemySlots);

            if (playerFeet.HasValue && enemyFeet.HasValue)
            {
                return new Vector3(
                    (playerFeet.Value.x + enemyFeet.Value.x) * 0.5f,
                    (playerFeet.Value.y + enemyFeet.Value.y) * 0.5f,
                    (playerFeet.Value.z + enemyFeet.Value.z) * 0.5f);
            }

            var playerStage = transform.Find("PlayerStage");
            var enemyStage = transform.Find("EnemyStage");
            if (playerStage != null && enemyStage != null)
                return (playerStage.position + enemyStage.position) * 0.5f;

            return transform.position;
        }

        static Vector3? GetTeamFeetReference(CombatantSlotView[] slots)
        {
            if (slots == null || slots.Length == 0)
                return null;

            var idx = slots.Length > 1 ? 1 : 0;
            for (var i = 0; i < slots.Length; i++)
            {
                var pick = slots[(idx + i) % slots.Length];
                if (pick == null || string.IsNullOrEmpty(pick.PortraitView?.CombatantId))
                    continue;

                return pick.GetFeetWorldPosition();
            }

            return slots[idx]?.GetFeetWorldPosition();
        }

        static Vector3? GetTeamDuelReference(CombatantSlotView[] slots) => GetTeamFeetReference(slots);
    }
}
