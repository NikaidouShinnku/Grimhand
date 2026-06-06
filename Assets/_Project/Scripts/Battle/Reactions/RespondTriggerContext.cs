using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Reactions
{
    public readonly struct RespondTriggerContext
    {
        public RespondTriggerContext(string enemyCombatantId, int enemyCardInstanceId)
        {
            EnemyCombatantId = enemyCombatantId;
            EnemyCardInstanceId = enemyCardInstanceId;
        }

        public string EnemyCombatantId { get; }
        public int EnemyCardInstanceId { get; }

        public static RespondTriggerContext FromStep(BattleState state, ResolutionStep enemyStep) =>
            new(enemyStep.CombatantId, enemyStep.CardInstanceId);
    }
}
