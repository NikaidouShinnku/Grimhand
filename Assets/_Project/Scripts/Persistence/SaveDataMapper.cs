using System;
using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Persistence
{
    public static class SaveDataMapper
    {
        public static PlayerProfileSaveData ToDto(PlayerProfileState profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            var dto = new PlayerProfileSaveData
            {
                saveVersion = PlayerProfileState.CurrentSaveVersion,
                lastSavedUtc = DateTime.UtcNow.ToString("o"),
                accountGold = profile.AccountGold,
                collectionCapacity = profile.CollectionCapacity,
                collectionEntries = profile.Collection?.Entries?.ToArray() ?? Array.Empty<string>()
            };

            var characters = new List<CharacterMetaProgressDto>();
            if (profile.Meta?.Characters != null)
            {
                foreach (var pair in profile.Meta.Characters)
                {
                    var progress = pair.Value;
                    if (progress == null)
                        continue;

                    characters.Add(new CharacterMetaProgressDto
                    {
                        characterDefinitionId = progress.CharacterDefinitionId,
                        outOfRunLevel = progress.OutOfRunLevel,
                        outOfRunXp = progress.OutOfRunXp,
                        selectedSlot1TalentId = progress.SelectedSlot1TalentId ?? "",
                        selectedSlot2TalentId = progress.SelectedSlot2TalentId ?? ""
                    });
                }
            }

            dto.characters = characters.ToArray();

            var members = new List<CampMemberLoadoutDto>();
            if (profile.Roster?.Members != null)
            {
                foreach (var member in profile.Roster.Members)
                {
                    if (member == null)
                        continue;

                    members.Add(new CampMemberLoadoutDto
                    {
                        characterDefinitionId = member.CharacterDefinitionId ?? "",
                        displayName = member.DisplayName ?? "",
                        deckCardIds = member.DeckCardIds?.ToArray() ?? Array.Empty<string>()
                    });
                }
            }

            dto.rosterMembers = members.ToArray();
            return dto;
        }

        public static PlayerProfileState FromDto(PlayerProfileSaveData dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var profile = new PlayerProfileState
            {
                AccountGold = dto.accountGold,
                CollectionCapacity = dto.collectionCapacity
            };

            if (dto.collectionEntries != null)
            {
                foreach (var entry in dto.collectionEntries)
                    profile.Collection.TryAddEntry(entry);
            }

            if (dto.characters != null)
            {
                foreach (var characterDto in dto.characters)
                {
                    if (characterDto == null || string.IsNullOrEmpty(characterDto.characterDefinitionId))
                        continue;

                    profile.Meta.Characters[characterDto.characterDefinitionId] = new CharacterMetaProgress
                    {
                        CharacterDefinitionId = characterDto.characterDefinitionId,
                        OutOfRunLevel = characterDto.outOfRunLevel,
                        OutOfRunXp = characterDto.outOfRunXp,
                        SelectedSlot1TalentId = characterDto.selectedSlot1TalentId ?? "",
                        SelectedSlot2TalentId = characterDto.selectedSlot2TalentId ?? ""
                    };
                }
            }

            if (dto.rosterMembers != null)
            {
                foreach (var memberDto in dto.rosterMembers)
                {
                    if (memberDto == null)
                        continue;

                    var member = new CampMemberLoadout
                    {
                        CharacterDefinitionId = memberDto.characterDefinitionId ?? "",
                        DisplayName = memberDto.displayName ?? ""
                    };

                    if (memberDto.deckCardIds != null)
                    {
                        foreach (var cardId in memberDto.deckCardIds)
                            member.DeckCardIds.Add(cardId ?? "");
                    }

                    profile.Roster.Members.Add(member);
                }
            }

            return profile;
        }
    }
}
