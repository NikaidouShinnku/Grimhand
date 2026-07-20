using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class EnergyRules
    {
        public const int DefaultMax = 8;
        public const int DefaultTurnRegen = 4;

        public static void ApplyTurnStartRegen(BattleState state)
        {
            if (state == null)
                return;

            var penalty = state.PendingPlayerEnergyRegenPenaltyNextTurn;
            state.PendingPlayerEnergyRegenPenaltyNextTurn = 0;

            if (state.IsFirstPlayerTurn)
            {
                state.EnergyCurrent = state.EnergyMax;
                state.IsFirstPlayerTurn = false;
                // 首回合满能量，惩罚顺延到下一回合再扣回复
                if (penalty > 0)
                    state.PendingPlayerEnergyRegenPenaltyNextTurn = penalty;
                return;
            }

            var baseRegen = state.Config?.TurnStartEnergyRegen ?? DefaultTurnRegen;
            var regen = System.Math.Max(0, baseRegen - penalty);
            Restore(state, regen);

            if (penalty > 0)
                ClearSoulDrainDisplay(state);

            if (state.PendingPlayerEnergyGainNextTurn > 0)
            {
                GainTemporary(state, state.PendingPlayerEnergyGainNextTurn);
                state.PendingPlayerEnergyGainNextTurn = 0;
            }
        }

        static void ClearSoulDrainDisplay(BattleState state)
        {
            if (state?.Combatants == null)
                return;

            foreach (var unit in state.Combatants)
            {
                if (unit == null || unit.Team != TeamSide.Player)
                    continue;

                for (var i = unit.Statuses.Count - 1; i >= 0; i--)
                {
                    if (unit.Statuses[i].StatusId == StatusCatalog.SoulDrain)
                        unit.Statuses.RemoveAt(i);
                }
            }
        }

        /// <summary>获得临时能量：可超过上限，不改变 EnergyMax。</summary>
        public static int GainTemporary(BattleState state, int amount)
        {
            if (state == null || amount <= 0)
                return 0;

            state.EnergyCurrent += amount;
            return amount;
        }

        /// <summary>回复能量：最多到 EnergyMax，不可超过。</summary>
        public static int Restore(BattleState state, int amount)
        {
            if (state == null || amount <= 0)
                return 0;

            var before = state.EnergyCurrent;
            state.EnergyCurrent = System.Math.Min(state.EnergyCurrent + amount, state.EnergyMax);
            return state.EnergyCurrent - before;
        }

        public static bool CanAfford(int energyCurrent, int cost) => energyCurrent >= cost;
    }
}
