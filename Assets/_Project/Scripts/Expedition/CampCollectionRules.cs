using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>军营收藏上限与玩法拦截规则。</summary>
    public static class CampCollectionRules
    {
        /// <summary>收藏已达或超过上限（含刚好装满）。</summary>
        public static bool IsAtOrOverCapacity(CampCollectionState collection, int capacity) =>
            collection != null && collection.Count >= capacity;

        /// <summary>收藏严格超出上限（多于格子）。</summary>
        public static bool IsOverCapacity(CampCollectionState collection, int capacity) =>
            collection != null && collection.Count > capacity;

        /// <summary>仅超额时禁止出征；刚好装满仍可开游戏。</summary>
        public static bool BlocksExpeditionStart(CampCollectionState collection, int capacity) =>
            IsOverCapacity(collection, capacity);

        /// <summary>收藏已满（含刚好装满）时禁止继续开包。</summary>
        public static bool BlocksShopCardPack(CampCollectionState collection, int capacity) =>
            IsAtOrOverCapacity(collection, capacity);

        /// <summary>白20 / 绿50 / 蓝100 / 紫300 / 橙1000。</summary>
        public static int GetSellGold(CardRarity rarity) =>
            rarity switch
            {
                CardRarity.Common => 20,
                CardRarity.Rare => 50,
                CardRarity.SuperRare => 100,
                CardRarity.Epic => 300,
                CardRarity.Legendary => 1000,
                _ => 20
            };

        public static bool TryRemoveCollectionEntry(
            CampCollectionState collection,
            int entryIndex,
            out string message)
        {
            message = "";
            if (collection == null)
            {
                message = "收藏数据无效。";
                return false;
            }

            if (entryIndex < 0 || entryIndex >= collection.Count)
            {
                message = "卡牌不存在。";
                return false;
            }

            if (!collection.TryRemoveAt(entryIndex))
            {
                message = "移除失败。";
                return false;
            }

            message = "已从收藏中永久移除。";
            return true;
        }

        public static bool TrySellCollectionEntry(
            CampCollectionState collection,
            int entryIndex,
            CardRarity rarity,
            out int goldGained,
            out string message)
        {
            goldGained = 0;
            message = "";
            if (!TryRemoveCollectionEntry(collection, entryIndex, out message))
                return false;

            goldGained = GetSellGold(rarity);
            message = $"已出售，获得 {goldGained} 黄金。";
            return true;
        }
    }
}
