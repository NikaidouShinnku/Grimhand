using System;
using System.Collections.Generic;
using Grimhand.Battle.AI;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Battle.V091;
using Grimhand.Core;

namespace Grimhand.Battle
{
    public sealed class BattleEngine
    {
        readonly BattleState _state = new();
        readonly BattleRng _rng;
        readonly List<BattleEvent> _events = new();
        PlanningDraft _draft;

        /// <summary>演出录制：每产生一条影响 UI 展示的战斗事件后回调 (eventIndex, kind, state)。</summary>
        public Action<int, BattleEventKind, BattleState> PresentationCheckpointRecorder { get; set; }

        public BattleEngine(BattleConfig config)
        {
            _rng = new BattleRng(config.Seed);
            Initialize(config);
        }

        public BattleState State => _state;
        public IReadOnlyList<BattleEvent> Events => _events;

        public void EmitExternalEvents(System.Collections.Generic.IEnumerable<BattleEvent> events)
        {
            if (events == null)
                return;

            foreach (var e in events)
            {
                if (e != null)
                    _events.Add(e);
            }
        }

        /// <summary>测试：在手动修改 State 后重新判定胜负。</summary>
        public void EvaluateOutcomeForTests() => EvaluateOutcome();

        public PlanningDraft Draft
        {
            get
            {
                if (_draft == null)
                    _draft = new PlanningDraft(_state, _events);
                return _draft;
            }
        }

        public void ClearEvents() => _events.Clear();

        /// <summary>出牌结算已完成，但回合末抽牌/回能等需等演出结束后再执行。</summary>
        public bool EndOfTurnPending { get; private set; }

        public void FlushPendingEndOfTurn()
        {
            if (!EndOfTurnPending)
                return;

            EndOfTurnPending = false;
            if (_state.Outcome != BattleOutcome.Ongoing)
                return;

            ProcessEndOfTurn();
        }

        public void StartBattle()
        {
            if (_state.AwaitingFelskullChoice)
                return;

            SetPhase(TurnPhase.Draw);
            ProcessDrawPhase();
            BeginPlanning();
        }

        public void ResumeAfterFelskullChoice()
        {
            if (!_state.AwaitingFelskullChoice)
                return;

            _state.AwaitingFelskullChoice = false;
            SetPhase(TurnPhase.Draw);
            ProcessDrawPhase();
            BeginPlanning();
        }

        public bool ToggleCardSelection(int instanceId) => Draft.ToggleCard(instanceId);

        public bool TryBeginConsumableUse(string consumableId, int slotIndex)
        {
            if (_state.Phase != TurnPhase.Planning)
                return false;

            if (!Consumables.ConsumableDatabase.TryGet(consumableId, out var definition))
                return false;

            if (Consumables.ConsumableRules.NeedsTarget(definition))
                return Draft.TryBeginConsumableUse(consumableId, slotIndex);

            if (!Draft.TryApplyInstantConsumable(consumableId, out _))
                return false;

            return true;
        }

        public bool TryAssignConsumableTarget(string combatantId) => Draft.TryAssignConsumableTarget(combatantId);

        public void CancelConsumableTargeting() => Draft.CancelConsumableTargeting();

        /// <summary>预览本回合速度结算顺序（含应对插队，不消耗 RNG）。</summary>
        public IReadOnlyList<ResolutionStep> PreviewResolutionSteps()
        {
            var playerPlan = Draft.CommitToPlan();
            return PreviewResolutionSchedule(playerPlan);
        }

        /// <summary>预览指定我方计划下的完整结算顺序（含应对插队）。</summary>
        public IReadOnlyList<ResolutionStep> PreviewResolutionSchedule(BattlePlan playerPlan)
        {
            var baseline = SpeedResolver.BuildResolutionOrder(
                _state, playerPlan, _state.EnemyPlan, _rng.Copy());
            var schedule = RespondResolutionPlanner.BuildSchedule(_state, baseline);
            var steps = new List<ResolutionStep>(schedule.Count);
            foreach (var entry in schedule)
                steps.Add(entry.Step);
            return steps;
        }

        /// <summary>我方已选牌按届时速度结算顺序排列的 instanceId 列表。</summary>
        public List<int> GetPlayerCardsInResolveOrder()
        {
            var result = new List<int>();
            var baseline = SpeedResolver.BuildResolutionOrder(
                _state, _state.PlayerPlan, _state.EnemyPlan, _rng.Copy());
            var schedule = RespondResolutionPlanner.BuildSchedule(_state, baseline);
            foreach (var entry in schedule)
            {
                var owner = _state.GetCombatant(entry.Step.CombatantId);
                if (owner != null && owner.Team == TeamSide.Player)
                    result.Add(entry.Step.CardInstanceId);
            }

            return result;
        }

        public bool CommitPlayerPlan()
        {
            if (_state.Phase != TurnPhase.Planning)
                return false;

            if (Draft.SelectedQueue.Count == 0)
                return false;

            return CommitPlanInternal("Player plan committed", BattleEventKind.PlanCommitted);
        }

        public bool SkipPlayerTurn()
        {
            if (_state.Phase != TurnPhase.Planning)
                return false;

            Draft.RefundAllSelections();
            return CommitPlanInternal("Skip turn", BattleEventKind.TurnSkipped);
        }

        /// <summary>快速启动：规划阶段立即结算一张 quick_start 卡（仅 PvE，无需等到回合结算）。</summary>
        public bool TryResolveQuickStartCard(int instanceId)
        {
            if (_state.Phase != TurnPhase.Planning)
                return false;

            var card = _state.GetCard(instanceId);
            if (card == null || !card.Keywords.Contains("quick_start"))
                return false;
            if (!card.IsUsable || !_state.PlayerHand.Contains(card))
                return false;

            var ownerId = PositionRules.GetOwnerCombatantId(_state, card);
            var owner = ownerId != null ? _state.GetCombatant(ownerId) : null;
            if (owner == null || owner.Team != TeamSide.Player || !owner.IsAlive)
                return false;

            if (CardLockRules.ShouldBlockPlayerCardPlanning(owner, card))
                return false;

            if (CardRules.ShouldPromptForTarget(_state, card, owner)
                && !_state.ResolutionTargets.ContainsKey(instanceId))
                return false;

            var cost = TalentBattleRules.GetEffectivePlayCost(_state, owner, card);
            cost = V09NewMechanicsRules.AdjustPlayCostForHandCostZero(_state, owner, cost);
            if (!EnergyRules.CanAfford(_state.EnergyCurrent, cost))
                return false;

            _state.EnergyCurrent -= cost;
            if (CardPowerRules.UsesRemainingEnergyCost(card))
                _state.EnergySpentByCardInstanceId[card.InstanceId] = cost;

            _state.PlayerHand.Remove(card);

            ResolveCardImmediately(owner, card);

            foreach (var c in _state.Combatants)
            {
                if (c.Team == TeamSide.Player)
                    CombatModifierRules.RefreshCombatantModifiers(_state, c, _state.Config?.RunModifiers);
            }

            _events.Add(new BattleEvent(BattleEventKind.EnergyChanged, card.DisplayName)
            {
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });
            EvaluateOutcome();
            return true;
        }

