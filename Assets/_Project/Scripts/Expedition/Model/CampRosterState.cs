using System.Collections.Generic;
using Grimhand.Expedition;

namespace Grimhand.Expedition.Model
{
    /// <summary>营地军营保存的出征编队（3 人 × 10 张牌）。</summary>
    public sealed class CampRosterState
    {
        public const int PartySize = 3;
        public const int DeckSize = 10;

        public List<CampMemberLoadout> Members { get; } = new();

        public bool IsReadyForExpedition
        {
            get
            {
                if (Members.Count != PartySize)
                    return false;

                foreach (var member in Members)
                {
                    if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                        return false;

                    if (member.DeckCardIds == null || member.DeckCardIds.Count != DeckSize)
                        return false;
                }

                if (!CampRosterValidation.HasUniqueCharacters(this))
                    return false;

                return true;
            }
        }
    }

    public sealed class CampMemberLoadout
    {
        public string CharacterDefinitionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<string> DeckCardIds { get; } = new();

        /// <summary>与 DeckCardIds 同槽位；指向 CampCollectionState.Entries 下标，-1 表示空槽。</summary>
        public List<int> DeckCollectionEntryIndices { get; } = new();
    }
}
