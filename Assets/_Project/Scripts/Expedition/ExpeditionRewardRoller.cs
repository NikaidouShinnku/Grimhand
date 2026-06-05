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
            BattleRng rng)
        {
            var gold = ExpeditionEconomy.RollVictoryGold(config, rng);
            gold = ApplyGoldRelicBonus(gold, run.Relics);

            var rewards = new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.BattleVictory,
                HeaderText = "战斗胜利",
                Gold = gold
            };

            if (RollPercent(rng, config.RelicDropChancePercent))
                rewards.RelicId = PickRandomRelicId(run.Relics, run, rng);

            if (RollPercent(rng, config.RelicDropChancePercent))
                rewards.ConsumableId = PickRandomConsumableId(rng);

            if (RollPercent(rng, config.CardDropChancePercent))
                TryRollCardReward(rewards, run, config, rng);

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
            gold = ApplyGoldRelicBonus(gold, run.Relics);

            var reward = new ExpeditionRewardPickup
            {
                Kind = RewardPickupKind.Chest,
                HeaderText = "宝箱",
                Gold = gold
            };

            if (RollPercent(rng, config.TreasureRelicChancePercent))
                reward.RelicId = PickRandomRelicId(run.Relics, run, rng);

            if (RollPercent(rng, config.TreasureRelicChancePercent))
                reward.ConsumableId = PickRandomConsumableId(rng);

            return reward;
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

        static int ApplyGoldRelicBonus(int baseGold, IReadOnlyList<string> relicIds)
        {
            var mods = RelicDatabase.BuildModifiers(relicIds);
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
            foreach (var consumable in ConsumableDatabase.All)
                pool.Add(consumable.Id);

            if (pool.Count == 0)
                return "";

            return pool[rng.NextIndex(pool.Count)];
        }

        static void TryRollCardReward(
            ExpeditionRewardPickup rewards,
            ExpeditionRunState run,
            ExpeditionConfig config,
            BattleRng rng)
        {
            if (run.Party == null || run.Party.Count == 0 || config.CombatEncounters.Count == 0)
                return;

            var templates = CollectRewardCardTemplates(config);
            if (templates.Count == 0)
                return;

            var member = run.Party[rng.NextIndex(run.Party.Count)];
            CardTemplate picked = null;

            var owned = new List<CardTemplate>();
            foreach (var m in run.Party)
                owned.AddRange(m.BonusCards);

            for (var attempt = 0; attempt < 12; attempt++)
            {
                var candidate = templates[rng.NextIndex(templates.Count)];
                if (IsDuplicateOwned(candidate, owned))
                    continue;

                picked = candidate;
                break;
            }

            picked ??= templates[rng.NextIndex(templates.Count)];
            rewards.CardDefinitionId = picked.DefinitionId;
            rewards.CardOwnerCharacterId = string.IsNullOrEmpty(picked.OwnerCharacterId)
                ? member.CharacterDefinitionId
                : picked.OwnerCharacterId;
            rewards.CardDisplayName = picked.DisplayName;
        }

        static List<CardTemplate> CollectRewardCardTemplates(ExpeditionConfig config)
        {
            var result = new List<CardTemplate>();
            var seen = new HashSet<string>();

            foreach (var encounter in config.CombatEncounters)
            {
                foreach (var cc in encounter.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    foreach (var template in cc.DeckTemplates)
                    {
                        if (string.IsNullOrEmpty(template.DefinitionId))
                            continue;

                        if (seen.Add(template.DefinitionId))
                            result.Add(template);
                    }
                }
            }

            return result;
        }

        static bool IsDuplicateOwned(CardTemplate candidate, IReadOnlyList<CardTemplate> owned)
        {
            foreach (var card in owned)
            {
                if (card.DefinitionId == candidate.DefinitionId)
                    return true;
            }

            return false;
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