        void ResolveCardImmediately(CombatantState actor, CardInstanceState card)
        {
            card = HolysunSpellbookRules.ApplyForResolution(_state.Config?.RunModifiers, actor, card);

            _events.Add(new BattleEvent(BattleEventKind.PortraitPoseChanged, actor.DisplayName)
            {
                CombatantId = actor.Id,
                CardType = card.CardType,
                CardInstanceId = card.InstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.CardResolvedStarted, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId,
                CardType = card.CardType
            });

            PassiveCardMechanicsRules.ApplyEndlessBladeSacrifice(_state, actor, card, _events, _rng);

            TalentBattleRules.OnCardAboutToResolve(_state, actor, card, _events);

            if (SpecialCardRules.IsSpecialCard(card))
                SpecialCardRules.TryResolve(_state, actor, card, _events, _rng);
            else
                EffectActionExecutor.ExecuteAll(_state, actor, card, _events, _rng);
            ConsumableRules.RecordLastPlayerAttackCard(_state, actor, card);
            RelicBattleRules.TryApplyStatusCardTeamBlock(_state, actor, card, _events);
            RelicEffectRules.OnCardResolved(_state, actor, card, _events, _rng);
            PassiveCardMechanicsRules.OnEndlessBladeResolved(_state, card, _events);
            if (card.DefinitionId == PassiveCardMechanicsRules.SpiderFatalBindCardId)
                PassiveCardMechanicsRules.OnSpiderFatalBindResolved(_state, actor, card, _events, _rng);

            TrySelfDestructAfterCard(actor, card);

            if (card.Keywords.Contains("exhaust") || card.IsBonusHandCard)
            {
                if (actor.Team == TeamSide.Player)
                    PassiveCardMechanicsRules.RecordExpeditionExhaustCardPlayed(_state, _events);
                DeckRules.ExhaustCard(_state, actor.Team, card, _events);
            }
            else
                DeckRules.MovePlayedCardToDiscard(_state, actor.Team, card, _events);

            _events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.PortraitIdleRestored, actor.DisplayName)
            {
                CombatantId = actor.Id
            });

