using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class CampCollectionBuilder
    {
        /// <summary>由出征编队的祭坛池生成初始收藏（允许重复 cardId）。</summary>
        public static CampCollectionState BuildInitialFromRoster(CampRosterState roster)
        {
            var collection = new CampCollectionState();
            if (roster?.Members == null)
                return collection;

            foreach (var member in roster.Members)
            {
                if (member?.DeckCardIds == null)
                    continue;

                foreach (var cardId in member.DeckCardIds)
                    collection.TryAddEntry(cardId);
            }

            return collection;
        }
    }
}
