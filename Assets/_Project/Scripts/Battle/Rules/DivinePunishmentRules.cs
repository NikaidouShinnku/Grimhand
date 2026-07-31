using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>古老神殿亵渎：仅本场战斗，敌人获得不衰减的可见神罚增伤状态。</summary>
    public static class DivinePunishmentRules
    {
        public static void ApplyToAllEnemies(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            var stacks = state.Config?.RunModifiers?.EnemyOutgoingDamagePercentBonus ?? 0;
            if (stacks <= 0)
                return;

            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                TryApplyToEnemy(state, enemy, stacks, events);
        }

        public static void TryApplyToEnemy(
            BattleState state,
            CombatantState enemy,
            List<BattleEvent> events)
        {
            var stacks = state?.Config?.RunModifiers?.EnemyOutgoingDamagePercentBonus ?? 0;
            TryApplyToEnemy(state, enemy, stacks, events);
        }

        public static void TryApplyToEnemy(
            BattleState state,
            CombatantState enemy,
            int stacks,
            List<BattleEvent> events)
        {
            if (enemy == null || !enemy.IsAlive || enemy.Team != TeamSide.Enemy || stacks <= 0)
                return;

            var current = StatusRules.GetStatusStacks(enemy, StatusCatalog.DivinePunishmentAtk);
            if (current == stacks)
                return;

            if (current > 0)
                StatusRules.RemoveAllStatus(enemy, StatusCatalog.DivinePunishmentAtk, events);

            StatusRules.ApplyStatusInternal(
                state,
                enemy,
                StatusCatalog.DivinePunishmentAtk,
                stacks,
                -1,
                events,
                mirrorChainWraith: false);
        }
    }
}
