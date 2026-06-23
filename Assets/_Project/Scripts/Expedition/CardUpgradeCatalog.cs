using System.Collections.Generic;

namespace Grimhand.Expedition
{
    /// <summary>卡牌升级配置（对照 Grimhand实际内容总览表.xlsx · 卡牌 sheet）。</summary>
    public static class CardUpgradeCatalog
    {
        public sealed class UpgradeSpec
        {
            public int MaxUpgrades { get; set; }
            public int DamagePerLevel { get; set; }
            public int BlockPerLevel { get; set; }
            public int HealPerLevel { get; set; }
            public int CostReductionPerLevel { get; set; }
            public int PoisonStacksPerLevel { get; set; }
            public int SlowStacksPerLevel { get; set; }
            public int XpCostPerLevel { get; set; }
        }

        static readonly Dictionary<string, UpgradeSpec> ByDisplayName = Build();

        public static bool TryGetByDisplayName(string displayName, out UpgradeSpec spec)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                spec = null;
                return false;
            }

            return ByDisplayName.TryGetValue(displayName, out spec);
        }

        public static bool CanUpgrade(string displayName, int currentLevel) =>
            TryGetByDisplayName(displayName, out var spec) && currentLevel < spec.MaxUpgrades;

        public static int GetXpCostPerLevel(string displayName) =>
            TryGetByDisplayName(displayName, out var spec) ? spec.XpCostPerLevel : 0;

        static Dictionary<string, UpgradeSpec> Build() => new()
        {
            ["基础斩击"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["举盾格挡"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["防御架势"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["猛力劈砍"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["剑柄猛击"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["嘲讽挑衅"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 2, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["铁壁弹反"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["战士冲锋"] = new() { MaxUpgrades = 5, DamagePerLevel = 3, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["剑刃风暴"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 35 },
            ["战吼鼓舞"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["誓死守护"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["致命打击"] = new() { MaxUpgrades = 5, DamagePerLevel = 3, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["不屈意志"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 5, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 35 },
            ["天神下凡"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 40 },
            ["沙暴射线"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["祈祷祝福"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 1, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["太阳之怒"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["生命汲取"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["法老权令"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["亡灵诅咒"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 25 },
            ["圣甲虫护盾"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["沙尘结界"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["复活祝福"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["日光审判"] = new() { MaxUpgrades = 5, DamagePerLevel = 4, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["沙矛重塑"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 35 },
            ["太阳神之怒"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["太阳神的庇佑"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["暗影爪击"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["恶魔之触"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["鲜血铠甲"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["血尾贯穿"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["血焰爆发"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["灵魂撕裂"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["暗黑献祭"] = new() { MaxUpgrades = 5, DamagePerLevel = 4, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["恶魔契约"] = new() { MaxUpgrades = 2, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["吸血光环"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["诅咒之链"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["地狱烈焰"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["魔王降临"] = new() { MaxUpgrades = 5, DamagePerLevel = 4, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["无尽血刃"] = new() { MaxUpgrades = 5, DamagePerLevel = 5, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 40 },
            ["最终鲜血仪式"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 40 },
        };
    }
}
