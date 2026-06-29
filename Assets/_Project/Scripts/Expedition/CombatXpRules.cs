using Grimhand.Core;

namespace Grimhand.Expedition
{
    /// <summary>战斗胜利经验（对照 v0.8 怪物组合表「战斗后经验奖励」）。</summary>
    public static class CombatXpRules
    {
        public static int Roll(BattleRng rng, int floor, bool isElite, bool isBoss)
        {
            if (isBoss)
            {
                if (floor >= 60)
                    return 80;

                if (floor >= 40)
                    return 60;

                return 40;
            }

            if (floor >= 41)
                return RollRange(rng, isElite ? 36 : 24, isElite ? 46 : 30);

            if (floor >= 21)
                return RollRange(rng, isElite ? 30 : 17, isElite ? 35 : 22);

            return RollRange(rng, isElite ? 17 : 10, isElite ? 22 : 13);
        }

        static int RollRange(BattleRng rng, int min, int max)
        {
            if (rng == null || min >= max)
                return min;

            return min + rng.NextIndex(max - min + 1);
        }
    }
}
