using System;

namespace Grimhand.Persistence
{
    [Serializable]
    public sealed class PlayerProfileSaveData
    {
        public int saveVersion;
        public string lastSavedUtc = "";
        public string integrityHash = "";
        public int accountGold;
        public int collectionCapacity;
        public string[] collectionEntries = Array.Empty<string>();
        public CharacterMetaProgressDto[] characters = Array.Empty<CharacterMetaProgressDto>();
        public CampMemberLoadoutDto[] rosterMembers = Array.Empty<CampMemberLoadoutDto>();
    }

    [Serializable]
    public sealed class CharacterMetaProgressDto
    {
        public string characterDefinitionId = "";
        public int outOfRunLevel;
        public int outOfRunXp;
        public string selectedSlot1TalentId = "";
        public string selectedSlot2TalentId = "";
    }

    [Serializable]
    public sealed class CampMemberLoadoutDto
    {
        public string characterDefinitionId = "";
        public string displayName = "";
        public string[] deckCardIds = Array.Empty<string>();
    }
}
