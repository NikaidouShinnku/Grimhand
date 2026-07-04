using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class ExpeditionRewardRoller
    {
        public const int CavePathVariantCount = 5;

        public static ExpeditionRewardPickup RollVictoryRewards(
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng,
            int floor,
            bool isElite,
            bool isBoss)
        {
            var profile = CombatRewardRules.GetProfile(floor, isElite, isBoss);
            var gold = CombatRewardRules.RollGold(rng, floor, isElite, isBoss);
            gold = ApplyGoldRelicBonus(gold, run.Relics, run.RelicGrowthTiers);

            var rewards = new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.BattleVictory,
                HeaderText = "战斗胜利",
                Gold = gold
            };

            if (RollPercent(rng, profile.RelicChancePercent))
                rewards.RelicId = PickRandomRelicId(run.Relics, run, rng);

            if (RollPercent(rng, profile.ConsumableChancePercent))
                rewards.ConsumableId = PickRandomConsumableId(rng);

            RollCardPacks(rewards, profile, rng);

            return rewards;
        }

        public static ExpeditionRewardPickup RollChestReward(
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            var min = config.TreasureGoldMin > 0 ? config.TreasureGoldMin : 20;
            var max = config.TreasureGoldMax >= min ? config.TreasureGoldMax : min;
            var gold = min == max ? min : rng.NextInt(min, max + 1);
            gold = ApplyGoldRelicBonus(gold, run.Relics, run.RelicGrowthTiers);

            var reward = new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.Chest,
                HeaderText = "宝箱",
                Gold = gold
            };

            reward.CardPacks.Add(new CardPackRewardEntry { PackId = CardPackIds.Common });

            var relicChance = config.TreasureRelicChancePercent > 0
                ? config.TreasureRelicChancePercent
                : 15;
            if (RollPercent(rng, relicChance))
                TryAssignRelicReward(reward, run, rng);

            var consumableChance = config.TreasureConsumableChancePercent > 0
                ? config.TreasureConsumableChancePercent
                : 33;
            if (RollPercent(rng, consumableChance))
                reward.ConsumableId = PickRandomConsumableId(rng);

            return reward;
        }

        static void RollCardPacks(ExpeditionRewardPickup rewards, CombatRewardProfile profile, BattleRng rng)
        {
            if (RollPercent(rng, profile.CommonPackChancePercent))
                rewards.CardPacks.Add(new CardPackRewardEntry { PackId = CardPackIds.Common });

            if (RollPercent(rng, profile.AdvancedPackChancePercent))
                rewards.CardPacks.Add(new CardPackRewardEntry { PackId = CardPackIds.Advanced });

            if (RollPercent(rng, profile.MasterPackChancePercent))
                rewards.CardPacks.Add(new CardPackRewardEntry { PackId = CardPackIds.Master });
        }

        static void TryAssignRelicReward(ExpeditionRewardPickup reward, ExpeditionRunState run, BattleRng rng)
        {
            var relicId = PickRandomRelicId(run.Relics, run, rng);
            if (!string.IsNullOrEmpty(relicId))
                reward.RelicId = relicId;
        }

        public static ExpeditionNodeType RollRouteNodeType(ExpeditionConfig config, BattleRng rng)
        {
            var combat = config.CombatRouteWeight > 0 ? config.CombatRouteWeight : 55;
            var treasure = config.TreasureRouteWeight > 0 ? config.TreasureRouteWeight : 45;
            var total = combat + treasure;
            if (total <= 0)
                return ExpeditionNodeType.Combat;

            return rng.NextIndex(total) < combat
                ? ExpeditionNodeType.Combat
                : ExpeditionNodeType.Treasure;
        }

        public static int RollPathSpriteIndex(BattleRng rng) =>
            rng.NextIndex(CavePathVariantCount);

        static bool RollPercent(BattleRng rng, int percent)
        {
            if (percent <= 0)
                return false;

            if (percent >= 100)
                return true;

            return rng.NextIndex(100) < percent;
        }

        static int ApplyGoldRelicBonus(
            int baseGold,
            IReadOnlyList<string> relicIds,
            IReadOnlyDictionary<string, int> growthTiers = null)
        {
            var mods = RelicDatabase.BuildModifiers(relicIds, growthTiers);
            if (mods.GoldBonusPercent <= 0f)
                return baseGold;

            return (int)System.Math.Round(baseGold * (1f + mods.GoldBonusPercent / 100f));
        }

        static string PickRandomRelicId(IReadOnlyList<string> owned, ExpeditionRunState run, BattleRng rng)
        {
            var pool = new List<string>();
            foreach (var relic in RelicDatabase.All)
            {
                if (owned != null && OwnsRelic(owned, relic.Id))
                    continue;

                if (!RelicDatabase.CanAppearInRewardPool(relic, run?.Party))
                    continue;

                pool.Add(relic.Id);
            }

            if (pool.Count == 0)
                return "";

            return pool[rng.NextIndex(pool.Count)];
        }

        static string PickRandomConsumableId(BattleRng rng)
        {
            var pool = new List<string>();
            ConsumableDatabase.CollectRewardPoolIds(pool);

            if (pool.Count == 0)
                return "";

            return pool[rng.NextIndex(pool.Count)];
        }

        static bool OwnsRelic(IReadOnlyList<string> owned, string relicId)
        {
            if (owned == null || string.IsNullOrEmpty(relicId))
                return false;

            for (var i = 0; i < owned.Count; i++)
            {
                if (owned[i] == relicId)
                    return true;
            }

            return false;
        }
    }
}
