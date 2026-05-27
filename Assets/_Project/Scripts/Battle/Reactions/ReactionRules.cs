using Grimhand.Battle.Model;

namespace Grimhand.Battle.Reactions
{
    public static class ReactionRules
    {
        public static bool MeetsCondition(BattleState state, ReactionConditionType condition, string actorId)
        {
            if (condition == ReactionConditionType.None)
                return true;

            var last = state.LastAction;
            switch (condition)
            {
                case ReactionConditionType.LastActionAttackOnSelf:
                    return last.ActionKind == ActionKind.Attack
                           && last.TargetId == actorId
                           && !string.IsNullOrEmpty(last.ActorId);
                default:
                    return true;
            }
        }
    }
}
