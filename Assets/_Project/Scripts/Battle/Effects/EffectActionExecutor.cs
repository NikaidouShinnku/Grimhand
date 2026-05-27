using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Effects
{
    public static class EffectActionExecutor
    {
        public static void ExecuteAll(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
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
                ExecuteOne(state, actor, card, action, events);
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

                ExecuteOne(state, actor, card, action, events);
            }
        }

        static void ExecuteOne(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events)
        {
            var target = TargetRules.ResolveTarget(state, actor, action.Target, card.InstanceId);
            var value = ComputeValue(action, actor);

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    if (target != null)
                        DamageRules.ApplyDamage(state, actor, target, value, card.CardType, events);
                    break;
                case EffectActionType.GainBlock:
                    DamageRules.ApplyBlock(actor, value, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    break;
                case EffectActionType.Heal:
                    DamageRules.ApplyHeal(actor, value, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
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

        static int ComputeValue(EffectActionSpec action, CombatantState actor)
        {
            var value = action.Value;
            if (action.ScaleWithAttack)
                value += actor.Attack;
            if (action.ScaleWithDefense)
                value += actor.Defense;
            return value;
        }
    }
}
