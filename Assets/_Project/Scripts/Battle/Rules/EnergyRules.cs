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
            state.EnergyCurrent = System.Math.Min(state.EnergyCurrent + regen, state.EnergyMax);
        }

        public static bool CanAfford(int energyCurrent, int cost) => energyCurrent >= cost;
    }
}
