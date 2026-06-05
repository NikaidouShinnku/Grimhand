using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Battle.Effects
{
    public static class EffectActionExecutor
    {
        public static void ExecuteAll(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (ParryRules.TryReadParryConfig(card, out var reductionPercent, out var reflectPercent))
            {
                ParryRules.Arm(actor, reductionPercent, reflectPercent, events);
                return;
            }

            var triggeredReaction = false;
            foreach (var action in card.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                if (!ReactionRules.MeetsCondition(state, action.Condition, actor.Id))
                    continue;

                triggeredReaction = true;
                ExecuteOne(state, actor, card, action, events, rng);
            }

            if (triggeredReaction)
            {
                events.Add(new BattleEvent(BattleEventKind.ReactionTriggered, card.DisplayName)
                {
                    CombatantId = actor.Id
                });
            }

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                ExecuteOne(state, actor, card, action, events, rng);
            }
        }

        static void ExecuteOne(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var target = TargetRules.ResolveTarget(state, actor, action.Target, card.InstanceId);
            var value = CardPowerRules.ComputeActionValue(action, actor);
            var beneficiary = target ?? actor;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    if (target != null)
                    {
                        var primaryPower = TargetReachRules.AdjustPowerForTarget(action, target, value);
                        DamageRules.ApplyDamage(state, actor, target, primaryPower, card.CardType, events);

                        if (action.SplashBehindTarget)
                        {
                            var behind = PositionRules.GetCombatantBehind(state, target);
                            if (behind != null && behind.IsAlive)
                            {
                                var splashPower = System.Math.Max(1,
                                    (int)System.Math.Round(primaryPower * action.SplashPowerPercent / 100f));
                                DamageRules.ApplyDamage(state, actor, behind, splashPower, card.CardType, events);
                            }
                        }
                    }
                    break;
                case EffectActionType.GainBlock:
                    DamageRules.ApplyBlock(beneficiary, value, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, beneficiary.Id, false, 0);
                    break;
                case EffectActionType.Heal:
                    DamageRules.ApplyHeal(beneficiary, value, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, beneficiary.Id, false, 0);
                    break;
                case EffectActionType.ApplyStatus:
                    if (target != null)
                        StatusRules.ApplyStatus(state, target, action.StatusId, action.Stacks, action.Duration, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status,
                        target != null ? target.Id : actor.Id, false, 0);
                    break;
                case EffectActionType.RemoveStatus:
                    if (target != null)
                        StatusRules.RemoveStatus(target, action.StatusId, action.Stacks, events);
                    break;
                case EffectActionType.SwapPositionWithFrontAlly:
                    PositionRules.SwapWithAdjacentAlly(state, actor, -1, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.DrawCardsNextTurn:
                    state.PendingDrawNextTurn += value;
                    events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"下回合额外抽 {value} 张")
                    {
                        CombatantId = actor.Id
                    });
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.DrawCards:
                    state.PendingDrawNextTurn += value;
                    events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"下回合额外抽 {value} 张")
                    {
                        CombatantId = actor.Id,
                        CardInstanceId = card.InstanceId
                    });
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ReflectLastDamageToAttacker:
                    var attacker = TargetRules.ResolveTarget(state, actor, EffectTarget.LastActionActor, card.InstanceId);
                    if (attacker != null)
                    {
                        var reflected = state.LastAction.DamageAmount * action.Value / 100;
                        if (reflected > 0)
                            DamageRules.ApplyDamage(state, actor, attacker, reflected, card.CardType, events);
                    }
                    break;
                case EffectActionType.GainBlockFromLastDamagePercent:
                    var block = state.LastAction.DamageAmount * action.Value / 100;
                    if (block > 0)
                    {
                        DamageRules.ApplyBlock(actor, block, events);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    }
                    break;
            }
        }
    }
}
