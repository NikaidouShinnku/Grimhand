using Grimhand.Core;

namespace Grimhand.Expedition
{
    public enum ExpeditionRegion
    {
        Cave,
        Dungeon,
        Abyss
    }

    public sealed class CombatRewardProfile
    {
        public int XpMin { get; set; }
        public int XpMax { get; set; }
        public int GoldMin { get; set; }
        public int GoldMax { get; set; }
        public int RelicChancePercent { get; set; }
        public int ConsumableChancePercent { get; set; }
        public int CommonPackChancePercent { get; set; }
        public int AdvancedPackChancePercent { get; set; }
        public int MasterPackChancePercent { get; set; }
    }

    /// <summary>战斗胜利奖励（对照 v0.9 怪物组合表）。</summary>
    public static class CombatRewardRules
    {
        public static ExpeditionRegion ResolveRegion(int floor, bool isBoss)
        {
            if (isBoss)
            {
                if (floor >= 60)
                    return ExpeditionRegion.Abyss;

                if (floor >= 40)
                    return ExpeditionRegion.Dungeon;

                return ExpeditionRegion.Cave;
            }

            if (floor >= 41)
                return ExpeditionRegion.Abyss;

            if (floor >= 21)
                return ExpeditionRegion.Dungeon;

            return ExpeditionRegion.Cave;
        }

        public static CombatRewardProfile GetProfile(int floor, bool isElite, bool isBoss)
        {
            if (isBoss)
            {
                var region = ResolveRegion(floor, true);
                return region switch
                {
                    ExpeditionRegion.Abyss => BossProfile(80, 80),
                    ExpeditionRegion.Dungeon => BossProfile(60, 60),
                    _ => BossProfile(40, 40)
                };
            }

            var elite = isElite;
            var regionNormal = ResolveRegion(floor, false);
            return regionNormal switch
            {
                ExpeditionRegion.Abyss when elite => NormalEliteProfile(36, 46, 38, 45, 50, 50, 100, 50),
                ExpeditionRegion.Abyss => NormalEliteProfile(24, 30, 28, 35, 10, 20, 100, 20),
                ExpeditionRegion.Dungeon when elite => NormalEliteProfile(30, 35, 30, 38, 50, 50, 100, 50),
                ExpeditionRegion.Dungeon => NormalEliteProfile(17, 22, 20, 25, 10, 20, 100, 20),
                _ when elite => NormalEliteProfile(18, 25, 20, 28, 50, 50, 100, 50),
                _ => NormalEliteProfile(10, 13, 15, 20, 10, 20, 100, 20)
            };
        }

        public static int RollXp(BattleRng rng, int floor, bool isElite, bool isBoss)
        {
            var profile = GetProfile(floor, isElite, isBoss);
            return RollRange(rng, profile.XpMin, profile.XpMax);
        }

        public static int RollGold(BattleRng rng, int floor, bool isElite, bool isBoss)
        {
            var profile = GetProfile(floor, isElite, isBoss);
            return RollRange(rng, profile.GoldMin, profile.GoldMax);
        }

        static CombatRewardProfile BossProfile(int xp, int gold) =>
            new()
            {
                XpMin = xp,
                XpMax = xp,
                GoldMin = gold,
                GoldMax = gold,
                RelicChancePercent = 100,
                ConsumableChancePercent = 100,
                CommonPackChancePercent = 0,
                AdvancedPackChancePercent = 0,
                MasterPackChancePercent = 100
            };

        static CombatRewardProfile NormalEliteProfile(
            int xpMin,
            int xpMax,
            int goldMin,
            int goldMax,
            int relicChance,
            int consumableChance,
            int commonPackChance,
            int advancedPackChance) =>
            new()
            {
                XpMin = xpMin,
                XpMax = xpMax,
                GoldMin = goldMin,
                GoldMax = goldMax,
                RelicChancePercent = relicChance,
                ConsumableChancePercent = consumableChance,
                CommonPackChancePercent = commonPackChance,
                AdvancedPackChancePercent = advancedPackChance,
                MasterPackChancePercent = 0
            };

        static int RollRange(BattleRng rng, int min, int max)
        {
            if (rng == null || min >= max)
                return min;

            return min + rng.NextIndex(max - min + 1);
        }
    }
}
