using Grimhand.Battle.Model;

namespace Grimhand.Battle
{
    public static class CardRarityRules
    {
        public static CardRarity UpgradeRarity(CardRarity current) =>
            current switch
            {
                CardRarity.Common => CardRarity.Rare,
                CardRarity.Rare => CardRarity.SuperRare,
                CardRarity.SuperRare => CardRarity.Epic,
                CardRarity.Epic => CardRarity.Legendary,
                _ => CardRarity.Legendary
            };
    }
}
