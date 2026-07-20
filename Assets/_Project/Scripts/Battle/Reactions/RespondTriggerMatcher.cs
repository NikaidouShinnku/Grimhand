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
                if (ActionDealsDamage(action.Type))
                    return true;
            }

            return false;
        }

        static bool ActionDealsDamage(EffectActionType type) =>
            type is EffectActionType.DealDamage
                or EffectActionType.ConsumeBlockDealDamage
                or EffectActionType.DealDamageScaledByActorHpLoss
                or EffectActionType.DealDamageAlternateIfHealedThisTurn
                or EffectActionType.DealDamageBonusPerTargetDebuffStack
                or EffectActionType.DamagePerRespondCount
                or EffectActionType.EtherealCountBonusDamage;

        public static bool EnemyStepIsStatus(BattleState state, ResolutionStep enemyStep)
        {
            var card = state.GetCard(enemyStep.CardInstanceId);
            return card != null && card.CardType == CardType.Status;
        }

        public static bool EnemyStepIsDefense(BattleState state, ResolutionStep enemyStep)
        {
            var card = state.GetCard(enemyStep.CardInstanceId);
            return card != null && card.CardType == CardType.Defense;
        }

        public static bool EnemyStepTriggersPlayerRespond(BattleState state, ResolutionStep enemyStep)
        {
            return EnemyStepHasAttack(state, enemyStep)
                   || EnemyStepIsStatus(state, enemyStep)
                   || EnemyStepIsDefense(state, enemyStep);
        }

        public static bool IsMonitoredEnemyStep(
            BattleState state,
            CardInstanceState respondCard,
            ResolutionStep enemyStep)
        {
            if (state == null || respondCard == null)
                return false;

            if (!state.ResolutionTargets.TryGetValue(respondCard.InstanceId, out var targetId)
                || string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            return targetId == enemyStep.CombatantId;
        }

        public static bool PlayerStepHasAttack(BattleState state, ResolutionStep playerStep) =>
            EnemyStepHasAttack(state, playerStep);

        public static bool PlayerStepTriggersEnemyRespond(BattleState state, ResolutionStep playerStep) =>
            PlayerStepHasAttack(state, playerStep);

        public static bool WouldEnemyStepAttackCombatant(
            BattleState state,
            ResolutionStep enemyStep,
            string defenderId) =>
            WouldStepAttackCombatant(state, enemyStep, defenderId);

        public static bool WouldPlayerStepAttackCombatant(
            BattleState state,
            ResolutionStep playerStep,
            string defenderId) =>
            WouldStepAttackCombatant(state, playerStep, defenderId);

        public static bool WouldStepAttackCombatant(
            BattleState state,
            ResolutionStep attackStep,
            string defenderId)
        {
            if (!EnemyStepHasAttack(state, attackStep))
                return false;

            var attacker = state.GetCombatant(attackStep.CombatantId);
            var card = state.GetCard(attackStep.CardInstanceId);
            var defender = state.GetCombatant(defenderId);
            if (attacker == null || card == null || defender == null || !defender.IsAlive)
                return false;

            foreach (var action in card.Actions)
            {
                if (!ActionDealsDamage(action.Type))
                    continue;

                if (action.Target == EffectTarget.AllEnemies)
                {
                    if (defender.Team != attacker.Team)
                        return true;
                    continue;
                }

                var target = TargetRules.ResolveTarget(state, attacker, action.Target, card.InstanceId, null, action);
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

            if (respondCard.Keywords.Contains("respond_status") && EnemyStepIsStatus(state, enemyStep))
            {
                return IsMonitoredEnemyStep(state, respondCard, enemyStep)
                       && respondCard.Actions.Count > 0;
            }

            if (respondCard.Keywords.Contains("respond_defense") && EnemyStepIsDefense(state, enemyStep))
            {
                return IsMonitoredEnemyStep(state, respondCard, enemyStep)
                       && respondCard.Actions.Count > 0;
            }

            return RespondCardMatchesAttackStep(state, respondOwner, respondCard, enemyStep);
        }

        public static bool RespondCardMatchesPlayerStep(
            BattleState state,
            CombatantState respondOwner,
            CardInstanceState respondCard,
            ResolutionStep playerStep) =>
            RespondCardMatchesAttackStep(state, respondOwner, respondCard, playerStep);

        static bool RespondCardMatchesAttackStep(
            BattleState state,
            CombatantState respondOwner,
            CardInstanceState respondCard,
            ResolutionStep attackStep)
        {
            if (!WouldStepAttackCombatant(state, attackStep, respondOwner.Id))
                return false;

            foreach (var action in respondCard.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                if (ReactionRules.MeetsRespondCondition(state, action.Condition, respondOwner.Id, attackStep))
                    return true;
            }

            return RespondRules.IsRespondCard(respondCard)
                   && (respondCard.Keywords.Contains("parry")
                       || HasConditionalAction(respondCard))
                   && ReactionRules.MeetsRespondCondition(
                       state,
                       ReactionConditionType.LastActionAttackOnSelf,
                       respondOwner.Id,
                       attackStep);
        }

        static bool HasConditionalAction(CardInstanceState card)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    return true;
            }

            return false;
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

        public static bool AnyMatchingPlayerStep(
            BattleState state,
            CombatantState respondOwner,
            CardInstanceState respondCard,
            IReadOnlyList<ResolutionStep> baseline)
        {
            foreach (var step in baseline)
            {
                var actor = state.GetCombatant(step.CombatantId);
                if (actor == null || actor.Team != TeamSide.Player)
                    continue;

                if (RespondCardMatchesPlayerStep(state, respondOwner, respondCard, step))
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
                if (!ActionDealsDamage(action.Type))
                    continue;

                var value = CardPowerRules.ComputeActionValue(action, attacker);
                if (action.Target == EffectTarget.AllEnemies)
                {
                    var adjusted = TargetReachRules.AdjustPowerForTarget(state, action, defender, value);
                    max = System.Math.Max(max, adjusted);
                    continue;
                }

                var target = TargetRules.ResolveTarget(state, attacker, action.Target, card.InstanceId, null, action);
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