            MaybeResolveHolyInfusionFollowUp(card);
        }

        void MaybeResolveHolyInfusionFollowUp(CardInstanceState card)
        {
            if (card?.DefinitionId != PassiveCardMechanicsRules.HolyInfusionCardId)
                return;

            ResolveHolyInfusionFollowUp(card.InstanceId);
        }

        void ResolveHolyInfusionFollowUp(int holyInfusionCardInstanceId)
        {
            if (!PassiveCardMechanicsRules.TryGetHolyInfusionRepeatTarget(
                    _state, holyInfusionCardInstanceId, out var repeatCardInstanceId))
                return;

            ResolveRepeatedCardPlay(repeatCardInstanceId, holyInfusionCardInstanceId);
        }

        /// <summary>神圣灌注：让上一张牌的所属角色再打出该牌一次（不再次进弃牌/消耗）。</summary>
        void ResolveRepeatedCardPlay(int cardInstanceId, int holyInfusionCardInstanceId)
        {
            var card = _state.GetCard(cardInstanceId);
            if (card == null)
                return;

            var ownerId = PositionRules.GetOwnerCombatantId(_state, card);
            var actor = ownerId != null ? _state.GetCombatant(ownerId) : null;
            if (actor == null || !actor.IsAlive)
                return;

            if (_state.PlayerPlan.TargetByCardInstanceId.TryGetValue(cardInstanceId, out var targetId))
                _state.ResolutionTargets[cardInstanceId] = targetId;

            card = HolysunSpellbookRules.ApplyForResolution(_state.Config?.RunModifiers, actor, card);
            var eventStart = _events.Count;

            _events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{actor.DisplayName} 神圣灌注：再打出 {card.DisplayName}")
            {
                CombatantId = actor.Id,
                CardInstanceId = holyInfusionCardInstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.PortraitPoseChanged, actor.DisplayName)
            {
                CombatantId = actor.Id,
                CardType = card.CardType,
                CardInstanceId = card.InstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.CardResolvedStarted, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId,
                CardType = card.CardType
            });

            PassiveCardMechanicsRules.ApplyEndlessBladeSacrifice(_state, actor, card, _events, _rng);

            TalentBattleRules.OnCardAboutToResolve(_state, actor, card, _events);

            if (SpecialCardRules.IsSpecialCard(card))
                SpecialCardRules.TryResolve(_state, actor, card, _events, _rng);
            else
                EffectActionExecutor.ExecuteAll(_state, actor, card, _events, _rng);

            ConsumableRules.RecordLastPlayerAttackCard(_state, actor, card);
            RelicBattleRules.TryApplyStatusCardTeamBlock(_state, actor, card, _events);
            RelicEffectRules.OnCardResolved(_state, actor, card, _events, _rng);
            PassiveCardMechanicsRules.OnEndlessBladeResolved(_state, card, _events);
            if (card.DefinitionId == PassiveCardMechanicsRules.SpiderFatalBindCardId)
                PassiveCardMechanicsRules.OnSpiderFatalBindResolved(_state, actor, card, _events, _rng);

            TrySelfDestructAfterCard(actor, card);

            _events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.PortraitIdleRestored, actor.DisplayName)
            {
                CombatantId = actor.Id
            });

            RecordPresentationCheckpoints(eventStart);
            EvaluateOutcome();
        }

        bool CommitPlanInternal(string message, BattleEventKind kind)
        {
            var plan = Draft.CommitToPlan();
            _state.PlayerPlan.PlayQueue.Clear();
            _state.PlayerPlan.PlayQueue.AddRange(plan.PlayQueue);
            _state.PlayerPlan.EnergySpent = plan.EnergySpent;
            _state.PlayerPlan.EnergySpentPerCard.Clear();
            foreach (var pair in plan.EnergySpentPerCard)
                _state.PlayerPlan.EnergySpentPerCard[pair.Key] = pair.Value;

            _state.EnergySpentByCardInstanceId.Clear();
            foreach (var pair in plan.EnergySpentPerCard)
                _state.EnergySpentByCardInstanceId[pair.Key] = pair.Value;

            // 已提交出牌：先声/魂火节流占用不再可取消恢复（Pending 已为 false，本场不再回档）
            _state.TalentMageFirstStatusDiscountReservedInstanceId = 0;
            _state.TalentLichFirstExhaustDiscountReservedInstanceId = 0;
            TalentBattleRules.SyncSoulFireThrottleStatus(_state);

            var enemyResolutionTargets = new Dictionary<int, string>();
            foreach (var cardId in _state.EnemyPlan.PlayQueue)
            {
                if (_state.ResolutionTargets.TryGetValue(cardId, out var targetId))
                    enemyResolutionTargets[cardId] = targetId;
            }

            _state.ResolutionTargets.Clear();
            foreach (var pair in plan.TargetByCardInstanceId)
                _state.ResolutionTargets[pair.Key] = pair.Value;
            foreach (var pair in enemyResolutionTargets)
                _state.ResolutionTargets[pair.Key] = pair.Value;

            _events.Add(new BattleEvent(kind, message)
            {
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });

            Draft.Reset();
            _draft = new PlanningDraft(_state, _events);

            ResolveTurn();
            return true;
        }

        void ResolveTurn()
        {
            SetPhase(TurnPhase.SpeedResolve);
            CombatMechanicsRules.ClearTurnFlags(_state);
            CombatMechanicsRules.ClearResolveTurnFlags(_state);
            _state.RespondMitigationByEnemyCard.Clear();
            _state.PendingParryStrikes.Clear();
            _state.SuppressedEnemyCardInstanceIds.Clear();
            _state.PlayerRespondStatusUsedThisTurn = false;

            // 连击：计划中 ≥3 张战士攻击 → 结算开始即挂增伤，本回合全部攻击生效
            TalentBattleRules.TryApplyComboFromCommittedPlan(_state, _events);

            var baseline = SpeedResolver.BuildResolutionOrder(
                _state, _state.PlayerPlan, _state.EnemyPlan, _rng);
            var schedule = RespondResolutionPlanner.BuildSchedule(_state, baseline);

            foreach (var entry in schedule)
            {
                var actor = _state.GetCombatant(entry.Step.CombatantId);
                if (actor != null && actor.SkipRemainingPlaysThisTurn)
                    continue;

                if (entry.RespondContext.HasValue)
                    ResolveRespondStep(entry);
                else
                {
                    RevealIntentIfHidden(entry.Step.CardInstanceId);
                    var card = _state.GetCard(entry.Step.CardInstanceId);
                    if (RespondRules.IsRespondCard(card))
                        ResolveRespondStep(entry);
                    else
                    {
                        // 攻击先演、应对后演：伤害前预武装/注册减伤
                        if (entry.PairedRespondCardInstanceId > 0)
                            PreparePairedRespondMitigation(entry);
                        ResolveStep(entry.Step);
                    }
                }

                if (_state.Outcome != BattleOutcome.Ongoing)
                {
                    SetPhase(TurnPhase.BattleEnd);
                    return;
                }
            }

            if (_state.Outcome != BattleOutcome.Ongoing)
            {
                SetPhase(TurnPhase.BattleEnd);
                return;
            }

            EndOfTurnPending = true;
        }

        void ResolveRespondStep(ScheduledResolution entry)
        {
            var step = entry.Step;
            var actor = _state.GetCombatant(step.CombatantId);
            var card = _state.GetCard(step.CardInstanceId);
            if (actor == null || card == null || !actor.IsAlive)
                return;

            if (CardLockRules.ShouldSkipPlayerCard(actor, card))
            {
                CardLockRules.SkipLockedPlayerCard(_state, actor, card, _events);
                EvaluateOutcome();
                return;
            }

            card = HolysunSpellbookRules.ApplyForResolution(_state.Config?.RunModifiers, actor, card);

            var eventStart = _events.Count;
            // 成功应对：禁止中间 Defense 出场（规范：原位 blocking + 可选反击 Attack）
            var successfulRespond = entry.ApplyConditionalEffects && entry.RespondContext.HasValue;

            if (!successfulRespond)
            {
                _events.Add(new BattleEvent(BattleEventKind.PortraitPoseChanged, actor.DisplayName)
                {
                    CombatantId = actor.Id,
                    CardType = card.CardType,
                    CardInstanceId = card.InstanceId
                });
            }

            _events.Add(new BattleEvent(BattleEventKind.CardResolvedStarted, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId,
                CardType = card.CardType
            });

            TalentBattleRules.OnCardAboutToResolve(_state, actor, card, _events);

            // 敌方应对牌：减伤已在攻击前预武装；此处只播应对成功，勿重复武装
            if (actor.Team == TeamSide.Enemy && RespondRules.IsRespondCard(card))
            {
                if (successfulRespond)
                {
                    if (!entry.MitigationWasPreArmed)
                    {
                        DefenderRespondArmRules.TryArmFromEnemyCardResolve(
                            _state, actor, card, entry.RespondContext.Value.EnemyCardInstanceId);
                    }

                    _events.Add(new BattleEvent(BattleEventKind.ReactionTriggered, card.DisplayName)
                    {
                        CombatantId = actor.Id,
                        CardInstanceId = card.InstanceId
                    });
                }
            }
            else if (successfulRespond)
            {
                RespondEffectExecutor.Execute(
                    _state, actor, card, entry.RespondContext.Value, _events, _rng);
                // 攻击已先结算并归位：此处再打出反击（Attack 出场）
                RespondEffectExecutor.ResolvePendingParriesForEnemyCard(
                    _state, entry.RespondContext.Value.EnemyCardInstanceId, _events, _rng);
            }

            PassiveCardMechanicsRules.ApplyEndlessBladeSacrifice(_state, actor, card, _events, _rng);

            if (entry.ApplyConditionalEffects
                || (!card.Keywords.Contains("respond_status") && !card.Keywords.Contains("respond_defense")))
                EffectActionExecutor.ExecuteUnconditionalActions(_state, actor, card, _events, _rng);

            // 终焉守护：权威发放 8 护甲（卡面无条件护甲由此统一落地，避免漏执行）
            if (card.DefinitionId == PassiveCardMechanicsRules.FinalGuardCardId)
                PassiveCardMechanicsRules.ApplyFinalGuardBlock(_state, actor, _events, _rng);

            if (!entry.ApplyConditionalEffects && RespondRules.IsRespondCard(card))
                EffectActionExecutor.ExecuteFailedRespondActions(_state, actor, card, _events, _rng);
            ConsumableRules.RecordLastPlayerAttackCard(_state, actor, card);
            RelicBattleRules.TryApplyStatusCardTeamBlock(_state, actor, card, _events);
            RelicEffectRules.OnCardResolved(_state, actor, card, _events, _rng);
            PassiveCardMechanicsRules.OnEndlessBladeResolved(_state, card, _events);
            if (card.DefinitionId == PassiveCardMechanicsRules.SpiderFatalBindCardId)
                PassiveCardMechanicsRules.OnSpiderFatalBindResolved(_state, actor, card, _events, _rng);

            TrySelfDestructAfterCard(actor, card);

            if (card.Keywords.Contains("exhaust") || card.IsBonusHandCard)
            {
                if (actor.Team == TeamSide.Player)
                    PassiveCardMechanicsRules.RecordExpeditionExhaustCardPlayed(_state, _events);
                DeckRules.ExhaustCard(_state, actor.Team, card, _events);
            }
            else
                DeckRules.MovePlayedCardToDiscard(_state, actor.Team, card, _events);

            _events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId
            });
            if (!successfulRespond)
            {
                _events.Add(new BattleEvent(BattleEventKind.PortraitIdleRestored, actor.DisplayName)
                {
                    CombatantId = actor.Id
                });
            }

            MaybeResolveHolyInfusionFollowUp(card);

            RecordPresentationCheckpoints(eventStart);
            EvaluateOutcome();
        }

        /// <summary>
        /// 配对攻击结算前：注册玩家减伤层 / 武装敌方应对，保证伤害数值正确。
        /// 演出仍是攻击先到中间，再播应对牌。
        /// </summary>
        void PreparePairedRespondMitigation(ScheduledResolution attackEntry)
        {
            if (attackEntry == null || attackEntry.PairedRespondCardInstanceId <= 0)
                return;

            var respondCard = _state.GetCard(attackEntry.PairedRespondCardInstanceId);
            if (respondCard == null || !RespondRules.IsRespondCard(respondCard))
                return;

            var ownerId = PositionRules.GetOwnerCombatantId(_state, respondCard);
            var owner = _state.GetCombatant(ownerId);
            if (owner == null || !owner.IsAlive)
                return;

            var attackCardId = attackEntry.Step.CardInstanceId;
            if (owner.Team == TeamSide.Enemy)
            {
                DefenderRespondArmRules.TryArmFromEnemyCardResolve(
                    _state, owner, respondCard, attackCardId);
            }
            else
            {
                var context = RespondTriggerContext.FromStep(_state, attackEntry.Step);
                RespondEffectExecutor.PrepareMitigation(
                    _state, owner, respondCard, context, _events, _rng);
            }
        }

        void RevealIntentIfHidden(int cardInstanceId)
        {
            foreach (var intent in _state.EnemyIntents)
            {
                if (intent.CardInstanceId != cardInstanceId || !intent.IsHidden)
                    continue;

                intent.IsHidden = false;
                var card = _state.GetCard(cardInstanceId);
                if (card == null)
                    return;

                var ownerId = PositionRules.GetOwnerCombatantId(_state, card);
                var owner = ownerId != null ? _state.GetCombatant(ownerId) : null;

                _events.Add(new BattleEvent(
                    BattleEventKind.EnemyIntentPrepared,
                    CardPowerRules.DescribeCardEffect(card, owner, false))
                {
                    CardInstanceId = cardInstanceId
                });
            }
        }

        void ResolveStep(ResolutionStep step)
        {
            var actor = _state.GetCombatant(step.CombatantId);
            var card = _state.GetCard(step.CardInstanceId);
            if (actor == null || card == null || !actor.IsAlive)
                return;

            if (CardLockRules.ShouldSkipPlayerCard(actor, card))
            {
                CardLockRules.SkipLockedPlayerCard(_state, actor, card, _events);
                EvaluateOutcome();
                return;
            }

            if (actor.Team == TeamSide.Enemy
                && (_state.PendingEnemyCardSeals > 0
                    || StatusRules.HasStatus(actor, StatusCatalog.SealedNextCard)
                    || (card.CardType == CardType.Status
                        && StatusRules.HasStatus(actor, StatusCatalog.SealNextStatusCard))))
            {
                if (_state.PendingEnemyCardSeals > 0)
                    _state.PendingEnemyCardSeals--;
                else if (StatusRules.HasStatus(actor, StatusCatalog.SealedNextCard))
                    StatusRules.RemoveStatus(actor, StatusCatalog.SealedNextCard, 1, _events);
                else
                    StatusRules.RemoveStatus(actor, StatusCatalog.SealNextStatusCard, 1, _events);

                _events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                    $"{card.DisplayName} 被封印，进入弃牌堆且不生效")
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                TalentBattleRules.OnEnemyCardSealed(_state, card, _events);
                DeckRules.MovePlayedCardToDiscard(_state, actor.Team, card, _events);
                _events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                EvaluateOutcome();
                return;
            }

            if (actor.Team == TeamSide.Enemy
                && _state.SuppressedEnemyCardInstanceIds.Contains(card.InstanceId))
            {
                _events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                    $"{card.DisplayName} 被应对状态压制")
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                DeckRules.MovePlayedCardToDiscard(_state, actor.Team, card, _events);
                _events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                EvaluateOutcome();
                return;
            }

            card = HolysunSpellbookRules.ApplyForResolution(_state.Config?.RunModifiers, actor, card);

            var eventStart = _events.Count;

            _events.Add(new BattleEvent(BattleEventKind.PortraitPoseChanged, actor.DisplayName)
            {
                CombatantId = actor.Id,
                CardType = card.CardType,
                CardInstanceId = card.InstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.CardResolvedStarted, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId,
                CardType = card.CardType
            });

            PassiveCardMechanicsRules.ApplyEndlessBladeSacrifice(_state, actor, card, _events, _rng);

            TalentBattleRules.OnCardAboutToResolve(_state, actor, card, _events);

            if (SpecialCardRules.IsSpecialCard(card))
                SpecialCardRules.TryResolve(_state, actor, card, _events, _rng);
            else
                EffectActionExecutor.ExecuteAll(_state, actor, card, _events, _rng);
            ConsumableRules.RecordLastPlayerAttackCard(_state, actor, card);
            RelicBattleRules.TryApplyStatusCardTeamBlock(_state, actor, card, _events);
            RelicEffectRules.OnCardResolved(_state, actor, card, _events, _rng);
            PassiveCardMechanicsRules.OnEndlessBladeResolved(_state, card, _events);
            if (card.DefinitionId == PassiveCardMechanicsRules.SpiderFatalBindCardId)
                PassiveCardMechanicsRules.OnSpiderFatalBindResolved(_state, actor, card, _events, _rng);

            if (actor.Team == TeamSide.Enemy)
                DefenderRespondArmRules.TryArmFromEnemyCardResolve(_state, actor, card);

            TrySelfDestructAfterCard(actor, card);

            if (card.Keywords.Contains("exhaust") || card.IsBonusHandCard)
            {
                if (actor.Team == TeamSide.Player)
                    PassiveCardMechanicsRules.RecordExpeditionExhaustCardPlayed(_state, _events);
                DeckRules.ExhaustCard(_state, actor.Team, card, _events);
            }
            else
                DeckRules.MovePlayedCardToDiscard(_state, actor.Team, card, _events);

            _events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId
            });
            _events.Add(new BattleEvent(BattleEventKind.PortraitIdleRestored, actor.DisplayName)
            {
                CombatantId = actor.Id
            });

            MaybeResolveHolyInfusionFollowUp(card);

            if (actor.Team == TeamSide.Enemy)
                RespondEffectExecutor.ResolvePendingParriesForEnemyCard(
                    _state, card.InstanceId, _events, _rng);

            RecordPresentationCheckpoints(eventStart);
            EvaluateOutcome();
        }

        void RecordPresentationCheckpoints(int eventStart)
        {
            if (PresentationCheckpointRecorder == null)
                return;

            for (var i = eventStart; i < _events.Count; i++)
            {
                var e = _events[i];
                if (!BattlePresentationCheckpointKinds.ShouldRecord(e.Kind))
                    continue;

                PresentationCheckpointRecorder.Invoke(i, e.Kind, _state);
            }
        }

        void TrySelfDestructAfterCard(CombatantState actor, CardInstanceState card)
        {
            if (actor == null || card == null || !card.Keywords.Contains("self_destruct"))
                return;

            SummonRules.SelfDestruct(_state, actor, _events);
        }

        void ProcessEndOfTurn()
        {
            SetPhase(TurnPhase.EndOfTurn);

            ArchiveLastTurnAttackForConsumables();

            MinionTraitRules.PrepareTurnEndArmorRetain(_state);

            // 养精蓄锐等：依赖清甲前的护甲值
            TalentBattleRules.ProcessEndOfTurnBeforeBlockClear(_state, _events);

            foreach (var c in _state.Combatants)
            {
                // v0.9 最终壁垒：回合末仅清除50%护甲，保留 GetFinalBulwarkRetainedBlock 返回的部分
                var retained = PassiveCardMechanicsRules.GetFinalBulwarkRetainedBlock(c);
                // 灵质护盾延迟护甲：发放当回合末不清理，保留至再下一回合
                if (_state.RetainBlockOnceCombatantIds.Remove(c.Id))
                    retained = System.Math.Max(retained, c.Block);
                c.Block = retained;
            }

            DeckRules.DiscardHandAtEndOfTurn(_state, TeamSide.Player, _events);
            DeckRules.DiscardHandAtEndOfTurn(_state, TeamSide.Enemy, _events);

            StatusRules.ProcessTurnEndStatuses(_state, _events, _rng);
            StatusRules.ProcessEndOfTurnDurations(_state, _events);
            RelicEffectRules.ProcessEndOfTurn(_state, _events);
            DefenderRespondArmRules.ExpireArmsAtEndOfTurn(_state);
            _state.ConsumableDodgeBonusThisTurn = 0f;

            _state.TurnNumber++;
            SetPhase(TurnPhase.Draw);
            ProcessDrawPhase();
            BeginPlanning();
        }

        void ProcessDrawPhase()
        {
            var energyBeforeRegen = _state.EnergyCurrent;
            var isFirstPlayerTurn = _state.IsFirstPlayerTurn;
            EnergyRules.ApplyTurnStartRegen(_state);
            StatusRules.ProcessTurnStartStatuses(_state, _events, _rng);
            RelicEffectRules.ProcessTurnStart(_state, _rng, _events);
            BossTraitRules.ProcessTurnStart(_state, _events, _rng);
            MinionTraitRules.ProcessTurnStart(_state, _events);
            // v0.9 腐朽化身：回合开始给所有敌人2层中毒（永久）
            PassiveCardMechanicsRules.TryTriggerRotAvatarOnTurnStart(_state, _events);
            // v0.9 毒蛇/巫妖新机制：缠绕/延迟伤害/永恒虚无/祈求远古蛇神
            V09NewMechanicsRules.ProcessTurnStart(_state, _events, _rng);
            V091MechanicsRules.ProcessTurnStart(_state, _events, _rng);
            // v0.9 天赋：蛇 s1_lv4/s2_lv4、巫妖 s1_lv7 / 封印武装交付
            TalentBattleRules.ProcessTurnStartV09Talents(
                _state, _events, energyBeforeRegen, isFirstPlayerTurn);
            foreach (var combatant in _state.Combatants)
                AnubisAvatarRules.ProcessTurnStart(combatant);
            // 持续回合在跳伤/缠绕/延迟伤害之后结算：先生效，再扣减并到期移除
            StatusRules.ProcessTurnStartDurations(_state, _events);
            // 蓄能等：持续扣减之后再挂状态，避免当回合立刻少 1 回合
            V09NewMechanicsRules.ProcessPendingStatusesNextTurn(_state, _events);
            // 绝望之魂：战斗中获虚化时延迟到下回合开始回收
            V09NewMechanicsRules.ProcessPendingDespairSoulRecall(_state, _events);
            // 启动状态到期后再发【蛇神的回应】，与禁出牌解除同一拍
            V09NewMechanicsRules.ProcessSnakeGodResponseHand(_state, _events);
            EvaluateOutcome();
            _events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "Turn start")
            {
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });

            var bonusDraw = _state.PendingDrawNextTurn;
            var bonusCostReduce = _state.PendingDrawNextTurnCostReduction;
            var bonusEnergy = _state.PendingEnergyNextTurn;
            _state.PendingDrawNextTurn = 0;
            _state.PendingDrawNextTurnCostReduction = 0;
            _state.PendingEnergyNextTurn = 0;

            if (bonusEnergy > 0)
            {
                EnergyRules.Restore(_state, bonusEnergy);
                _events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "翡翠短刀：下回合能量")
                {
                    Energy = _state.EnergyCurrent,
                    EnergyMax = _state.EnergyMax,
                    EnergyRemaining = _state.EnergyCurrent,
                    Amount = bonusEnergy
                });
            }

            var backRowDraw = 0;
            var mods = _state.Config?.RunModifiers;
            foreach (var c in _state.Combatants)
                backRowDraw += RelicBattleRules.GetBackRowExtraDraw(_state, c, mods);

            if (mods != null && mods.RandomDiscardEachTurn && _state.PlayerHand.Count > 0)
            {
                var idx = _rng.NextIndex(_state.PlayerHand.Count);
                var discarded = _state.PlayerHand[idx];
                _state.PlayerHand.RemoveAt(idx);
                _state.PlayerDiscardPile.Add(discarded);
                _events.Add(new BattleEvent(BattleEventKind.CardDiscarded, $"混沌之心弃牌：{discarded.DisplayName}"));
            }

            if (mods != null && mods.ScryDrawPileCount > 0 && _state.PlayerDrawPile.Count > 0)
            {
                var count = System.Math.Min(mods.ScryDrawPileCount, _state.PlayerDrawPile.Count);
                var names = new System.Text.StringBuilder();
                for (var i = 0; i < count; i++)
                {
                    if (i > 0)
                        names.Append("、");
                    names.Append(_state.PlayerDrawPile[i].DisplayName);
                }

                _events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"深渊之眼：即将抽到 {names}"));
            }

            // 常规抽牌与「下回合额外抽」分开，便于对额外抽到的牌施加费用减免
            DeckRules.DrawCards(_state, TeamSide.Player, _rng,
                _state.Config.CardsDrawnPerTurn + backRowDraw +
                (_state.TurnNumber == 1 ? mods?.ExtraDrawOnBattleStart ?? 0 : 0), _events);
            if (bonusDraw > 0)
            {
                var handBeforeBonus = _state.PlayerHand.Count;
                DeckRules.DrawCards(_state, TeamSide.Player, _rng, bonusDraw, _events);
                if (bonusCostReduce > 0)
                {
                    for (var i = handBeforeBonus; i < _state.PlayerHand.Count; i++)
                    {
                        var drawn = _state.PlayerHand[i];
                        if (drawn == null)
                            continue;
                        drawn.Cost = System.Math.Max(0, drawn.Cost - bonusCostReduce);
                    }
                }
            }

            TalentBattleRules.ProcessAfterHandDrawn(_state, _rng, _events);

            DeckRules.DrawCards(_state, TeamSide.Enemy, _rng, ResolveEnemyDrawCount(), _events);
            SummonRules.GrantSkullSelfDestructHands(_state, _events);
            BossBonusHandRules.GrantPendingBonusHands(_state, _events);
        }

        int ResolveEnemyDrawCount()
        {
            var enemyDraw = _state.Config?.EnemyCardsDrawnPerTurn ?? 0;
            return enemyDraw > 0 ? enemyDraw : _state.Config.CardsDrawnPerTurn;
        }

        void ArchiveLastTurnAttackForConsumables() =>
            ConsumableRules.ArchiveTurnAttackHistory(_state);

        void BeginPlanning()
        {
            SetPhase(TurnPhase.Planning);

            if (_state.Config != null && _state.Config.ManualEnemyIntentsOnly)
            {
                // 训练场：清空残留敌方手牌，本回合意图仅由外部手动排队。
                for (var i = _state.EnemyHand.Count - 1; i >= 0; i--)
                {
                    var leftover = _state.EnemyHand[i];
                    _state.EnemyHand.RemoveAt(i);
                    if (leftover != null)
                        _state.EnemyDiscardPile.Add(leftover);
                }

                _state.EnemyPlan.PlayQueue.Clear();
                _state.EnemyPlan.EnergySpent = 0;
                _state.EnemyIntents.Clear();
            }
            else
            {
                var enemyTurn = EnemyTurnPlanner.PrepareEnemyTurn(_state, _rng);
                _state.EnemyPlan.PlayQueue.Clear();
                _state.EnemyPlan.PlayQueue.AddRange(enemyTurn.Plan.PlayQueue);
                _state.EnemyPlan.EnergySpent = enemyTurn.Plan.EnergySpent;
                _state.EnemyIntents.Clear();
                _state.EnemyIntents.AddRange(enemyTurn.Intents);
            }

            TargetRules.PrerollEnemyAutoTargets(_state, _state.EnemyPlan, _rng);

            _state.EnergySpentByCardInstanceId.Clear();
            _state.PlayerPlan.EnergySpentPerCard.Clear();

            _events.Add(new BattleEvent(BattleEventKind.EnemyIntentPrepared,
                $"Enemy intends {_state.EnemyIntents.Count} card(s)"));

            Draft.Reset();
            _draft = new PlanningDraft(_state, _events);
        }

        void SetPhase(TurnPhase phase)
        {
            _state.Phase = phase;
            _events.Add(new BattleEvent(BattleEventKind.PhaseChanged, phase.ToString()) { Phase = phase });
        }

        void EvaluateOutcome()
        {
            var playerAlive = false;
            var enemyAlive = false;

            foreach (var c in _state.Combatants)
            {
                if (!c.IsAlive)
                    continue;

                if (c.Team == TeamSide.Player)
                    playerAlive = true;
                else
                    enemyAlive = true;
            }

            if (!playerAlive)
            {
                _state.Outcome = BattleOutcome.PlayerDefeat;
                _events.Add(new BattleEvent(BattleEventKind.BattleEnded, "Defeat")
                    { Outcome = BattleOutcome.PlayerDefeat });
                return;
            }

            if (TryResolveObjectiveVictory())
                return;

            if (!enemyAlive)
            {
                _state.Outcome = BattleOutcome.PlayerVictory;
                _events.Add(new BattleEvent(BattleEventKind.BattleEnded, "Victory")
                    { Outcome = BattleOutcome.PlayerVictory });
            }
        }

        bool TryResolveObjectiveVictory()
        {
            var objectiveId = _state.Config?.VictoryOnCharacterDeathId;
            if (string.IsNullOrEmpty(objectiveId))
                return false;

            foreach (var combatant in _state.Combatants)
            {
                if (combatant.Team != TeamSide.Enemy)
                    continue;

                if (combatant.CharacterDefinitionId != objectiveId)
                    continue;

                if (combatant.IsAlive)
                    return false;
            }

            _state.Outcome = BattleOutcome.PlayerVictory;
            _events.Add(new BattleEvent(BattleEventKind.BattleEnded, "Victory")
                { Outcome = BattleOutcome.PlayerVictory });
            return true;
        }

        void Initialize(BattleConfig config)
        {
            _state.Config = config;
            _state.EnergyMax = config.EnergyCap + (config.RunModifiers?.ExtraEnergyCap ?? 0);
            _state.TurnNumber = 1;
            _state.IsFirstPlayerTurn = true;
            _state.Outcome = BattleOutcome.Ongoing;

            if (config.RunModifiers != null)
                config.RunModifiers.FirstPlayerAttackPending = true;

            _state.MiracleLeafRevivesRemaining = config.MiracleLeafRevivesRemaining;
            _state.JadeDaggerFirstKillConsumed = false;

            var deckRng = new BattleRng(config.Seed ^ 0x5DEECE66);

            foreach (var cc in config.Combatants)
                PrepareCombatantDeck(cc, deckRng);

            foreach (var cc in config.Combatants)
            {
                var combatant = new CombatantState
                {
                    Id = cc.Id,
                    DisplayName = cc.DisplayName,
                    Team = cc.Team,
                    Slot = cc.Slot,
                    CharacterDefinitionId = cc.CharacterDefinitionId,
                    Level = cc.Level,
                    Xp = cc.Xp,
                    MaxHp = cc.MaxHp,
                    BaseAttack = cc.BaseAttack,
                    BaseDefense = cc.BaseDefense,
                    Speed = cc.Speed,
                    EnteredFromExpeditionDeath = cc.EnteredFromExpeditionDeath
                };
                var startHp = cc.StartHp ?? cc.MaxHp;
                combatant.Hp = System.Math.Max(0, System.Math.Min(startHp, cc.MaxHp));
                combatant.Traits.AddRange(cc.Traits);
                if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.BossFirstHitBlock))
                    combatant.BossFirstHitBlockPending = true;
                if (MinionTraitRules.HasTrait(combatant, MinionTraitCatalog.BatFirstHitDodge))
                    combatant.FirstHitDodgePending = true;
                _state.Combatants.Add(combatant);
            }

            foreach (var cc in config.Combatants)
            {
                var combatant = _state.GetCombatant(cc.Id);
                if (combatant == null || !combatant.IsAlive)
                    continue;

                if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.SkullSelfDestructHand))
                    continue;

                var drawPile = cc.Team == TeamSide.Player ? _state.PlayerDrawPile : _state.EnemyDrawPile;
                foreach (var template in cc.DeckTemplates)
                {
                    var instance = CreateCardInstance(template, cc.Id);
                    drawPile.Add(instance);
                }
            }

            RelicBattleRules.RefreshAllDerivedStats(_state);
            TalentBattleRules.OnBattleInitialized(_state);
            V09BossMechanicsRules.ProcessBattleStart(_state, _events, _rng);
            RelicBattleRules.ApplyTeamHpBonus(_state, config.RunModifiers);

            foreach (var combatant in _state.Combatants)
                MinionTraitRules.RefreshLowHpSpeed(_state, combatant);

            ApplyBattleStartRelicEffects(config.RunModifiers);

            foreach (var combatant in _state.Combatants)
            {
                RelicEffectRules.ResetTurnFlags(combatant);
                if (!combatant.IsAlive)
                    CombatantDeathRules.OnCharacterDied(_state, combatant, _events, _rng);
            }

            DeckRules.ShuffleDrawPile(_state, TeamSide.Player, _rng, _events);
            DeckRules.ShuffleDrawPile(_state, TeamSide.Enemy, _rng, _events);
            EvaluateOutcome();

            if (config.RunModifiers?.RequiresFelskullChoice == true)
                _state.AwaitingFelskullChoice = true;
        }

        static void PrepareCombatantDeck(CombatantConfig cc, BattleRng deckRng)
        {
            if (cc.UseSkillPool && cc.SkillPoolCandidates.Count > 0)
            {
                EnemyDeckBuilder.ApplySkillPoolEntries(cc.DeckTemplates, cc.SkillPoolCandidates);
                return;
            }

            EnemyDeckBuilder.ShuffleFixedDeck(cc.DeckTemplates, deckRng);
        }

        void ApplyBattleStartRelicEffects(RunModifierSnapshot mods)
        {
            if (mods == null)
                return;

            // 燃烬之靴 / 赤红烈焰靴：挂「加速」状态，脚标与速度条可见，并随回合到期。
            if (mods.BattleStartSpeedBonus > 0 && mods.BattleStartSpeedBonusTurns > 0)
            {
                foreach (var c in _state.Combatants)
                {
                    if (c.Team != TeamSide.Player || !c.IsAlive)
                        continue;

                    StatusRules.ApplyStatus(
                        _state,
                        c,
                        StatusCatalog.SpeedUp,
                        mods.BattleStartSpeedBonus,
                        mods.BattleStartSpeedBonusTurns,
                        _events);
                }
            }

            if (mods.BattleStartTeamHeal > 0)
            {
                foreach (var c in _state.Combatants)
                {
                    if (c.Team != TeamSide.Player || !c.IsAlive)
                        continue;

                    DamageRules.ApplyHeal(_state, c, mods.BattleStartTeamHeal, _events);
                }
            }

            if (mods.BattleStartFrontBlock > 0)
            {
                foreach (var c in _state.Combatants)
                {
                    if (c.Team != TeamSide.Player || !c.IsAlive)
                        continue;

                    if (PositionRules.GetEffectiveSlot(_state, c) != FormationSlot.Front)
                        continue;

                    DamageRules.ApplyBlock(c, mods.BattleStartFrontBlock, _events, _state);
                }
            }

            if (mods.SoulRiftBattleStartRandomHpLoss > 0)
            {
                var alivePlayers = new System.Collections.Generic.List<CombatantState>();
                foreach (var c in _state.Combatants)
                {
                    if (c.Team == TeamSide.Player && c.IsAlive)
                        alivePlayers.Add(c);
                }

                if (alivePlayers.Count > 0)
                {
                    var target = alivePlayers[_rng.NextIndex(alivePlayers.Count)];
                    var loss = System.Math.Max(
                        1,
                        (int)System.Math.Round(target.MaxHp * mods.SoulRiftBattleStartRandomHpLoss / 100f));
                    target.Hp = System.Math.Max(1, target.Hp - loss);
                    _events.Add(new BattleEvent(BattleEventKind.DamageApplied,
                        $"灵魂裂隙：{target.DisplayName} 失去 {loss} 生命")
                    {
                        TargetId = target.Id,
                        Amount = loss
                    });
                }
            }

            DivinePunishmentRules.ApplyToAllEnemies(_state, _events);
        }

        CardInstanceState CreateCardInstance(CardTemplate template, string ownerCombatantId = "")
        {
            if (template != null)
            {
                GhostQueenCardCatalog.TryApplyCanonical(template);
                AbyssMonsterCardCatalog.TryApplyCanonical(template);
            }

            var id = _state.NextCardInstanceId++;
            var card = new CardInstanceState
            {
                InstanceId = id,
                DefinitionId = template.DefinitionId,
                OwnerCharacterId = template.OwnerCharacterId,
                OwnerCombatantId = ownerCombatantId ?? "",
                Cost = template.Cost,
                BaseCost = template.Cost,
                CardType = template.CardType,
                DisplayName = template.DisplayName,
                UpgradeLevel = template.UpgradeLevel,
                IsUsable = true
            };
            foreach (var action in template.Actions)
                card.Actions.Add(CloneAction(action));
            card.Keywords.AddRange(template.Keywords);
            if (CardRules.IsCurseCard(card) || CardRules.HasEngravingLock(card))
                card.IsUsable = false;
            _state.CardsById[id] = card;
            return card;
        }

        /// <summary>测试用：按模板生成一张卡牌实例并置入玩家手牌（手牌满则进弃牌堆）。</summary>
        public CardInstanceState AddCardTemplateToHand(CardTemplate template)
        {
            if (_state == null || template == null)
                return null;

            var owner = ResolveOwnerForTemplate(template);
            var instance = CreateCardInstance(template, owner?.Id ?? "");

            if (_state.PlayerHand.Count < _state.Config.HandLimit)
                _state.PlayerHand.Add(instance);
            else
                _state.PlayerDiscardPile.Add(instance);

            _events.Add(new BattleEvent(BattleEventKind.CardDrawn, instance.DisplayName)
            {
                CombatantId = owner?.Id,
                CardInstanceId = instance.InstanceId
            });
            return instance;
        }

        /// <summary>训练场：将卡牌追加到本回合意图队列。优先绑定卡牌所属角色，其次非假人存活敌人，最后假人。</summary>
        public CardInstanceState EnqueueEnemyIntentCard(CardTemplate template)
        {
            if (_state == null || template == null || _state.Phase != TurnPhase.Planning)
                return null;

            CombatantState owner = null;
            CombatantState fallbackNonDummy = null;
            CombatantState dummy = null;
            foreach (var c in _state.Combatants)
            {
                if (c.Team != TeamSide.Enemy || !c.IsAlive)
                    continue;

                if (c.CharacterDefinitionId == "char_dummy")
                {
                    dummy = c;
                    continue;
                }

                if (!string.IsNullOrEmpty(template.OwnerCharacterId)
                    && c.CharacterDefinitionId == template.OwnerCharacterId)
                {
                    owner = c;
                    break;
                }

                fallbackNonDummy ??= c;
            }

            owner ??= fallbackNonDummy ?? dummy;
            if (owner == null)
                return null;

            var instance = CreateCardInstance(template, owner.Id);
            instance.IsUsable = true;
            _state.EnemyHand.Add(instance);
            _state.EnemyPlan.PlayQueue.Add(instance.InstanceId);
            _state.EnemyPlan.EnergySpent += Math.Max(0, instance.Cost);

            var order = _state.EnemyIntents.Count;
            _state.EnemyIntents.Add(new EnemyIntentSlot
            {
                CardInstanceId = instance.InstanceId,
                OwnerCombatantId = owner.Id,
                IsHidden = false,
                OrderIndex = order
            });

            TargetRules.PrerollEnemyAutoTargets(_state, _state.EnemyPlan, _rng);

            _events.Add(new BattleEvent(BattleEventKind.EnemyIntentPrepared,
                $"手动加入意图：{instance.DisplayName}（{owner.DisplayName}）")
            {
                CombatantId = owner.Id,
                CardInstanceId = instance.InstanceId
            });
            return instance;
        }

        CombatantState ResolveOwnerForTemplate(CardTemplate template)
        {
            if (_state == null)
                return null;

            if (!string.IsNullOrEmpty(template.OwnerCharacterId))
            {
                foreach (var c in _state.Combatants)
                    if (c.Team == TeamSide.Player && c.CharacterDefinitionId == template.OwnerCharacterId)
                        return c;
            }

            foreach (var c in _state.Combatants)
                if (c.Team == TeamSide.Player && c.IsAlive)
                    return c;

            return null;
        }

        static EffectActionSpec CloneAction(EffectActionSpec source) => EffectActionSpec.Clone(source);
    }
}
