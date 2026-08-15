using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>
    /// 遗物描述：按成长档替换文案中的有效数值（不写「+1」等档位字眼）。
    /// 数字与 RelicDatabase.ApplySpecialFlag + RelicGrowthRules 一致。
    /// </summary>
    public static class RelicDescriptionFormatter
    {
        public static string Format(string relicId, int growthTiers)
        {
            if (string.IsNullOrEmpty(relicId) || !RelicDatabase.TryGet(relicId, out var relic))
                return "";

            var t = growthTiers < 0 ? 0 : growthTiers;
            return FormatCore(relicId, relic.Description, t);
        }

        public static string Format(RelicDefinition relic, int growthTiers)
        {
            if (relic == null)
                return "";

            var t = growthTiers < 0 ? 0 : growthTiers;
            return FormatCore(relic.Id, relic.Description, t);
        }

        public static string Format(
            string relicId,
            IReadOnlyDictionary<string, int> growthTiers)
        {
            var tiers = RelicGrowthRules.GetGrowthTiers(growthTiers, relicId);
            return Format(relicId, tiers);
        }

        static string FormatCore(string relicId, string fallback, int t)
        {
            switch (relicId)
            {
                case RelicIds.SunPyramid:
                    return $"法老给予的护甲量+33%。法老每次施放状态类卡牌时，全队获得{3 + 5 * t}点护甲。";
                case RelicIds.KnightInCastle:
                    return $"战士拥有护甲期间获得20%减伤。战士每回合首次被攻击时自动获得{12 + 8 * t}点护甲。";
                case RelicIds.BloodAlter:
                    return $"恶魔使用献祭类卡牌时，献祭的HP消耗减少{15 + 5 * t}%。每次献祭后获得增伤5%（永久）（可叠加）。";
                case RelicIds.JadeStone:
                    return $"每回合开始时，随机一名队友获得{2 + 2 * t}点护甲。";
                case RelicIds.JadeRing:
                    return $"每回合开始时，全队获得{3 + 3 * t}点护甲。被攻击时15%概率完全闪避（不受任何伤害）。";
                case RelicIds.JadeDagger:
                    return $"全队获得{5 + 2 * t}%增伤（永久）。每场战斗首次击杀敌人时，下回合额外抽1张牌，额外回复2点能量。";
                case RelicIds.CrimsonBurningBoots:
                    return $"每场战斗前2回合全队SPD临时+2。每回合开始时给予所有敌人{2 + 1 * t}层灼烧（永久）。";
                case RelicIds.FlameSword:
                    return $"全队获得{5 + 2 * t}%增伤。攻击类卡牌有20%概率附加{5 + 5 * t}层灼烧效果（5回合）";
                case RelicIds.IronArmor:
                    return $"每场战斗开始时，前排角色获得{15 + 10 * t}点护甲。全队获得{5 + 2 * t}%强固";
                case RelicIds.WarriorHelmet:
                    return $"全队HP+{8 + 8 * t}。角色被攻击后，该角色下一次攻击伤害+{4 + 4 * t}。";
                case RelicIds.DragonRing:
                    return t > 0
                        ? $"全队获得10%增伤，额外获得{3 * t}点攻击。任何角色打出费用≥3的卡牌时，该卡牌伤害额外+15%。"
                        : "全队获得10%增伤。任何角色打出费用≥3的卡牌时，该卡牌伤害额外+15%。";
                case RelicIds.PaladinShield:
                    return $"全队获得10%强固。每回合第一个受到攻击的友方角色，受伤减少{30 + 5 * t}%";
                case RelicIds.SilverMoonPendant:
                    return $"每回合结束时回复全队{2 + 2 * t}HP。所有增益/减益状态持续时间+1回合（含中毒、灼烧等）。";
                case RelicIds.TaichiRing:
                    return
                        $"每回合中，每个角色打出的第一张攻击牌伤害+{5 + 5 * t}，第一张防御牌额外获得{5 + 5 * t}护甲。若同一角色本回合既出了攻击又出了防御，该角色在回合结束时回复{5 + 5 * t}HP。";
                case RelicIds.LeafOfMiracle:
                    return
                        $"每次远征限2次：当任何队友HP首次降至0时，该队友不进入死亡状态，而是恢复至{20 + 10 * t}%HP并获得无敌1回合。";
                case RelicIds.Bonfire:
                    return $"每场战斗胜利后，所有我方角色恢复{3 + 1 * t}%HP";
                case RelicIds.BottleOfPhantom:
                    return
                        $"当巫妖女王打出任何牌时，对随机一名敌人施加延迟伤害（每张牌{3 + 1 * t}点，下回合开始结算）；可提前在目标脚标看到延迟伤害";
                default:
                    return fallback ?? "";
            }
        }
    }
}
