namespace Grimhand.Expedition
{
    /// <summary>局外商店商品表（Demo：三种卡包）。</summary>
    public static class MetaShopCatalog
    {
        public sealed class Offer
        {
            public string PackId { get; set; } = "";
            public int Price { get; set; }
            public string Hint { get; set; } = "";
        }

        public static readonly Offer[] DemoCardPacks =
        {
            new()
            {
                PackId = CardPackIds.Common,
                Price = 500,
                Hint = "一次开出 3 张卡牌，全部加入收藏；概率与局内一致。"
            },
            new()
            {
                PackId = CardPackIds.Advanced,
                Price = 2000,
                Hint = "一次开出 3 张卡牌，全部加入收藏；概率与局内一致。"
            },
            new()
            {
                PackId = CardPackIds.Master,
                Price = 5000,
                Hint = "一次开出 3 张卡牌，全部加入收藏；概率与局内一致。"
            }
        };

        public static int GetPrice(string packId)
        {
            foreach (var offer in DemoCardPacks)
            {
                if (offer.PackId == packId)
                    return offer.Price;
            }

            return 0;
        }
    }
}
