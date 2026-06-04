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
            ApplyPlanningButtonIcons();
            CombatantTooltipLayer.GetOrCreate(transform);
            EnsureActiveCardBanner();
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
            if (_uiIcons == null)
                return;

            PlanningActionButtonStyle.Apply(confirmButton, _uiIcons.ConfirmPlayIcon, "出牌");
            PlanningActionButtonStyle.Apply(skipButton, _uiIcons.SkipIcon, "空过");
        }

        void ResolveHudReferences()
        {
            if (energyValueText == null)
            {
                energyValueText = transform.Find("PlanningInfoLeft/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("PlanningBar/EnergyRow/EnergyValue")?.GetComponent<Text>();
            }

            if (hudEnergyIcon == null)
            {
                hudEnergyIcon = transform.Find("PlanningInfoLeft/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("PlanningBar/EnergyIcon")?.GetComponent<Image>();
            }

            if (titleText == null)
            {
                titleText = transform.Find("PlanningInfoLeft/Title")?.GetComponent<Text>()
                    ?? transform.Find("PlanningBar/Title")?.GetComponent<Text>();
            }
        }

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
            le.preferredWidth = 28f;
            le.preferredHeight = 28f;
            le.minWidth = 28f;
            le.minHeight = 28f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var rt = icon.rectTransform;
            rt.sizeDelta = new Vector2(28f, 28f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
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
            RefreshEnemyIntents(state, draft);
            RefreshSelectedQueue(state, draft);
            RefreshTargetPrompt(state, draft);
            RefreshHand(state);
            RefreshActions(state, expeditionBlocks);
            RefreshExpeditionOverlay();
        }

        void RefreshHud(BattleState state)
        {
            ResolveHudReferences();

            if (hudEnergyIcon != null && _uiIcons != null)
            {
                hudEnergyIcon.sprite = _uiIcons.EnergyIcon;
                hudEnergyIcon.enabled = _uiIcons.EnergyIcon != null;
                hudEnergyIcon.preserveAspect = true;
            }

            if (energyValueText != null)
                energyValueText.text = $"{state.EnergyCurrent}/{state.EnergyMax}";

            if (titleText != null)
            {
                if (!_session.IsExpeditionMode)
                {
                    titleText.text = $"回合 {state.TurnNumber}  ·  {state.Phase}  ·  {state.Outcome}";
                }
                else
                {
                    titleText.text =
                        $"远征 {_session.Expedition.CurrentBattleNumber}/{_session.Expedition.Run.TargetBattleCount}";
                }
            }

            if (subtitleText != null)
            {
                if (state.Phase == TurnPhase.Planning && _session.Engine != null)
                {
                    subtitleText.gameObject.SetActive(true);
                    var phaseLabel = _session.IsExpeditionMode
                        ? _session.Expedition.Run.Phase switch
                        {
                            ExpeditionPhase.RouteSelect => "选路线",
                            ExpeditionPhase.RunComplete => "远征完成",
                            ExpeditionPhase.RunFailed => "远征失败",
                            _ => state.Phase.ToString()
                        }
                        : state.Phase.ToString();
                    subtitleText.text = $"回合 {state.TurnNumber}  ·  {phaseLabel}";
                }
                else if (_session.IsExpeditionMode && _session.Expedition.Run.Party.Count > 0 &&
                         _session.Expedition.Run.Phase != ExpeditionPhase.InBattle)
                {
                    subtitleText.gameObject.SetActive(true);
                    subtitleText.text = BattleUiFormatters.FormatPartyHpLine(_session.Expedition.Run.Party);
                }
                else
                {
                    subtitleText.text = "";
                    subtitleText.gameObject.SetActive(false);
                }
            }
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

            RefreshSlotRow(enemySlots, state, awaitingCard != null, validTargets, _session.PresentationSnapshot);
            RefreshSlotRow(playerSlots, state, awaitingCard != null, validTargets, _session.PresentationSnapshot);
        }

        void RefreshSlotRow(
            CombatantSlotView[] slots,
            BattleState state,
            bool targetMode,
            List<CombatantState> validTargets,
            PresentationSnapshot presentation)
        {
            if (slots == null)
                return;

            foreach (var slot in slots)
                slot?.Refresh(state, targetMode, validTargets, _characterVisuals, _uiIcons, presentation);
        }

        void RefreshEnemyIntents(BattleState state, PlanningDraft draft)
        {
            if (enemyIntentPanel == null)
                return;

            var planning = state.Phase == TurnPhase.Planning;
            var mergedQueue = planning && draft != null && draft.SelectedQueue.Count > 0;
            var show = planning && !mergedQueue && state.EnemyIntents.Count > 0;
            enemyIntentPanel.SetActive(show);
            if (!show || enemyIntentText == null)
                return;

            var lines = new List<string>();
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
                    lines.Add($"#{order} ? ({actorName})");
                else
                {
                    var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
                    lines.Add($"#{order} {card.DisplayName} 费{card.Cost} {effect} ({actorName})");
                }

                order++;
            }

            enemyIntentText.text = string.Join("\n", lines);
        }

        void RefreshSelectedQueue(BattleState state, PlanningDraft draft)
        {
            if (selectedQueuePanel == null)
                return;

            var show = state.Phase == TurnPhase.Planning && draft.SelectedQueue.Count > 0;
            selectedQueuePanel.SetActive(show);
            if (!show || selectedQueueText == null)
                return;

            selectedQueueText.text = string.Join("\n",
                BattleUiFormatters.BuildActionOrderSummary(
                    state, draft, _session.Engine.PreviewResolutionSteps()));
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
            targetPromptText.text = card != null
                ? $"请选择 {card.DisplayName} 的目标（点选高亮敌人）"
                : "请选择目标";
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
            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = planning && _session.Engine.Draft.SelectedQueue.Count > 0;
            }

            if (skipButton != null)
                skipButton.interactable = planning;

            if (restartButtonLabel != null)
                restartButtonLabel.text = _session.IsExpeditionMode ? "重开远征" : "重开战斗";
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
                    $"三场战斗全胜。\n{BattleUiFormatters.FormatPartyHpLine(_session.Expedition.Run.Party)}";
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
                BattleUiFormatters.FormatPartyHpLine(_session.Expedition.Run.Party);

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
            var playerStage = transform.Find("PlayerStage");
            var enemyStage = transform.Find("EnemyStage");
            if (playerStage != null && enemyStage != null)
                return (playerStage.position + enemyStage.position) * 0.5f;

            return transform.position;
        }
    }
}
