using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Reactions
{
    public static class ReactionRules
    {
        public static bool MeetsCondition(BattleState state, ReactionConditionType condition, string actorId)
        {
            if (condition == ReactionConditionType.None)
                return true;

            return condition switch
            {
                ReactionConditionType.LastActionAttackOnSelf =>
                    state != null
                    && state.LastAction.ActionKind == ActionKind.Attack
                    && state.LastAction.TargetId == actorId,
                _ => false
            };
        }

        public static bool MeetsRespondCondition(
            BattleState state,
            ReactionConditionType condition,
            string respondOwnerId,
            ResolutionStep enemyStep)
        {
            if (condition == ReactionConditionType.None)
                return true;

            return condition switch
            {
                ReactionConditionType.LastActionAttackOnSelf =>
                    RespondTriggerMatcher.WouldEnemyStepAttackCombatant(state, enemyStep, respondOwnerId),
                ReactionConditionType.LastActionDefenseOnTarget =>
                    RespondTriggerMatcher.EnemyStepIsDefense(state, enemyStep),
                ReactionConditionType.LastActionStatusOnTarget =>
                    RespondTriggerMatcher.EnemyStepIsStatus(state, enemyStep),
                _ => false
            };
        }
    }
}
