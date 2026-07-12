using System;
using System.Text;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Persistence
{
    public static class SaveValidator
    {
        public static bool TryValidate(PlayerProfileSaveData dto, SaveValidationContext context, out string error)
        {
            error = "";
            if (dto == null)
            {
                error = "存档 DTO 为空。";
                return false;
            }

            if (dto.saveVersion <= 0 || dto.saveVersion > PlayerProfileState.CurrentSaveVersion)
            {
                error = $"未知 saveVersion: {dto.saveVersion}";
                return false;
            }

            if (dto.accountGold < 0 || dto.accountGold > context.MaxAccountGold)
            {
                error = $"accountGold 非法: {dto.accountGold}";
                return false;
            }

            if (dto.collectionCapacity < context.MinCollectionCapacity
                || dto.collectionCapacity > context.MaxCollectionCapacity)
            {
                error = $"collectionCapacity 非法: {dto.collectionCapacity}";
                return false;
            }

            if (dto.collectionEntries != null)
            {
                foreach (var cardId in dto.collectionEntries)
                {
                    if (string.IsNullOrEmpty(cardId) || !context.ValidCardIds.Contains(cardId))
                    {
                        error = $"收藏库含非法 cardId: {cardId}";
                        return false;
                    }
                }
            }

            if (dto.characters != null)
            {
                foreach (var character in dto.characters)
                {
                    if (!TryValidateCharacter(character, context, out error))
                        return false;
                }
            }

            if (dto.rosterMembers != null)
            {
                foreach (var member in dto.rosterMembers)
                {
                    if (!TryValidateRosterMember(member, context, out error))
                        return false;
                }
            }

            if (dto.hasActiveRun)
            {
                if (string.IsNullOrWhiteSpace(dto.activeRunJson))
                {
                    error = "hasActiveRun 为 true 但 activeRunJson 为空。";
                    return false;
                }

                if (dto.activeRunMapStartLayer <= 0)
                {
                    error = $"activeRunMapStartLayer 非法: {dto.activeRunMapStartLayer}";
                    return false;
                }
            }

            return true;
        }

        static bool TryValidateCharacter(CharacterMetaProgressDto character, SaveValidationContext context, out string error)
        {
            error = "";
            if (character == null || string.IsNullOrEmpty(character.characterDefinitionId))
            {
                error = "角色 Meta 缺少 characterDefinitionId。";
                return false;
            }

            if (character.outOfRunLevel < 1 || character.outOfRunLevel > context.MaxCharacterLevel)
            {
                error = $"角色 {character.characterDefinitionId} 等级非法: {character.outOfRunLevel}";
                return false;
            }

            if (character.outOfRunXp < 0)
            {
                error = $"角色 {character.characterDefinitionId} 经验非法: {character.outOfRunXp}";
                return false;
            }

            if (!TryValidateTalentSelection(character, 1, character.selectedSlot1TalentId, context, out error))
                return false;

            if (!TryValidateTalentSelection(character, 2, character.selectedSlot2TalentId, context, out error))
                return false;

            return true;
        }

        static bool TryValidateTalentSelection(
            CharacterMetaProgressDto character,
            int slot,
            string talentId,
            SaveValidationContext context,
            out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(talentId))
                return true;

            if (!context.ValidTalentIds.Contains(talentId))
            {
                error = $"角色 {character.characterDefinitionId} 槽位 {slot} 天赋非法: {talentId}";
                return false;
            }

            var talent = TalentCatalog.Get(talentId);
            if (talent == null
                || talent.CharacterId != character.characterDefinitionId
                || talent.Slot != slot
                || character.outOfRunLevel < talent.UnlockLevel)
            {
                error = $"角色 {character.characterDefinitionId} 天赋 {talentId} 与等级不匹配。";
                return false;
            }

            return true;
        }

        static bool TryValidateRosterMember(CampMemberLoadoutDto member, SaveValidationContext context, out string error)
        {
            error = "";
            if (member == null)
                return true;

            if (member.deckCardIds == null)
                return true;

            if (member.deckCardIds.Length > CampRosterState.DeckSize)
            {
                error = $"角色 {member.characterDefinitionId} 祭坛池超过 {CampRosterState.DeckSize} 槽。";
                return false;
            }

            if (member.deckCollectionEntryIndices != null
                && member.deckCollectionEntryIndices.Length > CampRosterState.DeckSize)
            {
                error = $"角色 {member.characterDefinitionId} 收藏绑定超过 {CampRosterState.DeckSize} 槽。";
                return false;
            }

            foreach (var cardId in member.deckCardIds)
            {
                if (string.IsNullOrEmpty(cardId))
                    continue;

                if (!context.ValidCardIds.Contains(cardId))
                {
                    error = $"编队 cardId 非法: {cardId}";
                    return false;
                }

                if (!string.IsNullOrEmpty(member.characterDefinitionId)
                    && context.CardOwnerById.TryGetValue(cardId, out var owner)
                    && !string.IsNullOrEmpty(owner)
                    && owner != member.characterDefinitionId)
                {
                    error = $"卡牌 {cardId} 不属于角色 {member.characterDefinitionId}。";
                    return false;
                }
            }

            return true;
        }
    }
}
