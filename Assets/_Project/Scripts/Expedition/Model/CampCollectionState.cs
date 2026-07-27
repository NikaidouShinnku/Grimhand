using System.Collections.Generic;

namespace Grimhand.Expedition.Model
{
    /// <summary>军营共用收藏库（全角色计数；允许重复 cardId）。</summary>
    public sealed class CampCollectionState
    {
        /// <summary>新档开局收藏上限。</summary>
        public const int DefaultCapacity = 40;
        /// <summary>商店「升级卡牌收藏上限」可升到的最大值。</summary>
        public const int MaxCapacity = 100;

        public List<string> Entries { get; } = new();

        public int Count => Entries.Count;

        /// <summary>入库；超上限仍允许（见 CampCollectionRules）。</summary>
        public void TryAddEntry(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return;

            Entries.Add(cardId);
        }

        public bool TryRemoveAt(int index)
        {
            if (index < 0 || index >= Entries.Count)
                return false;

            Entries.RemoveAt(index);
            return true;
        }
    }
}
