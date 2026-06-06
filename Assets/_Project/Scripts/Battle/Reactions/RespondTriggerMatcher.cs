using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Reactions
{
    public static class RespondTriggerMatcher
    {
        public static bool EnemyStepHasAttack(BattleState state, ResolutionStep enemyStep)
        {
            var card = state.GetCard(enemyStep.CardInstanceId);
            if (card == null)
                return false;

            foreach (var action in card.Actions)
            {
                if (action.Type == EffectActionType.DealDamage)
                    return true;
            }

            return false;
        }

        public static bool WouldEnemyStepAttackCombatant(
            BattleState state,
            ResolutionStep enemyStep,
            string defenderId)
        {
            if (!EnemyStepHasAttack(state, enemyStep))
                return false;

            var attacker = state.GetCombatant(enemyStep.CombatantId);
            var card = state.GetCard(enemyStep.CardInstanceId);
            var defender = state.GetCombatant(defenderId);
            if (attacker == null || card == null || defender == null || !defender.IsAlive)
                return false;

            foreach (var action in card.Actions)
            {
                if (action.Type != EffectActionType.DealDamage)
                    continue;

                if (action.Target == EffectTarget.AllEnemies)
                {
                    if (defender.Team == TeamSide.Player)
                        return true;
                    continue;
                }

                var target = TargetRules.ResolveTarget(state, attacker, action.Target, card.InstanceId);
                if (target != null && target.Id == defenderId)
                    return true;
            }

            return false;
        }

        public static bool RespondCardMatchesEnemyStep(
            BattleState state,
            CombatantState respondOwner,
            CardInstanceState respondCard,
            ResolutionStep enemyStep)
        {
            if (respondOwner == null || respondCard == null)
                return false;

            if (!WouldEnemyStepAttackCombatant(state, enemyStep, respondOwner.Id))
                return false;

            foreach (var action in respondCard.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                if (ReactionRules.MeetsRespondCondition(state, action.Condition, respondOwner.Id, enemyStep))
                    return true;
            }

            return RespondRules.IsRespondCard(respondCard)
                   && ReactionRules.MeetsRespondCondition(
                       state,
                       ReactionConditionType.LastActionAttackOnSelf,
                       respondOwner.Id,
                       enemyStep);
        }

        public static bool AnyMatchingEnemyStep(
            BattleState state,
            CombatantState respondOwner,
            CardInstanceState respondCard,
            IReadOnlyList<ResolutionStep> baseline)
        {
            foreach (var step in baseline)
            {
                var actor = state.GetCombatant(step.CombatantId);
                if (actor == null || actor.Team != TeamSide.Enemy)
                    continue;

                if (RespondCardMatchesEnemyStep(state, respondOwner, respondCard, step))
                    return true;
            }

            return false;
        }

        public static int EstimateIncomingPower(
            BattleState state,
            CombatantState attacker,
            CardInstanceState card,
            CombatantState defender)
        {
            if (attacker == null || card == null || defender == null)
                return 0;

            var max = 0;
            foreach (var action in card.Actions)
            {
                if (action.Type != EffectActionType.DealDamage)
                    continue;

                var value = CardPowerRules.ComputeActionValue(action, attacker);
                if (action.Target == EffectTarget.AllEnemies)
                {
                    var adjusted = TargetReachRules.AdjustPowerForTarget(state, action, defender, value);
                    max = System.Math.Max(max, adjusted);
                    continue;
                }

                var target = TargetRules.ResolveTarget(state, attacker, action.Target, card.InstanceId);
                if (target != null && target.Id == defender.Id)
                {
                    var adjusted = TargetReachRules.AdjustPowerForTarget(state, action, defender, value);
                    max = System.Math.Max(max, adjusted);
                }
            }

            return max;
        }
    }
}
