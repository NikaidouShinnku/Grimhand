using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Persistence
{
    public sealed class SaveValidationContext
    {
        public HashSet<string> ValidCardIds { get; } = new();
        public HashSet<string> ValidTalentIds { get; } = new();
        public Dictionary<string, string> CardOwnerById { get; } = new();
        public int MaxCharacterLevel { get; set; } = 10;
        /// <summary>允许旧档 30 上限；新档默认 40。</summary>
        public int MinCollectionCapacity { get; set; } = 30;
        public int MaxCollectionCapacity { get; set; } = CampCollectionState.MaxCapacity;
        public int MaxAccountGold { get; set; } = 999_999_999;
    }
}
