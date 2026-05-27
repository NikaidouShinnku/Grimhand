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

            EffectActionExecutor.ExecuteAll(_state, actor, card, _events);
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

            _state.TurnNumber++;
            SetPhase(TurnPhase.Draw);
            ProcessDrawPhase();
            BeginPlanning();
        }

        void ProcessDrawPhase()
        {
            EnergyRules.ApplyTurnStartRegen(_state);
            StatusRules.ProcessTurnStartStatuses(_state, _events);
            EvaluateOutcome();
            _events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "Turn start")
            {
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });

            var bonusDraw = _state.PendingDrawNextTurn;
            _state.PendingDrawNextTurn = 0;

            DeckRules.DrawCards(_state, TeamSide.Player, _rng, _state.Config.CardsDrawnPerTurn + bonusDraw, _events);
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
            _state.EnergyMax = config.EnergyCap;
            _state.TurnNumber = 1;
            _state.IsFirstPlayerTurn = true;
            _state.Outcome = BattleOutcome.Ongoing;

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
                    MaxHp = cc.MaxHp,
                    BaseAttack = cc.BaseAttack,
                    BaseDefense = cc.BaseDefense,
                    Speed = cc.Speed
                };
                CombatantRules.RefreshDerivedStats(combatant);
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

            foreach (var combatant in _state.Combatants)
            {
                if (!combatant.IsAlive)
                    CombatantDeathRules.OnCharacterDied(_state, combatant, _events);
            }

            DeckRules.ShuffleDrawPile(_state, TeamSide.Player, _rng, _events);
            DeckRules.ShuffleDrawPile(_state, TeamSide.Enemy, _rng, _events);
            EvaluateOutcome();
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
                Condition = source.Condition,
                Reach = source.Reach,
                SplashBehindTarget = source.SplashBehindTarget,
                SplashPowerPercent = source.SplashPowerPercent,
                BackRowPowerPercent = source.BackRowPowerPercent
            };
        }
    }
}
