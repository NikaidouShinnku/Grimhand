using System.Collections.Generic;

namespace Grimhand.Expedition.Shop
{
    public sealed class ExpeditionShopState
    {
        public const int SlotCount = 6;
        public const int RefreshCostIncrement = 20;

        public List<ShopOffer> Offers { get; } = new();
        public int RefreshCount { get; set; }

        /// <summary>第一次刷新 0 金币，之后每次 +20。</summary>
        public int NextRefreshCost => RefreshCount * RefreshCostIncrement;

        public bool IsOpen => Offers.Count > 0;

        public void Clear()
        {
            Offers.Clear();
            RefreshCount = 0;
        }
    }
}
