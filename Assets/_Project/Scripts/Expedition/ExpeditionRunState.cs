using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public sealed class ExpeditionRunState
    {
        public ExpeditionPhase Phase { get; set; } = ExpeditionPhase.InBattle;
        public int BattlesWon { get; set; }
        public int TargetBattleCount { get; set; } = 3;
        public int Gold { get; set; }
        public int LastGoldReward { get; set; }
        public int LastXpReward { get; set; }
        public List<PartyMemberSnapshot> Party { get; } = new();
        public List<string> Relics { get; } = new();
        public List<ExpeditionRouteOption> PendingRoutes { get; } = new();
        public BattleConfig CurrentBattleConfig { get; set; }
    }
}
