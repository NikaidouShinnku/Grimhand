using System.Collections.Generic;
using Grimhand.Battle.AI;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Battle
{
    public sealed class BattleEngine
    {
        readonly BattleState _state = new();
        readonly BattleRng _rng;
        readonly List<BattleEvent> _events = new();
        PlanningDraft _draft;

        public BattleEngine(BattleConfig config)
        {
            _rng = new BattleRng(config.Seed);
            Initialize(config);
        }

        public BattleState State => _state;
        public IReadOnlyList<BattleEvent> Events => _events;
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

        public void StartBattle()
        {
            SetPhase(TurnPhase.Draw);
            ProcessDrawPhase();
            BeginPlanning();
        }

        public bool ToggleCardSelection(int instanceId) => Draft.ToggleCard(instanceId);

        /// <summary>预览本回合速度结算顺序（不消耗 RNG）。</summary>
        public IReadOnlyList<ResolutionStep> PreviewResolutionSteps()
        {
            var playerPlan = Draft.CommitToPlan();
            return SpeedResolver.BuildResolutionOrder(_state, playerPlan, _state.EnemyPlan, _rng.Copy());
        }

        /// <summary>我方已选牌按届时速度结算顺序排列的 instanceId 列表。</summary>
        public List<int> GetPlayerCardsInResolveOrder()
        {
            var result = new List<int>();
            foreach (var step in PreviewResolutionSteps())
            {
                var owner = _state.GetCombatant(step.CombatantId);
                if (owner != null && owner.Team == TeamSide.Player)
                    result.Add(step.CardInstanceId);
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

        bool CommitPlanInternal(string message, BattleEventKind kind)
        {
            var plan = Draft.CommitToPlan();
            _state.PlayerPlan.PlayQueue.Clear();
            _state.PlayerPlan.PlayQueue.AddRange(plan.PlayQueue);
            _state.PlayerPlan.EnergySpent = plan.EnergySpent;

            _state.ResolutionTargets.Clear();
            foreach (var pair in plan.TargetByCardInstanceId)
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
            ParryRules.ClearAll(_state);
            CombatMechanicsRules.ClearTurnFlags(_state);

            var queues = SpeedResolver.BuildPlayQueues(_state, _state.PlayerPlan, _state.EnemyPlan);
            var round = 0;

            while (true)
            {
                var actors = new List<CombatantState>();
                foreach (var pair in queues)
                {
                    if (pair.Value.Count == 0)
                        continue;

                    var combatant = _state.GetCombatant(pair.Key);
                    if (combatant != null && combatant.IsAlive)
                        actors.Add(combatant);
                }

                if (actors.Count == 0)
                    break;

                var ordered = SpeedResolver.OrderByEffectiveSpeed(_state, actors, _rng);
                foreach (var actor in ordered)
                {
                    if (!queues.TryGetValue(actor.Id, out var queue) || queue.Count == 0)
                        continue;

                    var cardId = queue.Dequeue();
                    RevealIntentIfHidden(cardId);
                    ResolveStep(new ResolutionStep(actor.Id, cardId, round));

                    if (_state.Outcome != BattleOutcome.Ongoing)
                    {
                        SetPhase(TurnPhase.BattleEnd);
                        return;
                    }
                }

                round++;
            }

            if (_state.Outcome != BattleOutcome.Ongoing)
            {
                SetPhase(TurnPhase.BattleEnd);
                return;
            }

            ProcessEndOfTurn();
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

            EffectActionExecutor.ExecuteAll(_state, actor, card, _events, _rng);
            RelicBattleRules.TryApplyStatusCardTeamBlock(_state, actor, card, _events);
            RelicEffectRules.OnCardResolved(_state, actor, card, _events, _rng);

            if (card.Keywords.Contains("exhaust"))
                DeckRules.ExhaustCard(_state, actor.Team, card, _events);
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

            EvaluateOutcome();
        }

        void ProcessEndOfTurn()
        {
            SetPhase(TurnPhase.EndOfTurn);

            foreach (var c in _state.Combatants)
                c.Block = 0;

            DeckRules.DiscardHandAtEndOfTurn(_state, TeamSide.Player, _events);
            DeckRules.DiscardHandAtEndOfTurn(_state, TeamSide.Enemy, _events);

            StatusRules.ProcessEndOfTurnDurations(_state, _events);
            RelicEffectRules.ProcessEndOfTurn(_state, _events);

            _state.TurnNumber++;
            SetPhase(TurnPhase.Draw);
            ProcessDrawPhase();
            BeginPlanning();
        }

        void ProcessDrawPhase()
        {
            EnergyRules.ApplyTurnStartRegen(_state);
            StatusRules.ProcessTurnStartStatuses(_state, _events);
            RelicEffectRules.ProcessTurnStart(_state, _rng, _events);
            EvaluateOutcome();
            _events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "Turn start")
            {
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });

            var bonusDraw = _state.PendingDrawNextTurn;
            _state.PendingDrawNextTurn = 0;

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

            DeckRules.DrawCards(_state, TeamSide.Player, _rng,
                _state.Config.CardsDrawnPerTurn + bonusDraw + backRowDraw +
                (_state.TurnNumber == 1 ? mods?.ExtraDrawOnBattleStart ?? 0 : 0), _events);
            DeckRules.DrawCards(_state, TeamSide.Enemy, _rng, _state.Config.CardsDrawnPerTurn, _events);
        }

        void BeginPlanning()
        {
            SetPhase(TurnPhase.Planning);

            var enemyTurn = EnemyTurnPlanner.PrepareEnemyTurn(_state, _rng, energyBudget: 3);
            _state.EnemyPlan.PlayQueue.Clear();
            _state.EnemyPlan.PlayQueue.AddRange(enemyTurn.Plan.PlayQueue);
            _state.EnemyPlan.EnergySpent = enemyTurn.Plan.EnergySpent;
            _state.EnemyIntents.Clear();
            _state.EnemyIntents.AddRange(enemyTurn.Intents);

            _events.Add(new BattleEvent(BattleEventKind.EnemyIntentPrepared,
                $"Enemy intends {enemyTurn.Intents.Count} card(s)"));

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
            }
            else if (!enemyAlive)
            {
                _state.Outcome = BattleOutcome.PlayerVictory;
                _events.Add(new BattleEvent(BattleEventKind.BattleEnded, "Victory")
                    { Outcome = BattleOutcome.PlayerVictory });
            }
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
            {
                PrepareCombatantDeck(cc, deckRng);

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
                    Speed = cc.Speed
                };
                var startHp = cc.StartHp ?? cc.MaxHp;
                combatant.Hp = System.Math.Max(0, System.Math.Min(startHp, cc.MaxHp));
                _state.Combatants.Add(combatant);

                var drawPile = cc.Team == TeamSide.Player ? _state.PlayerDrawPile : _state.EnemyDrawPile;
                foreach (var template in cc.DeckTemplates)
                {
                    var instance = CreateCardInstance(template);
                    drawPile.Add(instance);
                }
            }

            RelicBattleRules.RefreshAllDerivedStats(_state);
            RelicBattleRules.ApplyTeamHpBonus(_state, config.RunModifiers);

            ApplyBattleStartRelicEffects(config.RunModifiers);

            foreach (var combatant in _state.Combatants)
            {
                RelicEffectRules.ResetTurnFlags(combatant);
                if (!combatant.IsAlive)
                    CombatantDeathRules.OnCharacterDied(_state, combatant, _events);
            }

            DeckRules.ShuffleDrawPile(_state, TeamSide.Player, _rng, _events);
            DeckRules.ShuffleDrawPile(_state, TeamSide.Enemy, _rng, _events);
            EvaluateOutcome();
        }

        static void PrepareCombatantDeck(CombatantConfig cc, BattleRng deckRng)
        {
            if (!cc.UseRandomSkillPool || cc.SkillPoolCandidates.Count == 0)
                return;

            cc.DeckTemplates.Clear();
            var built = EnemyDeckBuilder.BuildRandomDeck(
                cc.SkillPoolCandidates,
                deckRng,
                cc.RandomDeckSize,
                cc.RandomSkillPickMin,
                cc.RandomSkillPickMax);
            cc.DeckTemplates.AddRange(built);
        }

        void ApplyBattleStartRelicEffects(RunModifierSnapshot mods)
        {
            if (mods == null)
                return;

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

                    DamageRules.ApplyBlock(c, mods.BattleStartFrontBlock, _events);
                }
            }
        }

        CardInstanceState CreateCardInstance(CardTemplate template)
        {
            var id = _state.NextCardInstanceId++;
            var card = new CardInstanceState
            {
                InstanceId = id,
                DefinitionId = template.DefinitionId,
                OwnerCharacterId = template.OwnerCharacterId,
                Cost = template.Cost,
                CardType = template.CardType,
                DisplayName = template.DisplayName,
                IsUsable = true
            };
            foreach (var action in template.Actions)
                card.Actions.Add(CloneAction(action));
            card.Keywords.AddRange(template.Keywords);
            _state.CardsById[id] = card;
            return card;
        }

        static EffectActionSpec CloneAction(EffectActionSpec source)
        {
            return new EffectActionSpec
            {
                Type = source.Type,
                Target = source.Target,
                Value = source.Value,
                StatusId = source.StatusId,
                Stacks = source.Stacks,
                Duration = source.Duration,
                ScaleWithAttack = source.ScaleWithAttack,
                ScaleWithDefense = source.ScaleWithDefense,
                AttackScalePercent = source.AttackScalePercent,
                DefenseScalePercent = source.DefenseScalePercent,
                Condition = source.Condition,
                Reach = source.Reach,
                SplashBehindTarget = source.SplashBehindTarget,
                SplashPowerPercent = source.SplashPowerPercent,
                BackRowPowerPercent = source.BackRowPowerPercent,
                IgnoreDefPercent = source.IgnoreDefPercent,
                BonusIfTargetHpBelowPercent = source.BonusIfTargetHpBelowPercent,
                BonusIfTargetHpBelowFlat = source.BonusIfTargetHpBelowFlat,
                BonusIfTargetHitThisTurnPercent = source.BonusIfTargetHitThisTurnPercent,
                LifestealPercent = source.LifestealPercent,
                OnKillHealAmount = source.OnKillHealAmount
            };
        }
    }
}
