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
        /// <summary>与 Entries 等长；true 表示该条目已刻印（不可再刻）。</summary>
        public List<bool> EngravedFlags { get; } = new();

        public int Count => Entries.Count;

        /// <summary>入库；超上限仍允许（见 CampCollectionRules）。</summary>
        public void TryAddEntry(string cardId) => TryAddEntry(cardId, isEngraved: false);

        public void TryAddEntry(string cardId, bool isEngraved)
        {
            if (string.IsNullOrEmpty(cardId))
                return;

            Entries.Add(cardId);
            EngravedFlags.Add(isEngraved);
        }

        public bool IsEngravedAt(int index) =>
            index >= 0 && index < EngravedFlags.Count && EngravedFlags[index];

        public bool TryRemoveAt(int index)
        {
            if (index < 0 || index >= Entries.Count)
                return false;

            Entries.RemoveAt(index);
            if (index < EngravedFlags.Count)
                EngravedFlags.RemoveAt(index);
            return true;
        }

        public void EnsureEngravedFlagsAligned()
        {
            while (EngravedFlags.Count < Entries.Count)
                EngravedFlags.Add(false);
            while (EngravedFlags.Count > Entries.Count)
                EngravedFlags.RemoveAt(EngravedFlags.Count - 1);
        }
    }
}
