using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Expedition.Shop
{
    public static class ExpeditionShopPricing
    {
        public static int RollCardPrice(CardRarity rarity, BattleRng rng)
        {
            var basePrice = rarity switch
            {
                CardRarity.Common => 45,
                CardRarity.Rare => 70,
                CardRarity.Epic => 140,
                CardRarity.SuperRare => 105,
                CardRarity.Legendary => 240,
                _ => 45
            };

            return ApplyFluctuation(basePrice, rng);
        }

        public static int RollConsumablePrice(BattleRng rng) =>
            ApplyFluctuation(32, rng);

        public static int RollRelicPrice(RelicRarity rarity, BattleRng rng)
        {
            var basePrice = rarity switch
            {
                RelicRarity.Rare => 220,
                RelicRarity.Epic => 280,
                _ => 175
            };

            return ApplyFluctuation(basePrice, rng);
        }

        static int ApplyFluctuation(int basePrice, BattleRng rng)
        {
            var percent = rng.NextInt(85, 116);
            return System.Math.Max(1, basePrice * percent / 100);
        }
    }
}
