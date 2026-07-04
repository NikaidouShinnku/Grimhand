using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition;

namespace Grimhand.Expedition.Shop
{
    public static class ExpeditionShopPricing
    {
        public const int CommonPackPrice = 40;
        public const int AdvancedPackPrice = 100;
        public const int MasterPackPrice = 250;

        public static int RollRelicPrice(BattleRng rng) =>
            rng.NextInt(120, 181);

        public static int RollConsumablePrice(BattleRng rng) =>
            rng.NextInt(30, 51);
    }
}
