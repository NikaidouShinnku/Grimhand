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

        /// <summary>商店开包拦截（玩法后续实装）。</summary>
        public static bool BlocksShopCardPack(CampCollectionState collection, int capacity) =>
            IsOverCapacity(collection, capacity);
    }
}
