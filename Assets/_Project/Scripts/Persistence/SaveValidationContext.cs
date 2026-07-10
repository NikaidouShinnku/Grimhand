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
        public int MinCollectionCapacity { get; set; } = CampCollectionState.DefaultCapacity;
        public int MaxCollectionCapacity { get; set; } = 999;
        public int MaxAccountGold { get; set; } = 999_999_999;
    }
}
