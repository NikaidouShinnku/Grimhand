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

        BattleSession _session;
        CardVisualCatalogSO _catalog;
        CharacterVisualCatalogSO _characterVisuals;
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
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
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
            BattleUiLayoutRuntimeFix.ApplyIfNeeded(transform);
        }

        void ConfigureBattlefieldSlots()
        {
            for (var i = 0; i < playerSlots.Length && i < SlotOrder.Length; i++)
                playerSlots[i]?.Configure(SlotOrder[i], TeamSide.Player, "我方");
            for (var i = 0; i < enemySlots.Length && i < SlotOrder.Length; i++)
                enemySlots[i]?.Configure(SlotOrder[i], TeamSide.Enemy, "敌方");
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
            RefreshEnemyIntents(state);
            RefreshSelectedQueue(state, draft);
            RefreshTargetPrompt(state, draft);
            RefreshHand(state);
            RefreshActions(state, expeditionBlocks);
            RefreshExpeditionOverlay();
        }

        void RefreshHud(BattleState state)
        {
            if (titleText != null)
            {
                if (!_session.IsExpeditionMode)
                {
                    titleText.text =
                        $"回合 {state.TurnNumber}  ·  {state.Phase}  ·  能量 {state.EnergyCurrent}/{state.EnergyMax}  ·  {state.Outcome}";
                }
                else
                {
                    var phaseLabel = _session.Expedition.Run.Phase switch
                    {
                        ExpeditionPhase.RouteSelect => "选路线",
                        ExpeditionPhase.RunComplete => "远征完成",
                        ExpeditionPhase.RunFailed => "远征失败",
                        _ => state.Phase.ToString()
                    };
                    titleText.text =
                        $"远征 {_session.Expedition.CurrentBattleNumber}/{_session.Expedition.Run.TargetBattleCount}  ·  回合 {state.TurnNumber}  ·  {phaseLabel}  ·  能量 {state.EnergyCurrent}/{state.EnergyMax}";
                }
            }

            if (subtitleText != null)
            {
                if (_session.IsExpeditionMode && _session.Expedition.Run.Party.Count > 0 &&
                    _session.Expedition.Run.Phase != ExpeditionPhase.InBattle)
                    subtitleText.text = BattleUiFormatters.FormatPartyHpLine(_session.Expedition.Run.Party);
                else
                    subtitleText.text = FormatLivePartyHp(state);
            }
        }

        static string FormatLivePartyHp(BattleState state)
        {
            var parts = new List<string>();
            foreach (var c in state.Combatants)
            {
                if (c.Team != TeamSide.Player)
                    continue;
                parts.Add($"{c.DisplayName} {c.Hp}/{c.MaxHp}");
            }

            return parts.Count == 0 ? "" : string.Join("  ·  ", parts);
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

            RefreshSlotRow(enemySlots, state, awaitingCard != null, validTargets);
            RefreshSlotRow(playerSlots, state, awaitingCard != null, validTargets);
        }

        void RefreshSlotRow(
            CombatantSlotView[] slots,
            BattleState state,
            bool targetMode,
            List<CombatantState> validTargets)
        {
            if (slots == null)
                return;

            foreach (var slot in slots)
                slot?.Refresh(state, targetMode, validTargets, _characterVisuals);
        }

        void RefreshEnemyIntents(BattleState state)
        {
            if (enemyIntentPanel == null)
                return;

            var show = state.Phase == TurnPhase.Planning && state.EnemyIntents.Count > 0;
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
                BattleUiFormatters.BuildSelectedQueueSummary(state, draft));
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
            var planning = state.Phase == TurnPhase.Planning && !expeditionBlocks;
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
            var show = phase != ExpeditionPhase.InBattle;
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
            if (keywordTooltipPanel == null || card == null || card.Keywords.Count == 0)
            {
                HideKeywordTooltip();
                return;
            }

            var body = KeywordCatalog.BuildTooltipText(card.Keywords);
            if (string.IsNullOrEmpty(body))
            {
                HideKeywordTooltip();
                return;
            }

            keywordTooltipPanel.SetActive(true);
            keywordTooltipText.text = card.DisplayName + " — 关键词\n" + body;

            if (anchor == null)
                return;

            var panel = keywordTooltipPanel.transform as RectTransform;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            panel.position = new Vector3(corners[2].x + 24f, corners[1].y + 16f, panel.position.z);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        public void HideKeywordTooltip()
        {
            if (keywordTooltipPanel != null)
                keywordTooltipPanel.SetActive(false);
        }
    }
}
