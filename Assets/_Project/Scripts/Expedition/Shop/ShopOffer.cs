using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Shop
{
    public sealed class ShopOffer
    {
        public ShopOfferKind Kind { get; set; }
        public int Price { get; set; }
        public bool Sold { get; set; }

        public string CardDefinitionId { get; set; } = "";
        public string CardOwnerCharacterId { get; set; } = "";
        public string CardDisplayName { get; set; } = "";
        public CardRarity CardRarity { get; set; } = CardRarity.Common;

        public string ConsumableId { get; set; } = "";
        public string ConsumableDisplayName { get; set; } = "";

        public string RelicId { get; set; } = "";
        public string RelicDisplayName { get; set; } = "";
        public RelicRarity RelicRarity { get; set; } = RelicRarity.Common;

        public string DisplayLabel =>
            Kind switch
            {
                ShopOfferKind.Card => CardDisplayName,
                ShopOfferKind.Consumable => ConsumableDisplayName,
                ShopOfferKind.Relic => RelicDisplayName,
                _ => ""
            };
    }
}
