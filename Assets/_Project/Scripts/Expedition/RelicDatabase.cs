using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>遗物图鉴对照表（Grimhand_遗物图鉴对照表.xlsx）。</summary>
    public static class RelicDatabase
    {
        static readonly Dictionary<string, RelicDefinition> ById = Build();

        public static IReadOnlyCollection<RelicDefinition> All => ById.Values;

        public static bool TryGet(string relicId, out RelicDefinition definition) =>
            ById.TryGetValue(relicId, out definition);

        public static RunModifierSnapshot BuildModifiers(
            IReadOnlyList<string> relicIds,
            IReadOnlyDictionary<string, int> growthTiers = null)
        {
            var mods = new RunModifierSnapshot();
            if (relicIds == null || relicIds.Count == 0)
                return mods;

            foreach (var id in relicIds)
            {
                if (string.IsNullOrEmpty(id) || !TryGet(id, out var relic))
                    continue;

                mods.TeamAttackBonus += relic.AtkBonus;
                mods.TeamDefenseBonus += relic.DefBonus;
                mods.TeamHpBonus += relic.HpBonus;
                mods.TeamAttackBonusPercent += relic.AtkPercentBonus;
                mods.TeamBlockGainBonusPercent += relic.BlockGainPercentBonus;
                ApplySpecialFlag(mods, relic);
                RelicGrowthRules.ApplyGrowthBonuses(id, RelicGrowthRules.GetGrowthTiers(growthTiers, id), mods);
            }

            return mods;
        }

        public static bool CanAppearInRewardPool(RelicDefinition relic, IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (relic == null || relic.EvolutionOnly)
                return false;

            if (string.IsNullOrEmpty(relic.RequiredCharacterId))
                return true;

            if (party == null)
                return false;

            foreach (var member in party)
            {
                if (member?.CharacterDefinitionId == relic.RequiredCharacterId)
                    return true;
            }

            return false;
        }

        static void ApplySpecialFlag(RunModifierSnapshot mods, RelicDefinition relic)
        {
            switch (relic.SpecialFlag)
            {
                case "pharaoh_only":
                    mods.PharaohBlockGivenBonusPercent += 33f;
                    mods.StatusCardTeamBlock += 3;
                    break;
                case "warrior_only":
                    mods.WarriorFirstHitBlockAmount += 12;
                    mods.WarriorBlockDamageReductionPercent += 20f;
                    break;
                case "demon_only":
                    mods.SacrificeHpCostReductionPercent += 15f;
                    mods.SacrificeStackAttackBonus += 5;
                    break;
                case "front_armor_15":
                    mods.BattleStartFrontBlock += 15;
                    break;
                case "extra_draw_1":
                    mods.ExtraDrawOnBattleStart += 1;
                    mods.SkipPollutedCardsOnDraw = true;
                    break;
                case "burn_proc_20pct":
                    mods.AttackBurnProcChance = 0.20f;
                    mods.AttackBurnStacks = 5;
                    mods.AttackBurnDurationTurns = 5;
                    break;
                case "cost3_plus_15pct":
                    mods.HighCostCardDamageBonusPercent += 15f;
                    break;
                case "first_hit_minus_30pct":
                    mods.FirstHitDamageReductionPercent += 30f;
                    break;
                case "heal_2_per_turn":
                    mods.EndTurnTeamHeal += 2;
                    mods.StatusDurationBonusTurns += 1;
                    break;
                case "first_atk_5_first_def_5_both_heal_5":
                    mods.FirstAttackFlatBonus += 5;
                    mods.FirstDefenseFlatBonus += 5;
                    mods.AttackAndDefenseSameTurnHeal += 5;
                    break;
                case "evolvable":
                    if (relic.Id == RelicIds.JadeStone)
                        mods.TurnStartRandomAllyBlock += 2;
                    else if (relic.Id == RelicIds.BurningBoots)
                    {
                        mods.BattleStartSpeedBonusTurns = System.Math.Max(mods.BattleStartSpeedBonusTurns, 1);
                        mods.BattleStartSpeedBonus += 2;
                    }

                    break;
                case "evolved_from_jade_stone":
                    if (relic.Id == RelicIds.JadeRing)
                    {
                        mods.TurnStartTeamBlock += 3;
                        mods.DodgeChanceOnHit += 0.15f;
                    }
                    else if (relic.Id == RelicIds.JadeDagger)
                        mods.JadeDaggerFirstKillBonus = true;

                    break;
                case "evolved_from_burning_boots":
                    if (relic.Id == RelicIds.CrimsonBurningBoots)
                    {
                        mods.BattleStartSpeedBonusTurns = System.Math.Max(mods.BattleStartSpeedBonusTurns, 2);
                        mods.BattleStartSpeedBonus += 2;
                        mods.TurnStartEnemyBurnStacks += 2;
                    }

                    break;
                case "jade_series":
                case "flame_series":
                    break;
                case "back_target_any":
                    mods.BackRowAttackAnyTarget = true;
                    break;
                case "revenge_atk_4":
                    mods.RevengeAttackFlatBonus += 4;
                    break;
                case "revive_2_per_run":
                    break;
                case "post_battle_heal_3pct":
                    mods.PostBattleTeamHealPercent += 3f;
                    break;
                case "holysun_spellbook":
                    mods.HolysunSpellbookBonusUpgradeLevels = 3;
                    break;
                case "front_burn_target_15x":
                    mods.FrontRowBurnTargetDamageMultiplier = System.Math.Max(
                        mods.FrontRowBurnTargetDamageMultiplier, 1.5f);
                    break;
                case "front_ignore_armor_75pct":
                    mods.FrontRowIgnoreArmorDamagePercent = 75;
                    break;
                case "felskull_choice":
                    mods.RequiresFelskullChoice = true;
                    break;
            }
        }

        static Dictionary<string, RelicDefinition> Build()
        {
            var list = new[]
            {
                Def(RelicIds.SunPyramid, "太阳金字塔", RelicRarity.Rare, "法老专属",
                    "法老给予的护甲量+33%。法老每次施放状态类卡牌时，全队获得3点护甲。",
                    "pharaoh_only", requiredCharacterId: "char_mage"),
                Def(RelicIds.KnightInCastle, "城堡骑士", RelicRarity.Rare, "战士专属",
                    "战士拥有护甲期间获得20%减伤。战士每回合首次被攻击时自动获得12点护甲。",
                    "warrior_only", requiredCharacterId: "char_knight"),
                Def(RelicIds.BloodAlter, "血祭坛", RelicRarity.Rare, "恶魔专属",
                    "恶魔使用献祭类卡牌时，献祭的HP消耗减少15%。每次献祭后获得增伤5%（永久）（可叠加）。",
                    "demon_only", requiredCharacterId: "char_ranger"),
                Def(RelicIds.JadeStone, "翡翠原石", RelicRarity.Common, "翡翠系列·基础",
                    "每回合开始时，随机一名队友获得2点护甲。",
                    "evolvable"),
                Def(RelicIds.JadeRing, "翡翠戒指", RelicRarity.Rare, "翡翠系列·防御进化",
                    "每回合开始时，全队获得3点护甲。被攻击时15%概率完全闪避（不受任何伤害）。",
                    "evolved_from_jade_stone", evolutionOnly: true),
                Def(RelicIds.JadeDagger, "翡翠短刀", RelicRarity.Rare, "翡翠系列·攻击进化",
                    "全队获得5%增伤。每场战斗首次击杀敌人时，抽1张牌并回复2点能量。",
                    "evolved_from_jade_stone", evolutionOnly: true, atkPercent: 5),
                Def(RelicIds.BurningBoots, "燃烬之靴", RelicRarity.Common, "烈焰系列·基础",
                    "每场战斗第一回合，全队SPD临时+2（仅影响第一回合结算顺序）。",
                    "evolvable"),
                Def(RelicIds.CrimsonBurningBoots, "赤红烈焰靴", RelicRarity.Rare, "烈焰系列·进化",
                    "每场战斗前2回合全队SPD临时+2。每回合开始时给予所有敌人2层灼烧（永久）。",
                    "evolved_from_burning_boots", evolutionOnly: true),
                Def(RelicIds.FlameSword, "烈焰之剑", RelicRarity.Common, "通用",
                    "全队获得5%增伤。攻击类卡牌有20%概率附加5层灼烧效果（5回合）",
                    "burn_proc_20pct", atkPercent: 5),
                Def(RelicIds.IronArmor, "铁壁战甲", RelicRarity.Common, "通用",
                    "每场战斗开始时，前排角色获得15点护甲。全队获得5%强固",
                    "front_armor_15", blockGainPercent: 5),
                Def(RelicIds.WarriorHelmet, "角斗士之盔", RelicRarity.Common, "通用",
                    "全队HP+8。角色被攻击后，该角色下一次攻击伤害+4。",
                    "revenge_atk_4", hp: 8),
                Def(RelicIds.CatStatue, "猫灵雕像", RelicRarity.Common, "通用",
                    "每场战斗开始时额外抽1张牌（首回合手牌变为6张）。不会再抽取污染的卡牌。",
                    "extra_draw_1"),
                Def(RelicIds.ElfBow, "精灵之弓", RelicRarity.Common, "通用",
                    "后排角色的攻击类卡牌可以指定攻击敌方任意位置的目标（无视敌方位置优先级）。",
                    "back_target_any"),
                Def(RelicIds.DragonRing, "龙纹指环", RelicRarity.Rare, "通用",
                    "全队获得10%增伤。任何角色打出费用≥3的卡牌时，该卡牌伤害额外+15%。",
                    "cost3_plus_15pct", atkPercent: 10),
                Def(RelicIds.PaladinShield, "圣骑之盾", RelicRarity.Rare, "通用",
                    "全队获得10%强固。每回合第一个受到伤害的角色，受伤减少30%",
                    "first_hit_minus_30pct", blockGainPercent: 10),
                Def(RelicIds.SilverMoonPendant, "银月项链", RelicRarity.Rare, "通用",
                    "每回合结束时回复全队2HP。所有增益/减益状态持续时间+1回合（含中毒、灼烧等）。",
                    "heal_2_per_turn"),
                Def(RelicIds.TaichiRing, "太极指环", RelicRarity.Rare, "通用",
                    "每回合中，每个角色打出的第一张攻击牌伤害+5，第一张防御牌额外获得5护甲。若同一角色本回合既出了攻击又出了防御，该角色在回合结束时回复5HP。",
                    "first_atk_5_first_def_5_both_heal_5"),
                Def(RelicIds.LeafOfMiracle, "奇迹之叶", RelicRarity.Epic, "通用",
                    "每次远征限2次：当任何队友HP首次降至0时，该队友不进入死亡状态，而是恢复至20%HP并获得无敌1回合。",
                    "revive_2_per_run"),
                Def(RelicIds.BurningLongsword, "烈火长剑", RelicRarity.Common, "通用",
                    "使我方前排角色对拥有灼烧状态的敌人造成1.5倍的伤害",
                    "front_burn_target_15x"),
                Def(RelicIds.CrystalLongsword, "水晶剑", RelicRarity.Common, "通用",
                    "使我方前排角色的攻击无视对方护甲，但仅造成75%的伤害",
                    "front_ignore_armor_75pct"),
                Def(RelicIds.Bonfire, "便携篝火", RelicRarity.Common, "通用",
                    "每场战斗胜利后，所有我方角色恢复3%HP",
                    "post_battle_heal_3pct"),
                Def(RelicIds.HolysunSpellbook, "圣阳之书", RelicRarity.Common, "法老专属",
                    "当使用任何名字中含有“阳”或“日”的卡牌时，使该卡牌视为等级+3（可超出卡牌升级上限）",
                    "holysun_spellbook", requiredCharacterId: "char_mage"),
                Def(RelicIds.Felskull, "魔焰颅骨", RelicRarity.Common, "通用",
                    "每场战斗开始时，你必须选择其一：A.所有我方角色失去5%HP，本场战斗获得1额外能量上限；B.在本场战斗中失去1点能量上限，所有我方角色的攻击牌增加10%伤害",
                    "felskull_choice")
            };

            var map = new Dictionary<string, RelicDefinition>();
            foreach (var relic in list)
                map[relic.Id] = relic;
            return map;
        }

        static RelicDefinition Def(
            string id,
            string name,
            RelicRarity rarity,
            string category,
            string desc,
            string specialFlag,
            int atk = 0,
            int def = 0,
            int hp = 0,
            string requiredCharacterId = "",
            bool evolutionOnly = false,
            float atkPercent = 0,
            float blockGainPercent = 0) =>
            new()
            {
                Id = id,
                DisplayName = name,
                Rarity = rarity,
                Category = category,
                Description = desc,
                SpecialFlag = specialFlag,
                AtkBonus = atk,
                DefBonus = def,
                HpBonus = hp,
                AtkPercentBonus = atkPercent,
                BlockGainPercentBonus = blockGainPercent,
                RequiredCharacterId = requiredCharacterId,
                EvolutionOnly = evolutionOnly
            };
    }
}
