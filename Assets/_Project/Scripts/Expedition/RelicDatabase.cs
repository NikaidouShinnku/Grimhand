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

        public static RunModifierSnapshot BuildModifiers(IReadOnlyList<string> relicIds)
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
                ApplySpecialFlag(mods, relic);
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
                    mods.HealBonusPercent += 30f;
                    mods.StatusCardTeamBlock += 3;
                    break;
                case "warrior_only":
                    mods.WarriorFirstHitBlockAmount += 8;
                    break;
                case "demon_only":
                    mods.SacrificeHpCostReduction += 3;
                    mods.SacrificeStackAttackBonus += 1;
                    break;
                case "front_armor_10":
                    mods.BattleStartFrontBlock += 10;
                    break;
                case "extra_draw_1":
                    mods.ExtraDrawOnBattleStart += 1;
                    break;
                case "burn_proc_20pct":
                    mods.AttackBurnProcChance = 0.20f;
                    mods.AttackBurnDamagePerTurn = 3;
                    mods.AttackBurnDurationTurns = 2;
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
                        mods.EndTurnEnemyFireDamage += 3;
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
            }
        }

        static Dictionary<string, RelicDefinition> Build()
        {
            var list = new[]
            {
                Def(RelicIds.SunPyramid, "太阳金字塔", RelicRarity.Rare, "法老专属",
                    "法老治疗效果+30%。法老每次施放状态类卡牌时，全队获得3点护甲。",
                    "pharaoh_only", requiredCharacterId: "char_mage"),
                Def(RelicIds.KnightInCastle, "城堡骑士", RelicRarity.Rare, "战士专属",
                    "战士每回合首次被攻击时自动获得8点护甲。",
                    "warrior_only", requiredCharacterId: "char_knight"),
                Def(RelicIds.BloodAlter, "血祭坛", RelicRarity.Rare, "恶魔专属",
                    "恶魔使用献祭类卡牌时HP消耗减少3点。每次献祭后本场战斗ATK+1（可叠加，战斗结束重置）。",
                    "demon_only", requiredCharacterId: "char_ranger"),
                Def(RelicIds.JadeStone, "翡翠原石", RelicRarity.Common, "翡翠系列",
                    "每回合开始时随机1名队友获得2点护甲。",
                    "evolvable"),
                Def(RelicIds.JadeRing, "翡翠戒指", RelicRarity.Rare, "翡翠进化·防御",
                    "每回合开始时全队获得3点护甲。被攻击时15%概率完全闪避。",
                    "evolved_from_jade_stone", evolutionOnly: true),
                Def(RelicIds.JadeDagger, "翡翠短刀", RelicRarity.Rare, "翡翠进化·攻击",
                    "全队ATK+2。每场战斗首次击杀敌人时，击杀者额外抽1张牌并回复2点能量。",
                    "evolved_from_jade_stone", atk: 2, evolutionOnly: true),
                Def(RelicIds.BurningBoots, "燃烬之靴", RelicRarity.Common, "烈焰系列",
                    "每场战斗第一回合全队SPD临时+2（仅影响第一回合结算顺序）。",
                    "evolvable"),
                Def(RelicIds.CrimsonBurningBoots, "赤红烈焰靴", RelicRarity.Rare, "烈焰进化",
                    "每场战斗前2回合全队SPD临时+2。每回合结束时对所有敌人造成3点火焰伤害（无视DEF）。",
                    "evolved_from_burning_boots", evolutionOnly: true),
                Def(RelicIds.FlameSword, "烈焰之剑", RelicRarity.Common, "通用",
                    "全队ATK+2。攻击类卡牌有20%概率附加灼烧（3伤害/回合×2回合，无视DEF）。",
                    "burn_proc_20pct", atk: 2),
                Def(RelicIds.IronArmor, "铁壁战甲", RelicRarity.Common, "通用",
                    "全队DEF+2。每场战斗开始时前排角色获得10点护甲。",
                    "front_armor_10", def: 2),
                Def(RelicIds.WarriorHelmet, "角斗士之盔", RelicRarity.Common, "通用",
                    "全队HP+8。角色被攻击后，该角色下一次攻击伤害+4。",
                    "revenge_atk_4", hp: 8),
                Def(RelicIds.CatStatue, "猫灵雕像", RelicRarity.Common, "通用",
                    "每场战斗开始时额外抽1张牌（首回合6张手牌）。",
                    "extra_draw_1"),
                Def(RelicIds.ElfBow, "精灵之弓", RelicRarity.Common, "通用",
                    "后排角色的攻击类卡牌可指定攻击敌方任意位置目标（无视敌方位置优先级）。",
                    "back_target_any"),
                Def(RelicIds.DragonRing, "龙纹指环", RelicRarity.Rare, "通用",
                    "全队ATK+3。任何角色打出费用>=3的卡牌时，该卡伤害额外+15%。",
                    "cost3_plus_15pct", atk: 3),
                Def(RelicIds.PaladinShield, "圣骑之盾", RelicRarity.Rare, "通用",
                    "全队DEF+3。每回合每个角色首次受伤减少30%（每人每回合各触发1次）。",
                    "first_hit_minus_30pct", def: 3),
                Def(RelicIds.SilverMoonPendant, "银月项链", RelicRarity.Rare, "通用",
                    "每回合结束时回复全队2HP。所有增益/减益状态持续+1回合。",
                    "heal_2_per_turn"),
                Def(RelicIds.TaichiRing, "太极指环", RelicRarity.Rare, "通用",
                    "每回合每个角色：第一张攻击牌伤害+5，第一张防御牌护甲+5。同一角色本回合既攻又防则回复5HP。",
                    "first_atk_5_first_def_5_both_heal_5"),
                Def(RelicIds.LeafOfMiracle, "奇迹之叶", RelicRarity.Epic, "通用",
                    "每次远征限2次：队友HP降至0时不死亡，恢复20%HP并获得无敌1回合。",
                    "revive_2_per_run")
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
            bool evolutionOnly = false) =>
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
                RequiredCharacterId = requiredCharacterId,
                EvolutionOnly = evolutionOnly
            };
    }
}
