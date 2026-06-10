using Grimhand.Battle.Model;

namespace Grimhand.Battle
{
    public static class CardRarityRules
    {
        public static CardRarity UpgradeRarity(CardRarity current) =>
            current switch
            {
                CardRarity.Common => CardRarity.Rare,
                CardRarity.Rare => CardRarity.Epic,
                CardRarity.Epic => CardRarity.SuperRare,
                CardRarity.SuperRare => CardRarity.Legendary,
                _ => CardRarity.Legendary
            };
    }
}
