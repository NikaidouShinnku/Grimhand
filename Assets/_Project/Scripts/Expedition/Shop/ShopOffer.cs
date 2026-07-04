using Grimhand.Battle.Model;
using Grimhand.Expedition;

namespace Grimhand.Expedition.Shop
{
    public sealed class ShopOffer
    {
        public ShopOfferKind Kind { get; set; }
        public int Price { get; set; }
        public bool Sold { get; set; }

        public string CardPackId { get; set; } = "";

        public string ConsumableId { get; set; } = "";
        public string ConsumableDisplayName { get; set; } = "";

        public string RelicId { get; set; } = "";
        public string RelicDisplayName { get; set; } = "";
        public RelicRarity RelicRarity { get; set; } = RelicRarity.Common;

        public string DisplayLabel =>
            Kind switch
            {
                ShopOfferKind.CardPack => CardPackIds.GetDisplayName(CardPackId),
                ShopOfferKind.Consumable => ConsumableDisplayName,
                ShopOfferKind.Relic => RelicDisplayName,
                _ => ""
            };
    }
}
