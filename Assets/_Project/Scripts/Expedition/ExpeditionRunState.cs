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
        public bool LastBattleWasElite { get; set; }
        public bool LastBattleWasBoss { get; set; }
        public int LastBattleFloor { get; set; } = 1;
        public ExpeditionPendingEventAftermath PendingEventAftermath { get; set; }
        /// <summary>事件确认结算时使用的预掷结果（0–99）。</summary>
        public int? EventResolutionFixedRoll100 { get; set; }
        public string LastEventMessage { get; set; } = "";
        public List<PartyMemberSnapshot> Party { get; } = new();
        public List<string> Relics { get; } = new();
        public Dictionary<string, int> RelicGrowthTiers { get; } = new();
        public HashSet<string> UsedEventIds { get; } = new();
        public HashSet<string> EventFlags { get; } = new();
        public List<string> ConsumableSlots { get; } = new();
        public List<string> RunAcquisitionLog { get; } = new();
        public string PendingConsumableOfferId { get; set; } = "";
        public ExpeditionPendingCardOffer PendingCardOffer { get; set; }
        public ExpeditionCardAltarState CardAltar { get; set; }
        /// <summary>开局时军营收藏的卡牌 ID（memberId → 10 张）；祭坛只读此快照，避免战后 party 快照丢字段。</summary>
        public Dictionary<string, List<string>> RunStartCampDecks { get; } = new();
        /// <summary>本局各角色已从收藏取走的槽位（memberId → indices）。</summary>
        public Dictionary<string, HashSet<int>> ExtractedCampCollectionIndices { get; } = new();
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
        public int PendingEventBattleBonusXp { get; set; }
        public ExpeditionRewardPickup PendingEventBattleVictoryReward { get; set; }
        public ExpeditionRewardPickup PendingDeferredReward { get; set; }
        public ExpeditionPendingShrine PendingShrine { get; set; }
        public ExpeditionShopState Shop { get; } = new();
        public ExpeditionTalentRunState TalentRun { get; } = new();
        /// <summary>远征级额外牌池（诅咒等），不计入角色 10 张上限，不可放弃。</summary>
        public List<CardTemplate> RunWideBonusCards { get; } = new();
        public string PendingTravelerGiftRelicId { get; set; } = "";
        public string PendingTravelerGiftCurseOwnerId { get; set; } = "";
    }
}
