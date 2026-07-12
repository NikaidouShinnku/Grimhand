using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>军营编队：从收藏选取卡牌填入祭坛池，每张收藏实例仅能携带一次。</summary>
    public static class CampRosterLoadoutRules
    {
        public const int EmptyCollectionIndex = -1;

        public static void EnsureDeckStructure(CampMemberLoadout member)
        {
            if (member == null)
                return;

            while (member.DeckCardIds.Count < CampRosterState.DeckSize)
                member.DeckCardIds.Add("");

            while (member.DeckCardIds.Count > CampRosterState.DeckSize)
                member.DeckCardIds.RemoveAt(member.DeckCardIds.Count - 1);

            while (member.DeckCollectionEntryIndices.Count < CampRosterState.DeckSize)
                member.DeckCollectionEntryIndices.Add(EmptyCollectionIndex);

            while (member.DeckCollectionEntryIndices.Count > CampRosterState.DeckSize)
                member.DeckCollectionEntryIndices.RemoveAt(member.DeckCollectionEntryIndices.Count - 1);
        }

        public static void EnsureRosterStructure(CampRosterState roster)
        {
            if (roster?.Members == null)
                return;

            foreach (var member in roster.Members)
                EnsureDeckStructure(member);
        }

        public static HashSet<int> CollectAssignedCollectionIndices(CampRosterState roster)
        {
            var assigned = new HashSet<int>();
            if (roster?.Members == null)
                return assigned;

            foreach (var member in roster.Members)
            {
                if (member == null)
                    continue;

                EnsureDeckStructure(member);
                foreach (var index in member.DeckCollectionEntryIndices)
                {
                    if (index >= 0)
                        assigned.Add(index);
                }
            }

            return assigned;
        }

        public static bool IsCollectionEntryAssigned(
            CampRosterState roster,
            int collectionEntryIndex,
            int ignoreMemberIndex = -1,
            int ignoreSlotIndex = -1)
        {
            if (roster?.Members == null || collectionEntryIndex < 0)
                return false;

            for (var memberIndex = 0; memberIndex < roster.Members.Count; memberIndex++)
            {
                var member = roster.Members[memberIndex];
                if (member == null)
                    continue;

                EnsureDeckStructure(member);
                for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
                {
                    if (memberIndex == ignoreMemberIndex && slot == ignoreSlotIndex)
                        continue;

                    if (member.DeckCollectionEntryIndices[slot] == collectionEntryIndex)
                        return true;
                }
            }

            return false;
        }

        public static void ClearSlot(CampMemberLoadout member, int slotIndex)
        {
            if (member == null || slotIndex < 0 || slotIndex >= CampRosterState.DeckSize)
                return;

            EnsureDeckStructure(member);
            member.DeckCollectionEntryIndices[slotIndex] = EmptyCollectionIndex;
            member.DeckCardIds[slotIndex] = "";
        }

        public static bool TryAssignCollectionEntry(
            CampRosterState roster,
            CampCollectionState collection,
            IReadOnlyDictionary<string, string> cardOwnerById,
            int memberIndex,
            int slotIndex,
            int collectionEntryIndex,
            out string error)
        {
            error = "";
            if (roster?.Members == null || collection == null)
            {
                error = "编队或收藏数据无效。";
                return false;
            }

            if (memberIndex < 0 || memberIndex >= roster.Members.Count)
            {
                error = "成员索引无效。";
                return false;
            }

            if (slotIndex < 0 || slotIndex >= CampRosterState.DeckSize)
            {
                error = "槽位索引无效。";
                return false;
            }

            if (collectionEntryIndex < 0 || collectionEntryIndex >= collection.Count)
            {
                error = "收藏条目无效。";
                return false;
            }

            var member = roster.Members[memberIndex];
            EnsureDeckStructure(member);
            var cardId = collection.Entries[collectionEntryIndex];
            if (string.IsNullOrEmpty(cardId))
            {
                error = "收藏条目为空。";
                return false;
            }

            if (!IsCardOwnedByCharacter(cardId, member.CharacterDefinitionId, cardOwnerById))
            {
                error = "该卡牌不属于当前角色。";
                return false;
            }

            if (IsCollectionEntryAssigned(roster, collectionEntryIndex, memberIndex, slotIndex))
            {
                error = "该收藏卡牌已被其他槽位携带。";
                return false;
            }

            member.DeckCollectionEntryIndices[slotIndex] = collectionEntryIndex;
            member.DeckCardIds[slotIndex] = cardId;
            return true;
        }

        public static void SanitizeRoster(
            CampRosterState roster,
            CampCollectionState collection,
            IReadOnlyDictionary<string, string> cardOwnerById)
        {
            if (roster?.Members == null)
                return;

            var assigned = new HashSet<int>();
            foreach (var member in roster.Members)
            {
                if (member == null)
                    continue;

                EnsureDeckStructure(member);
                for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
                {
                    var entryIndex = member.DeckCollectionEntryIndices[slot];
                    if (entryIndex >= 0)
                    {
                        if (!TryKeepAssignedSlot(member, slot, entryIndex, collection, cardOwnerById, assigned))
                            ClearSlot(member, slot);

                        continue;
                    }

                    var legacyCardId = member.DeckCardIds[slot];
                    if (string.IsNullOrEmpty(legacyCardId))
                        continue;

                    var matched = TryFindFirstUnassignedMatchingEntry(
                        collection,
                        cardOwnerById,
                        member.CharacterDefinitionId,
                        legacyCardId,
                        assigned);
                    if (matched >= 0)
                    {
                        member.DeckCollectionEntryIndices[slot] = matched;
                        member.DeckCardIds[slot] = collection.Entries[matched];
                        assigned.Add(matched);
                    }
                    else
                    {
                        ClearSlot(member, slot);
                    }
                }
            }
        }

        public static bool IsCardOwnedByCharacter(
            string cardDefinitionId,
            string characterDefinitionId,
            IReadOnlyDictionary<string, string> cardOwnerById)
        {
            if (string.IsNullOrEmpty(cardDefinitionId) || string.IsNullOrEmpty(characterDefinitionId))
                return false;

            if (cardOwnerById == null || !cardOwnerById.TryGetValue(cardDefinitionId, out var owner))
                return true;

            if (string.IsNullOrEmpty(owner))
                return true;

            return owner == characterDefinitionId;
        }

        static bool TryKeepAssignedSlot(
            CampMemberLoadout member,
            int slot,
            int entryIndex,
            CampCollectionState collection,
            IReadOnlyDictionary<string, string> cardOwnerById,
            HashSet<int> assigned)
        {
            if (collection == null || entryIndex < 0 || entryIndex >= collection.Count)
                return false;

            if (assigned.Contains(entryIndex))
                return false;

            var cardId = collection.Entries[entryIndex];
            if (string.IsNullOrEmpty(cardId))
                return false;

            if (!IsCardOwnedByCharacter(cardId, member.CharacterDefinitionId, cardOwnerById))
                return false;

            member.DeckCardIds[slot] = cardId;
            assigned.Add(entryIndex);
            return true;
        }

        static int TryFindFirstUnassignedMatchingEntry(
            CampCollectionState collection,
            IReadOnlyDictionary<string, string> cardOwnerById,
            string characterDefinitionId,
            string cardId,
            HashSet<int> assigned)
        {
            if (collection == null || string.IsNullOrEmpty(cardId))
                return EmptyCollectionIndex;

            if (!IsCardOwnedByCharacter(cardId, characterDefinitionId, cardOwnerById))
                return EmptyCollectionIndex;

            for (var i = 0; i < collection.Count; i++)
            {
                if (assigned.Contains(i))
                    continue;

                if (collection.Entries[i] == cardId)
                    return i;
            }

            return EmptyCollectionIndex;
        }

        public static void OnCollectionEntryRemoved(CampRosterState roster, int removedIndex)
        {
            if (roster?.Members == null || removedIndex < 0)
                return;

            foreach (var member in roster.Members)
            {
                if (member == null)
                    continue;

                EnsureDeckStructure(member);
                for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
                {
                    var entryIndex = member.DeckCollectionEntryIndices[slot];
                    if (entryIndex == removedIndex)
                        ClearSlot(member, slot);
                    else if (entryIndex > removedIndex)
                        member.DeckCollectionEntryIndices[slot] = entryIndex - 1;
                }
            }
        }
    }
}
