using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>策划表遗物定义（v2）。</summary>
    public static class RelicDatabase
    {
        static readonly Dictionary<string, RelicDefinition> ById = Build();

        public static IReadOnlyCollection<RelicDefinition> All => ById.Values;

        public static bool TryGet(string relicId, out RelicDefinition definition) =>
            ById.TryGetValue(relicId, out definition);

        public static RunModifierSnapshot BuildModifiers(IReadOnlyList<string> relicIds)
        {
            var mods = new RunModifierSnapshot();
            if (relicIds == null || relicIds.Count == 0)
                return mods;

            foreach (var id in relicIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                switch (id)
                {
                    case RelicIds.CourageBadge:
                        mods.TeamAttackBonus += 2;
                        break;
                    case RelicIds.IronWallHeart:
                        mods.FrontDefenseBonus += 3;
                        break;
                    case RelicIds.SwiftFeather:
                        mods.BackRowExtraDrawPerTurn += 1;
                        break;
                    case RelicIds.LifeSpring:
                        mods.BattleStartTeamHeal += 8;
                        break;
                    case RelicIds.GreedyHand:
                        mods.GoldBonusPercent += 20f;
                        break;
                    case RelicIds.BloodGem:
                        mods.SacrificeDamageBonusPercent += 15f;
                        break;
                    case RelicIds.SunSigil:
                        mods.HealBonusPercent += 30f;
                        mods.HealGrantsBlock += 3;
                        break;
                    case RelicIds.ImmovableKing:
                        mods.WarriorBlockChanceOnHit = 0.25f;
                        mods.WarriorBlockAmountOnHit = 10;
                        break;
                    case RelicIds.AbyssEye:
                        mods.ScryDrawPileCount += 2;
                        break;
                    case RelicIds.EchoStone:
                        mods.FirstAttackDamageBonusPercent += 20f;
                        break;
                    case RelicIds.SoulChain:
                        mods.DeathCardsSkipPolluteTurns = true;
                        mods.DeathCardsSkipPolluteDuration = 3;
                        break;
                    case RelicIds.ChaosHeart:
                        mods.ExtraEnergyCap += 2;
                        mods.RandomDiscardEachTurn = true;
                        break;
                }
            }

            return mods;
        }

        static Dictionary<string, RelicDefinition> Build()
        {
            var list = new[]
            {
                Def(RelicIds.CourageBadge, "勇气徽章", RelicRarity.Common, "全队 ATK +2"),
                Def(RelicIds.IronWallHeart, "铁壁之心", RelicRarity.Common, "前排 DEF +3"),
                Def(RelicIds.SwiftFeather, "疾风之羽", RelicRarity.Common, "后排角色每回合额外抽 1 张牌"),
                Def(RelicIds.LifeSpring, "生命之泉", RelicRarity.Common, "每场战斗开始回复全队 8 HP"),
                Def(RelicIds.GreedyHand, "贪婪之手", RelicRarity.Common, "战斗结束金币 +20%"),
                Def(RelicIds.BloodGem, "鲜血宝石", RelicRarity.Rare, "献祭类卡牌伤害 +15%"),
                Def(RelicIds.SunSigil, "太阳圣印", RelicRarity.Rare, "治疗 +30%，被治疗者获得 3 护甲"),
                Def(RelicIds.ImmovableKing, "不动明王", RelicRarity.Rare, "战士受击 25% 概率格挡 10 点伤害"),
                Def(RelicIds.AbyssEye, "深渊之眼", RelicRarity.Rare, "每回合可预见抽牌堆顶 2 张"),
                Def(RelicIds.EchoStone, "回响之石", RelicRarity.Rare, "每场首张攻击牌伤害 +20%"),
                Def(RelicIds.SoulChain, "灵魂锁链", RelicRarity.Epic, "角色死亡后其牌 3 回合内不污染"),
                Def(RelicIds.ChaosHeart, "混沌之心", RelicRarity.Epic, "能量上限 +2，但每回合随机弃 1 张手牌")
            };

            var map = new Dictionary<string, RelicDefinition>();
            foreach (var relic in list)
                map[relic.Id] = relic;
            return map;
        }

        static RelicDefinition Def(string id, string name, RelicRarity rarity, string desc) =>
            new()
            {
                Id = id,
                DisplayName = name,
                Rarity = rarity,
                Description = desc
            };
    }
}
