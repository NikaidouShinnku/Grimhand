using Grimhand.Battle.Consumables;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Tutorial
{
    /// <summary>教程专用奖励（不影响正式掉落逻辑）。</summary>
    public static class ExpeditionTutorialRewards
    {
        public const int FirstBattleGold = 45;
        public const int EliteBattleGold = 35;
        public const int StartingGold = 25;

        public static ExpeditionRewardPickup BuildFirstBattleVictory()
        {
            var rewards = new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.BattleVictory,
                HeaderText = "教学胜利",
                Gold = FirstBattleGold
            };
            rewards.CardPacks.Add(new CardPackRewardEntry { PackId = CardPackIds.Common });
            return rewards;
        }

        public static ExpeditionRewardPickup BuildEliteVictory()
        {
            return new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.BattleVictory,
                HeaderText = "精英击破",
                Gold = EliteBattleGold
            };
        }

        public static ExpeditionRewardPickup BuildChestReward()
        {
            return new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.Chest,
                HeaderText = "教学宝箱",
                RelicId = RelicIds.SunPyramid,
                ConsumableId = ConsumableIds.LargeHealingPotion,
                ConsumableCount = 1
            };
        }
    }
}
