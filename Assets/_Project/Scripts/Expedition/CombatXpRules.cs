using Grimhand.Core;

namespace Grimhand.Expedition
{
    /// <summary>战斗胜利经验（对照怪物组合表「战斗后经验奖励」）。</summary>
    public static class CombatXpRules
    {
        public static int Roll(BattleRng rng, int floor, bool isElite, bool isBoss)
        {
            if (isBoss)
            {
                if (floor >= 60)
                    return 55;

                if (floor >= 40)
                    return 40;

                return 25;
            }

            if (floor >= 41)
                return RollRange(rng, isElite ? 28 : 18, isElite ? 36 : 24);

            if (floor >= 21)
                return RollRange(rng, isElite ? 23 : 13, isElite ? 27 : 17);

            return RollRange(rng, isElite ? 14 : 8, isElite ? 20 : 10);
        }

        static int RollRange(BattleRng rng, int min, int max)
        {
            if (rng == null || min >= max)
                return min;

            return min + rng.NextIndex(max - min + 1);
        }
    }
}
