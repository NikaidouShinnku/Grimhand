namespace Grimhand.Battle.Rules
{
    public static class EnergyRules
    {
        public const int DefaultMax = 8;
        public const int DefaultTurnRegen = 4;

        public static void ApplyTurnStartRegen(Model.BattleState state)
        {
            if (state.IsFirstPlayerTurn)
            {
                state.EnergyCurrent = state.EnergyMax;
                state.IsFirstPlayerTurn = false;
                return;
            }

            var regen = state.Config.TurnStartEnergyRegen;
            if (state.PendingPlayerEnergyRegenPenaltyNextTurn > 0)
            {
                regen = System.Math.Max(0, regen - state.PendingPlayerEnergyRegenPenaltyNextTurn);
                state.PendingPlayerEnergyRegenPenaltyNextTurn = 0;
            }

            Restore(state, regen);

            if (state.PendingPlayerEnergyGainNextTurn > 0)
            {
                GainTemporary(state, state.PendingPlayerEnergyGainNextTurn);
                state.PendingPlayerEnergyGainNextTurn = 0;
            }
        }

        /// <summary>获得临时能量：可超过上限，不改变 EnergyMax。</summary>
        public static int GainTemporary(Model.BattleState state, int amount)
        {
            if (state == null || amount <= 0)
                return 0;

            state.EnergyCurrent += amount;
            return amount;
        }

        /// <summary>回复能量：最多到 EnergyMax，不可超过。</summary>
        public static int Restore(Model.BattleState state, int amount)
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
