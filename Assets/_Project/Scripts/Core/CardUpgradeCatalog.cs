using System.Collections.Generic;

namespace Grimhand.Core
{
    /// <summary>卡牌升级配置（对照 Grimhand实际内容总览表 v0.8 · 卡牌 sheet）。</summary>
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
            public int DrawPerLevel { get; set; }
            public int DamageReductionPerLevel { get; set; }
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

            if (ByDisplayName.TryGetValue(displayName, out spec))
                return true;

            if (displayName == "太阳审判" && ByDisplayName.TryGetValue("日光审判", out spec))
                return true;

            spec = null;
            return false;
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
            ["战吼鼓舞"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["猛力劈砍"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["剑柄猛击"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["嘲讽挑衅"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 2, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["铁壁弹反"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["战士冲锋"] = new() { MaxUpgrades = 5, DamagePerLevel = 3, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["誓死守护"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["致命打击"] = new() { MaxUpgrades = 5, DamagePerLevel = 3, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["剑刃风暴"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["不屈意志"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 5, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["天神下凡"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["沙暴射线"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["祈祷祝福"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 1, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["圣甲虫护盾"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["太阳之怒"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["沙尘结界"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["生命汲取"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["法老权令"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["亡灵诅咒"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["日光审判"] = new() { MaxUpgrades = 5, DamagePerLevel = 4, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["复活祝福"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["沙矛重塑"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["太阳神之怒"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["太阳神的庇佑"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["暗影爪击"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["恶魔之触"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["鲜血铠甲"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["恶魔契约"] = new() { MaxUpgrades = 2, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["吸血光环"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 5, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["血尾贯穿"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["血焰爆发"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["灵魂撕裂"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["诅咒之链"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["地狱烈焰"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["暗黑献祭"] = new() { MaxUpgrades = 5, DamagePerLevel = 4, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["魔王降临"] = new() { MaxUpgrades = 5, DamagePerLevel = 4, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["无尽血刃"] = new() { MaxUpgrades = 5, DamagePerLevel = 5, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["最终鲜血仪式"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["荆棘护甲"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["报复打击"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["战斗咆哮"] = new() { MaxUpgrades = 2, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["无畏冲锋"] = new() { MaxUpgrades = 5, DamagePerLevel = 3, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["重整旗鼓"] = new() { MaxUpgrades = 2, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["生命之泉"] = new() { MaxUpgrades = 2, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 1, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["苦痛转化"] = new() { MaxUpgrades = 3, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 1, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["魔神回响"] = new() { MaxUpgrades = 5, DamagePerLevel = 5, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["毒雾弥漫"] = new() { MaxUpgrades = 3, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 1, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["女王之吻"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 3, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            ["万蛇噬心"] = new() { MaxUpgrades = 6, DamagePerLevel = 5, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 30 },
            // 毒蛇女王初始组（Excel 卡牌表）
            ["蛇牙撕咬"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["蟒蛇守护"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["剧毒之触"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["女王威信"] = new() { MaxUpgrades = 1, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, DrawPerLevel = 1, XpCostPerLevel = 8 },
            ["灵质护盾"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 1, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["灵能箭雨"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 10 },
            ["灵界封印"] = new() { MaxUpgrades = 1, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 1, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            ["灵魂强化"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            // 巫妖女王初始组（Excel 卡牌表；虚化形态/聚能不可升级）
            ["幽灵爪击"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 8 },
            ["灵魂风暴"] = new() { MaxUpgrades = 5, DamagePerLevel = 1, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 15 },
            // v0.92 新增可升级卡
            ["借机攻击架势"] = new() { MaxUpgrades = 5, DamagePerLevel = 2, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, XpCostPerLevel = 20 },
            ["刀枪不入"] = new() { MaxUpgrades = 5, DamagePerLevel = 0, BlockPerLevel = 0, HealPerLevel = 0, CostReductionPerLevel = 0, PoisonStacksPerLevel = 0, SlowStacksPerLevel = 0, DamageReductionPerLevel = 2, XpCostPerLevel = 15 },
        };
    }
}
