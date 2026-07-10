using System.Collections.Generic;

namespace Grimhand.Expedition.Model
{
    /// <summary>军营共用收藏库（全角色计数；允许重复 cardId）。</summary>
    public sealed class CampCollectionState
    {
        public const int DefaultCapacity = 30;

        public List<string> Entries { get; } = new();

        public int Count => Entries.Count;

        /// <summary>入库；超上限仍允许（见 CampCollectionRules）。</summary>
        public void TryAddEntry(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return;

            Entries.Add(cardId);
        }
    }
}
