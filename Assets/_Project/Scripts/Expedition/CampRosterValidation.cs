using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class CampRosterValidation
    {
        public static bool HasUniqueCharacters(CampRosterState roster)
        {
            if (roster?.Members == null)
                return false;

            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var member in roster.Members)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                if (!seen.Add(member.CharacterDefinitionId))
                    return false;
            }

            return true;
        }

        public static int FindMemberIndexWithCharacter(
            CampRosterState roster,
            string characterDefinitionId,
            int excludeIndex = -1)
        {
            if (roster?.Members == null || string.IsNullOrEmpty(characterDefinitionId))
                return -1;

            for (var i = 0; i < roster.Members.Count; i++)
            {
                if (i == excludeIndex)
                    continue;

                var member = roster.Members[i];
                if (member != null && member.CharacterDefinitionId == characterDefinitionId)
                    return i;
            }

            return -1;
        }

        public static void SwapMembers(CampRosterState roster, int indexA, int indexB)
        {
            if (roster?.Members == null)
                return;

            if (indexA < 0 || indexB < 0 || indexA >= roster.Members.Count || indexB >= roster.Members.Count)
                return;

            if (indexA == indexB)
                return;

            var temp = CloneLoadout(roster.Members[indexA]);
            CopyLoadout(roster.Members[indexB], roster.Members[indexA]);
            CopyLoadout(temp, roster.Members[indexB]);
        }

        static CampMemberLoadout CloneLoadout(CampMemberLoadout source)
        {
            var copy = new CampMemberLoadout
            {
                CharacterDefinitionId = source?.CharacterDefinitionId ?? "",
                DisplayName = source?.DisplayName ?? ""
            };

            if (source?.DeckCardIds != null)
            {
                foreach (var id in source.DeckCardIds)
                    copy.DeckCardIds.Add(id ?? "");
            }

            if (source?.DeckCollectionEntryIndices != null)
            {
                foreach (var index in source.DeckCollectionEntryIndices)
                    copy.DeckCollectionEntryIndices.Add(index);
            }

            CampRosterLoadoutRules.EnsureDeckStructure(copy);
            return copy;
        }

        static void CopyLoadout(CampMemberLoadout source, CampMemberLoadout target)
        {
            if (source == null || target == null)
                return;

            target.CharacterDefinitionId = source.CharacterDefinitionId;
            target.DisplayName = source.DisplayName;
            target.DeckCardIds.Clear();
            foreach (var id in source.DeckCardIds)
                target.DeckCardIds.Add(id ?? "");

            target.DeckCollectionEntryIndices.Clear();
            foreach (var index in source.DeckCollectionEntryIndices)
                target.DeckCollectionEntryIndices.Add(index);

            CampRosterLoadoutRules.EnsureDeckStructure(target);
        }
    }
}
