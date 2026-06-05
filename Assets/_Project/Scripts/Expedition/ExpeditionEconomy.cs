using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class ExpeditionEconomy
    {
        public static int RollVictoryGold(ExpeditionConfig config, BattleRng rng)
        {
            if (config == null || rng == null)
                return 0;

            var min = config.GoldMinPerVictory;
            var max = config.GoldMaxPerVictory;
            if (min > max)
                (min, max) = (max, min);

            if (max <= 0)
                return 0;

            if (min < 0)
                min = 0;

            return rng.NextInt(min, max + 1);
        }
    }
}
