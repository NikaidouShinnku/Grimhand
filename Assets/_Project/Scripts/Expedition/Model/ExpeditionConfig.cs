using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Model
{
    public sealed class ExpeditionConfig
    {
        public int RunSeed { get; set; } = 42;
        public int TargetBattleCount { get; set; } = 3;
        public int RoutesPerVictory { get; set; } = 3;

        public int GoldMinPerVictory { get; set; } = 15;

        public int GoldMaxPerVictory { get; set; } = 25;

        public List<BattleConfig> CombatEncounters { get; } = new();
    }
}
