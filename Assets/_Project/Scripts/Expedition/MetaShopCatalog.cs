namespace Grimhand.Expedition
{
    public enum MetaShopOfferKind
    {
        CardPack = 0,
        CollectionCapacityUpgrade = 1
    }

    /// <summary>局外商店商品表。</summary>
    public static class MetaShopCatalog
    {
        public const string CollectionCapacityUpgradeId = "upgrade_card_limit";
        public const int CollectionCapacityUpgradePrice = 1000;

        public sealed class Offer
        {
            public MetaShopOfferKind Kind { get; set; }
            public string OfferId { get; set; } = "";
            /// <summary>卡包商品时等于 PackId。</summary>
            public string PackId { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int Price { get; set; }
            public string Hint { get; set; } = "";
        }

        public static readonly Offer[] AllOffers =
        {
            new()
            {
                Kind = MetaShopOfferKind.CardPack,
                OfferId = CardPackIds.Common,
                PackId = CardPackIds.Common,
                DisplayName = "",
                Price = 500,
                Hint = "一次开出 3 张卡牌，全部加入收藏；概率与局内一致。"
            },
            new()
            {
                Kind = MetaShopOfferKind.CardPack,
                OfferId = CardPackIds.Advanced,
                PackId = CardPackIds.Advanced,
                DisplayName = "",
                Price = 2000,
                Hint = "一次开出 3 张卡牌，全部加入收藏；概率与局内一致。"
            },
            new()
            {
                Kind = MetaShopOfferKind.CardPack,
                OfferId = CardPackIds.Master,
                PackId = CardPackIds.Master,
                DisplayName = "",
                Price = 5000,
                Hint = "一次开出 3 张卡牌，全部加入收藏；概率与局内一致。"
            },
            new()
            {
                Kind = MetaShopOfferKind.CollectionCapacityUpgrade,
                OfferId = CollectionCapacityUpgradeId,
                PackId = "",
                DisplayName = "升级卡牌收藏上限",
                Price = CollectionCapacityUpgradePrice,
                Hint = "花费金币将军营收藏上限 +1（最高 100）。"
            }
        };

        /// <summary>兼容旧调用：仅卡包列表。</summary>
        public static readonly Offer[] DemoCardPacks =
        {
            AllOffers[0],
            AllOffers[1],
            AllOffers[2]
        };

        public static Offer Find(string offerId)
        {
            if (string.IsNullOrEmpty(offerId))
                return null;

            foreach (var offer in AllOffers)
            {
                if (offer.OfferId == offerId || offer.PackId == offerId)
                    return offer;
            }

            return null;
        }

        public static int GetPrice(string packId)
        {
            var offer = Find(packId);
            return offer?.Price ?? 0;
        }

        public static string GetDisplayName(Offer offer)
        {
            if (offer == null)
                return "";

            if (offer.Kind == MetaShopOfferKind.CardPack)
                return CardPackIds.GetDisplayName(offer.PackId);

            return string.IsNullOrEmpty(offer.DisplayName) ? offer.OfferId : offer.DisplayName;
        }
    }
}
