using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>军营收藏上限与玩法拦截规则。</summary>
    public static class CampCollectionRules
    {
        public static bool IsOverCapacity(CampCollectionState collection, int capacity) =>
            collection != null && collection.Count > capacity;

        public static bool BlocksExpeditionStart(CampCollectionState collection, int capacity) =>
            IsOverCapacity(collection, capacity);

        /// <summary>收藏超出上限时禁止继续开包。</summary>
        public static bool BlocksShopCardPack(CampCollectionState collection, int capacity) =>
            IsOverCapacity(collection, capacity);

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
    }
}
