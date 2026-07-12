using System;
using System.Collections.Generic;
using Grimhand.Expedition;
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
                        deckCardIds = member.DeckCardIds?.ToArray() ?? Array.Empty<string>(),
                        deckCollectionEntryIndices = member.DeckCollectionEntryIndices?.ToArray() ?? Array.Empty<int>()
                    });
                }
            }

            dto.rosterMembers = members.ToArray();
            dto.hasActiveRun = profile.HasActiveRun;
            if (profile.ActiveRun != null && profile.ActiveRun.HasRun)
            {
                dto.activeRunVersion = profile.ActiveRun.Version;
                dto.activeRunMapStartLayer = profile.ActiveRun.MapStartLayer;
                dto.activeRunSeed = profile.ActiveRun.RunSeed;
                dto.activeRunRngState = profile.ActiveRun.RngState.ToString();
                dto.activeRunMetaGoldSynced = profile.ActiveRun.MetaGoldSyncedRunGold;
                dto.activeRunJson = profile.ActiveRun.RunJson ?? "";
            }

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

                    var progress = new CharacterMetaProgress
                    {
                        CharacterDefinitionId = characterDto.characterDefinitionId,
                        OutOfRunLevel = characterDto.outOfRunLevel,
                        OutOfRunXp = characterDto.outOfRunXp,
                        SelectedSlot1TalentId = characterDto.selectedSlot1TalentId ?? "",
                        SelectedSlot2TalentId = characterDto.selectedSlot2TalentId ?? ""
                    };
                    MetaProgressionRules.NormalizeProgress(progress);
                    profile.Meta.Characters[characterDto.characterDefinitionId] = progress;
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

                    if (memberDto.deckCollectionEntryIndices != null)
                    {
                        foreach (var entryIndex in memberDto.deckCollectionEntryIndices)
                            member.DeckCollectionEntryIndices.Add(entryIndex);
                    }

                    CampRosterLoadoutRules.EnsureDeckStructure(member);

                    profile.Roster.Members.Add(member);
                }
            }

            CampRosterLoadoutRules.SanitizeRoster(profile.Roster, profile.Collection, null);

            if (dto.hasActiveRun && !string.IsNullOrWhiteSpace(dto.activeRunJson))
            {
                ulong.TryParse(dto.activeRunRngState, out var rngState);
                profile.ActiveRun = new ActiveRunSnapshot
                {
                    Version = dto.activeRunVersion > 0 ? dto.activeRunVersion : ActiveRunSnapshot.CurrentVersion,
                    MapStartLayer = dto.activeRunMapStartLayer > 0 ? dto.activeRunMapStartLayer : 1,
                    RunSeed = dto.activeRunSeed,
                    RngState = rngState > 0 ? rngState : 1,
                    MetaGoldSyncedRunGold = dto.activeRunMetaGoldSynced,
                    RunJson = dto.activeRunJson
                };
            }

            return profile;
        }
    }
}
