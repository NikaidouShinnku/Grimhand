using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;

namespace Grimhand.Expedition
{
    public sealed class ExpeditionRunState
    {
        public ExpeditionPhase Phase { get; set; } = ExpeditionPhase.RouteSelect;
        public int BattlesWon { get; set; }
        public int TargetBattleCount { get; set; } = 9;
        public int Gold { get; set; }
        public int LastGoldReward { get; set; }
        public int LastXpReward { get; set; }
        public string LastEventMessage { get; set; } = "";
        public List<PartyMemberSnapshot> Party { get; } = new();
        public List<string> Relics { get; } = new();
        public HashSet<string> UsedEventIds { get; } = new();
        public HashSet<string> EventFlags { get; } = new();
        public List<string> ConsumableSlots { get; } = new();
        public List<string> RunAcquisitionLog { get; } = new();
        public string PendingConsumableOfferId { get; set; } = "";
        public ExpeditionRunModifiers Modifiers { get; } = new();
        public ExpeditionMapState Map { get; set; }
        public int MiracleLeafUsesRemaining { get; set; } = -1;
        public List<ExpeditionRouteOption> PendingRoutes { get; } = new();
        public BattleConfig CurrentBattleConfig { get; set; }
        public string CurrentBossDisplayName { get; set; } = "";
        public ExpeditionRewardPickup PendingRewardPickup { get; set; }
        public ExpeditionPendingEvent PendingEvent { get; set; }
        public Expedition.Events.ExpeditionEventInteractionState EventInteraction { get; set; }
        public string PendingEventBattleKey { get; set; } = "";
        public ExpeditionRewardPickup PendingEventBattleVictoryReward { get; set; }
        public ExpeditionRewardPickup PendingDeferredReward { get; set; }
        public ExpeditionPendingShrine PendingShrine { get; set; }
        public ExpeditionShopState Shop { get; } = new();
    }
}
