using System.Collections.Generic;

namespace Grimhand.Expedition.Shop
{
    public sealed class ExpeditionShopState
    {
        public const int SlotCount = 6;
        public const int BaseRefreshCost = 20;

        public List<ShopOffer> Offers { get; } = new();
        public int RefreshCount { get; set; }

        public int NextRefreshCost =>
            BaseRefreshCost * (1 << RefreshCount);

        public bool IsOpen => Offers.Count > 0;

        public void Clear()
        {
            Offers.Clear();
            RefreshCount = 0;
        }
    }
}
