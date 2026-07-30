using System;
using System.Collections.Generic;
using System.Linq;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;
using UnityEngine;

namespace Grimhand.Persistence
{
    public static class ExpeditionRunSaveCodec
    {
        public static string Serialize(ExpeditionRunState run, ulong rngState)
        {
            if (run == null)
                return "";

            var dto = ToDto(run);
            return JsonUtility.ToJson(dto);
        }

        public static bool TryDeserialize(
            string json,
            ExpeditionConfig config,
            out ExpeditionRunState run,
            out ulong rngState)
        {
            run = null;
            rngState = 1;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            ExpeditionRunSaveData dto;
            try
            {
                dto = JsonUtility.FromJson<ExpeditionRunSaveData>(json);
            }
            catch
            {
                return false;
            }

            if (dto == null || dto.version <= 0)
                return false;

            run = FromDto(dto, config);
            return run != null;
        }

        public static ExpeditionRunState CloneRunState(ExpeditionRunState source) =>
            ExpeditionRunStateCopy.Clone(source);

        static ExpeditionRunSaveData ToDto(ExpeditionRunState run)
        {
            var dto = new ExpeditionRunSaveData
            {
                version = ExpeditionRunSaveData.CurrentVersion,
                phase = (int)run.Phase,
                battlesWon = run.BattlesWon,
                targetBattleCount = run.TargetBattleCount,
                gold = run.Gold,
                lastGoldReward = run.LastGoldReward,
                lastXpReward = run.LastXpReward,
                totalGoldGained = run.TotalGoldGained,
                totalXpGained = run.TotalXpGained,
                sharedXpPool = run.SharedXpPool,
                lastBattleWasElite = run.LastBattleWasElite,
                lastBattleWasBoss = run.LastBattleWasBoss,
                lastBattleFloor = run.LastBattleFloor,
                hasEventResolutionFixedRoll100 = run.EventResolutionFixedRoll100.HasValue,
                eventResolutionFixedRoll100 = run.EventResolutionFixedRoll100 ?? 0,
                lastEventMessage = run.LastEventMessage ?? "",
                pendingConsumableOfferId = run.PendingConsumableOfferId ?? "",
                miracleLeafUsesRemaining = run.MiracleLeafUsesRemaining,
                currentBossDisplayName = run.CurrentBossDisplayName ?? "",
                pendingEventBattleKey = run.PendingEventBattleKey ?? "",
                pendingEventBattleBonusXp = run.PendingEventBattleBonusXp,
                pendingTravelerGiftRelicId = run.PendingTravelerGiftRelicId ?? "",
                pendingTravelerGiftCurseOwnerId = run.PendingTravelerGiftCurseOwnerId ?? "",
                v09EtherealEntryCount = run.V09EtherealEntryCount,
                v09ExpeditionRespondSuccessCount = run.V09ExpeditionRespondSuccessCount,
                v09SandSpearExhaustCardsPlayed = run.V09SandSpearExhaustCardsPlayed,
                chestRewardRevealed = run.ChestRewardRevealed,
                modifiers = ToModifiersDto(run.Modifiers),
                map = ToMapDto(run.Map),
                pendingRewardPickup = ToRewardPickupDto(run.PendingRewardPickup),
                pendingEvent = ToPendingEventDto(run.PendingEvent),
                pendingEventAftermath = ToPendingEventAftermathDto(run.PendingEventAftermath),
                eventInteraction = ToEventInteractionDto(run.EventInteraction),
                pendingEventBattleVictoryReward = ToRewardPickupDto(run.PendingEventBattleVictoryReward),
                pendingDeferredReward = ToRewardPickupDto(run.PendingDeferredReward),
                pendingShrine = ToPendingShrineDto(run.PendingShrine),
                shop = ToShopDto(run.Shop),
                talentRun = ToTalentRunDto(run.TalentRun),
                cardAltar = ToCardAltarDto(run.CardAltar),
                engravedDeckInstanceIds = ToStringArray(run.EngravedDeckInstanceIds),
                altarExtractedDeckInstanceIds = ToStringArray(run.AltarExtractedDeckInstanceIds),
                pendingCardEngravings = ToPendingEngravingsDto(run.PendingCardEngravings),
                pendingCardOffer = ToPendingCardOfferDto(run.PendingCardOffer),
                pendingCardPackOffer = ToPendingCardPackOfferDto(run.PendingCardPackOffer)
            };

            dto.party = ToPartyDto(run.Party);
            dto.relics = run.Relics.ToArray();
            dto.relicGrowthTiers = ToStringIntPairs(run.RelicGrowthTiers);
            dto.usedEventIds = run.UsedEventIds.ToArray();
            dto.eventFlags = run.EventFlags.ToArray();
            dto.consumableSlots = run.ConsumableSlots.ToArray();
            dto.runAcquisitionLog = run.RunAcquisitionLog.ToArray();
            dto.pendingRoutes = ToRoutesDto(run.PendingRoutes);
            dto.runWideBonusCards = ToCardRefs(run.RunWideBonusCards);
            dto.runStartCampDecks = ToRunStartCampDecksDto(run.RunStartCampDecks);
            dto.extractedCampCollectionIndices = ToExtractedCampIndicesDto(run.ExtractedCampCollectionIndices);
            return dto;
        }

        static ExpeditionRunState FromDto(ExpeditionRunSaveData dto, ExpeditionConfig config)
        {
            if (dto == null)
                return null;

            var run = new ExpeditionRunState
            {
                Phase = (ExpeditionPhase)dto.phase,
                BattlesWon = dto.battlesWon,
                TargetBattleCount = dto.targetBattleCount,
                Gold = dto.gold,
                LastGoldReward = dto.lastGoldReward,
                LastXpReward = dto.lastXpReward,
                TotalGoldGained = dto.totalGoldGained,
                TotalXpGained = dto.totalXpGained,
                SharedXpPool = dto.sharedXpPool,
                LastBattleWasElite = dto.lastBattleWasElite,
                LastBattleWasBoss = dto.lastBattleWasBoss,
                LastBattleFloor = dto.lastBattleFloor,
                EventResolutionFixedRoll100 = dto.hasEventResolutionFixedRoll100 ? dto.eventResolutionFixedRoll100 : null,
                LastEventMessage = dto.lastEventMessage ?? "",
                PendingConsumableOfferId = dto.pendingConsumableOfferId ?? "",
                MiracleLeafUsesRemaining = dto.miracleLeafUsesRemaining,
                CurrentBossDisplayName = dto.currentBossDisplayName ?? "",
                PendingEventBattleKey = dto.pendingEventBattleKey ?? "",
                PendingEventBattleBonusXp = dto.pendingEventBattleBonusXp,
                PendingTravelerGiftRelicId = dto.pendingTravelerGiftRelicId ?? "",
                PendingTravelerGiftCurseOwnerId = dto.pendingTravelerGiftCurseOwnerId ?? "",
                V09EtherealEntryCount = dto.v09EtherealEntryCount,
                V09ExpeditionRespondSuccessCount = dto.v09ExpeditionRespondSuccessCount,
                V09SandSpearExhaustCardsPlayed = dto.v09SandSpearExhaustCardsPlayed,
                ChestRewardRevealed = dto.chestRewardRevealed
            };

            FromModifiersDto(dto.modifiers, run.Modifiers);
            run.Map = FromMapDto(dto.map);
            run.PendingRewardPickup = FromRewardPickupDto(dto.pendingRewardPickup, config);
            run.PendingEvent = FromPendingEventDto(dto.pendingEvent);
            run.PendingEventAftermath = FromPendingEventAftermathDto(dto.pendingEventAftermath);
            run.EventInteraction = FromEventInteractionDto(dto.eventInteraction, config);
            run.PendingEventBattleVictoryReward = FromRewardPickupDto(dto.pendingEventBattleVictoryReward, config);
            run.PendingDeferredReward = FromRewardPickupDto(dto.pendingDeferredReward, config);
            run.PendingShrine = FromPendingShrineDto(dto.pendingShrine);
            FromShopDto(dto.shop, run.Shop);
            FromTalentRunDto(dto.talentRun, run.TalentRun);
            run.CardAltar = FromCardAltarDto(dto.cardAltar);
            if (dto.engravedDeckInstanceIds != null)
            {
                foreach (var id in dto.engravedDeckInstanceIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        run.EngravedDeckInstanceIds.Add(id);
                }
            }

            if (dto.altarExtractedDeckInstanceIds != null)
            {
                foreach (var id in dto.altarExtractedDeckInstanceIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        run.AltarExtractedDeckInstanceIds.Add(id);
                }
            }

            if (dto.pendingCardEngravings != null)
            {
                foreach (var pendingDto in dto.pendingCardEngravings)
                {
                    if (pendingDto == null || string.IsNullOrEmpty(pendingDto.deckInstanceId))
                        continue;
                    run.PendingCardEngravings.Add(new PendingCardEngraving
                    {
                        MemberId = pendingDto.memberId ?? "",
                        DeckInstanceId = pendingDto.deckInstanceId ?? "",
                        DefinitionId = pendingDto.definitionId ?? "",
                        DisplayName = pendingDto.displayName ?? "",
                        BattlesRequired = pendingDto.battlesRequired,
                        BattlesCompleted = pendingDto.battlesCompleted
                    });
                }
            }

            run.PendingCardOffer = FromPendingCardOfferDto(dto.pendingCardOffer, config);
            run.PendingCardPackOffer = FromPendingCardPackOfferDto(dto.pendingCardPackOffer, config);

            FromPartyDto(dto.party, run.Party, config);
            if (dto.relics != null)
                run.Relics.AddRange(dto.relics);
            FillIntDict(run.RelicGrowthTiers, dto.relicGrowthTiers);
            if (dto.usedEventIds != null)
            {
                foreach (var id in dto.usedEventIds)
                    run.UsedEventIds.Add(id);
            }

            if (dto.eventFlags != null)
            {
                foreach (var flag in dto.eventFlags)
                    run.EventFlags.Add(flag);
            }

            if (dto.consumableSlots != null)
                run.ConsumableSlots.AddRange(dto.consumableSlots);
            if (dto.runAcquisitionLog != null)
                run.RunAcquisitionLog.AddRange(dto.runAcquisitionLog);
            FromRoutesDto(dto.pendingRoutes, run.PendingRoutes);
            FromCardRefs(dto.runWideBonusCards, run.RunWideBonusCards, config);
            FromRunStartCampDecksDto(dto.runStartCampDecks, run.RunStartCampDecks);
            FromExtractedCampIndicesDto(dto.extractedCampCollectionIndices, run.ExtractedCampCollectionIndices);
            return run;
        }

        static PartyMemberSaveData[] ToPartyDto(IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (party == null || party.Count == 0)
                return Array.Empty<PartyMemberSaveData>();

            var result = new PartyMemberSaveData[party.Count];
            for (var i = 0; i < party.Count; i++)
                result[i] = ToMemberDto(party[i]);
            return result;
        }

        static PartyMemberSaveData ToMemberDto(PartyMemberSnapshot member)
        {
            if (member == null)
                return new PartyMemberSaveData();

            return new PartyMemberSaveData
            {
                characterDefinitionId = member.CharacterDefinitionId ?? "",
                displayName = member.DisplayName ?? "",
                level = member.Level,
                xp = member.Xp,
                hp = member.Hp,
                maxHp = member.MaxHp,
                maxHpPenalty = member.MaxHpPenalty,
                altarMaxHpBonus = member.AltarMaxHpBonus,
                personalAttackBonus = member.PersonalAttackBonus,
                altarSpeedUpgrades = member.AltarSpeedUpgrades,
                personalSpeedBonus = member.PersonalSpeedBonus,
                usesCampDeckAsBattleBase = member.UsesCampDeckAsBattleBase,
                selectedTalentSlot1Id = member.SelectedTalentSlot1Id ?? "",
                selectedTalentSlot2Id = member.SelectedTalentSlot2Id ?? "",
                removedCardCounts = ToStringIntPairs(member.RemovedCardCounts),
                cardUpgradeLevels = ToStringIntPairs(member.CardUpgradeLevels),
                baseDeckInstanceIds = member.BaseDeckInstanceIds.ToArray(),
                cardPowerBonusPercent = ToStringIntPairs(member.CardPowerBonusPercent),
                cardFlatDamageBonuses = ToStringIntPairs(member.CardFlatDamageBonuses),
                bonusCards = ToCardRefs(member.BonusCards),
                campDeckCardIds = member.CampDeckCardIds.ToArray(),
                extractedCampCardIndices = ToIntArray(member.ExtractedCampCardIndices)
            };
        }

        static void FromPartyDto(PartyMemberSaveData[] dtos, List<PartyMemberSnapshot> party, ExpeditionConfig config)
        {
            party.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                if (dto == null)
                    continue;

                var member = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = dto.characterDefinitionId ?? "",
                    DisplayName = dto.displayName ?? "",
                    Level = dto.level,
                    Xp = dto.xp,
                    Hp = dto.hp,
                    MaxHp = dto.maxHp,
                    MaxHpPenalty = dto.maxHpPenalty,
                    AltarMaxHpBonus = dto.altarMaxHpBonus,
                    PersonalAttackBonus = dto.personalAttackBonus,
                    AltarSpeedUpgrades = dto.altarSpeedUpgrades,
                    PersonalSpeedBonus = dto.personalSpeedBonus,
                    UsesCampDeckAsBattleBase = dto.usesCampDeckAsBattleBase,
                    SelectedTalentSlot1Id = dto.selectedTalentSlot1Id ?? "",
                    SelectedTalentSlot2Id = dto.selectedTalentSlot2Id ?? ""
                };
                FillIntDict(member.RemovedCardCounts, dto.removedCardCounts);
                FillIntDict(member.CardUpgradeLevels, dto.cardUpgradeLevels);
                if (dto.baseDeckInstanceIds != null)
                    member.BaseDeckInstanceIds.AddRange(dto.baseDeckInstanceIds);
                FillIntDict(member.CardPowerBonusPercent, dto.cardPowerBonusPercent);
                FillIntDict(member.CardFlatDamageBonuses, dto.cardFlatDamageBonuses);
                FromCardRefs(dto.bonusCards, member.BonusCards, config);
                if (dto.campDeckCardIds != null)
                    member.CampDeckCardIds.AddRange(dto.campDeckCardIds);
                if (dto.extractedCampCardIndices != null)
                {
                    foreach (var index in dto.extractedCampCardIndices)
                        member.ExtractedCampCardIndices.Add(index);
                }

                party.Add(member);
            }
        }

        static ExpeditionRunModifiersSaveData ToModifiersDto(ExpeditionRunModifiers modifiers)
        {
            if (modifiers == null)
                return new ExpeditionRunModifiersSaveData();

            return new ExpeditionRunModifiersSaveData
            {
                teamAttackBonus = modifiers.TeamAttackBonus,
                teamDefenseBonus = modifiers.TeamDefenseBonus,
                teamBlockGainBonusPercent = modifiers.TeamBlockGainBonusPercent,
                energyCapBonus = modifiers.EnergyCapBonus,
                handLimitBonus = modifiers.HandLimitBonus,
                drawPerTurnBonus = modifiers.DrawPerTurnBonus,
                altarHpPlus5Purchases = modifiers.AltarHpPlus5Purchases,
                altarHpPlus10Purchases = modifiers.AltarHpPlus10Purchases,
                nextCombatEnemyAttackBonus = modifiers.NextCombatEnemyAttackBonus,
                foreseenLayerCount = modifiers.ForeseenLayerCount,
                skipNextRouteSelect = modifiers.SkipNextRouteSelect,
                lootedInjuredAdventurer = modifiers.LootedInjuredAdventurer,
                divinePunishmentActive = modifiers.DivinePunishmentActive,
                soulRiftBattleStartRandomHpLoss = modifiers.SoulRiftBattleStartRandomHpLoss
            };
        }

        static void FromModifiersDto(ExpeditionRunModifiersSaveData dto, ExpeditionRunModifiers modifiers)
        {
            if (dto == null || modifiers == null)
                return;

            modifiers.TeamAttackBonus = dto.teamAttackBonus;
            modifiers.TeamDefenseBonus = dto.teamDefenseBonus;
            modifiers.TeamBlockGainBonusPercent = dto.teamBlockGainBonusPercent;
            modifiers.EnergyCapBonus = dto.energyCapBonus;
            modifiers.HandLimitBonus = dto.handLimitBonus;
            modifiers.DrawPerTurnBonus = dto.drawPerTurnBonus;
            modifiers.AltarHpPlus5Purchases = dto.altarHpPlus5Purchases;
            modifiers.AltarHpPlus10Purchases = dto.altarHpPlus10Purchases;
            modifiers.NextCombatEnemyAttackBonus = dto.nextCombatEnemyAttackBonus;
            modifiers.ForeseenLayerCount = dto.foreseenLayerCount;
            modifiers.SkipNextRouteSelect = dto.skipNextRouteSelect;
            modifiers.LootedInjuredAdventurer = dto.lootedInjuredAdventurer;
            modifiers.DivinePunishmentActive = dto.divinePunishmentActive;
            modifiers.SoulRiftBattleStartRandomHpLoss = dto.soulRiftBattleStartRandomHpLoss;
        }

        static ExpeditionMapSaveData ToMapDto(ExpeditionMapState map)
        {
            if (map == null)
                return null;

            var dto = new ExpeditionMapSaveData
            {
                chapterLayerCount = map.ChapterLayerCount,
                nodesCompleted = map.NodesCompleted
            };

            if (map.Layers.Count == 0)
                return dto;

            var layers = new ExpeditionMapLayerSaveData[map.Layers.Count];
            for (var i = 0; i < map.Layers.Count; i++)
            {
                var layer = map.Layers[i];
                layers[i] = new ExpeditionMapLayerSaveData
                {
                    layerNumber = layer.LayerNumber,
                    isBoss = layer.IsBoss,
                    isRevealed = layer.IsRevealed,
                    hasChosenOptionIndex = layer.ChosenOptionIndex.HasValue,
                    chosenOptionIndex = layer.ChosenOptionIndex ?? 0,
                    options = ToMapOptionsDto(layer.Options)
                };
            }

            dto.layers = layers;
            return dto;
        }

        static ExpeditionMapOptionSaveData[] ToMapOptionsDto(IReadOnlyList<ExpeditionMapOption> options)
        {
            if (options == null || options.Count == 0)
                return Array.Empty<ExpeditionMapOptionSaveData>();

            var result = new ExpeditionMapOptionSaveData[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                result[i] = new ExpeditionMapOptionSaveData
                {
                    nodeType = (int)option.NodeType,
                    displayName = option.DisplayName ?? "",
                    description = option.Description ?? "",
                    pathSpriteIndex = option.PathSpriteIndex,
                    encounterIndex = option.EncounterIndex,
                    monsterEncounterId = option.MonsterEncounterId ?? "",
                    eventId = option.EventId ?? "",
                    shrineId = option.ShrineId ?? "",
                    treasureTier = option.TreasureTier ?? "",
                    isElite = option.IsElite
                };
            }

            return result;
        }

        static ExpeditionMapState FromMapDto(ExpeditionMapSaveData dto)
        {
            if (dto == null)
                return null;

            var map = new ExpeditionMapState
            {
                ChapterLayerCount = dto.chapterLayerCount,
                NodesCompleted = dto.nodesCompleted
            };

            if (dto.layers == null)
                return map;

            foreach (var layerDto in dto.layers)
            {
                if (layerDto == null)
                    continue;

                var layer = new ExpeditionMapLayer
                {
                    LayerNumber = layerDto.layerNumber,
                    IsBoss = layerDto.isBoss,
                    IsRevealed = layerDto.isRevealed,
                    ChosenOptionIndex = layerDto.hasChosenOptionIndex ? layerDto.chosenOptionIndex : null
                };
                FromMapOptionsDto(layerDto.options, layer.Options);
                map.Layers.Add(layer);
            }

            return map;
        }

        static void FromMapOptionsDto(ExpeditionMapOptionSaveData[] dtos, List<ExpeditionMapOption> options)
        {
            options.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                if (dto == null)
                    continue;

                options.Add(new ExpeditionMapOption
                {
                    NodeType = (ExpeditionNodeType)dto.nodeType,
                    DisplayName = dto.displayName ?? "",
                    Description = dto.description ?? "",
                    PathSpriteIndex = dto.pathSpriteIndex,
                    EncounterIndex = dto.encounterIndex,
                    MonsterEncounterId = dto.monsterEncounterId ?? "",
                    EventId = dto.eventId ?? "",
                    ShrineId = dto.shrineId ?? "",
                    TreasureTier = dto.treasureTier ?? "",
                    IsElite = dto.isElite
                });
            }
        }

        static ExpeditionRouteOptionSaveData[] ToRoutesDto(IReadOnlyList<ExpeditionRouteOption> routes)
        {
            if (routes == null || routes.Count == 0)
                return Array.Empty<ExpeditionRouteOptionSaveData>();

            var result = new ExpeditionRouteOptionSaveData[routes.Count];
            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                result[i] = new ExpeditionRouteOptionSaveData
                {
                    id = route.Id ?? "",
                    displayName = route.DisplayName ?? "",
                    description = route.Description ?? "",
                    nodeType = (int)route.NodeType,
                    encounterIndex = route.EncounterIndex,
                    monsterEncounterId = route.MonsterEncounterId ?? "",
                    eventId = route.EventId ?? "",
                    shrineId = route.ShrineId ?? "",
                    treasureTier = route.TreasureTier ?? "",
                    isElite = route.IsElite,
                    layerNumber = route.LayerNumber,
                    mapOptionIndex = route.MapOptionIndex,
                    pathSpriteIndex = route.PathSpriteIndex
                };
            }

            return result;
        }

        static void FromRoutesDto(ExpeditionRouteOptionSaveData[] dtos, List<ExpeditionRouteOption> routes)
        {
            routes.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                if (dto == null)
                    continue;

                routes.Add(new ExpeditionRouteOption
                {
                    Id = dto.id ?? "",
                    DisplayName = dto.displayName ?? "",
                    Description = dto.description ?? "",
                    NodeType = (ExpeditionNodeType)dto.nodeType,
                    EncounterIndex = dto.encounterIndex,
                    MonsterEncounterId = dto.monsterEncounterId ?? "",
                    EventId = dto.eventId ?? "",
                    ShrineId = dto.shrineId ?? "",
                    TreasureTier = dto.treasureTier ?? "",
                    IsElite = dto.isElite,
                    LayerNumber = dto.layerNumber,
                    MapOptionIndex = dto.mapOptionIndex,
                    PathSpriteIndex = dto.pathSpriteIndex
                });
            }
        }

        static ExpeditionRewardPickupSaveData ToRewardPickupDto(ExpeditionRewardPickup pickup)
        {
            if (pickup == null)
                return null;

            return new ExpeditionRewardPickupSaveData
            {
                headerText = pickup.HeaderText ?? "",
                kind = (int)pickup.Kind,
                gold = pickup.Gold,
                goldClaimed = pickup.GoldClaimed,
                goldSkipped = pickup.GoldSkipped,
                relicId = pickup.RelicId ?? "",
                relicClaimed = pickup.RelicClaimed,
                relicSkipped = pickup.RelicSkipped,
                cardDefinitionId = pickup.CardDefinitionId ?? "",
                cardOwnerCharacterId = pickup.CardOwnerCharacterId ?? "",
                cardDisplayName = pickup.CardDisplayName ?? "",
                cardClaimed = pickup.CardClaimed,
                cardSkipped = pickup.CardSkipped,
                cardPacks = ToCardPackEntriesDto(pickup.CardPacks),
                consumableId = pickup.ConsumableId ?? "",
                consumableCount = pickup.ConsumableCount,
                consumableClaimed = pickup.ConsumableClaimed,
                consumableSkipped = pickup.ConsumableSkipped,
                relicEvolveFromId = pickup.RelicEvolveFromId ?? "",
                relicEvolveToId = pickup.RelicEvolveToId ?? "",
                statCharacterId = pickup.StatCharacterId ?? "",
                statCharacterName = pickup.StatCharacterName ?? "",
                teamAttackBonus = pickup.TeamAttackBonus,
                teamDefenseBonus = pickup.TeamDefenseBonus,
                teamBlockGainBonusPercent = pickup.TeamBlockGainBonusPercent,
                energyCapBonus = pickup.EnergyCapBonus,
                personalAttackBonus = pickup.PersonalAttackBonus,
                grantXp = pickup.GrantXp,
                enableSoulRiftBattleStartRandomHpLoss = pickup.EnableSoulRiftBattleStartRandomHpLoss,
                enableDivinePunishment = pickup.EnableDivinePunishment,
                resolveStatCharacterFromInteraction = pickup.ResolveStatCharacterFromInteraction,
                statClaimed = pickup.StatClaimed,
                statSkipped = pickup.StatSkipped
            };
        }

        static CardPackRewardEntrySaveData[] ToCardPackEntriesDto(IReadOnlyList<CardPackRewardEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return Array.Empty<CardPackRewardEntrySaveData>();

            var result = new CardPackRewardEntrySaveData[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                result[i] = new CardPackRewardEntrySaveData
                {
                    packId = entry.PackId ?? "",
                    claimed = entry.Claimed,
                    skipped = entry.Skipped
                };
            }

            return result;
        }

        static ExpeditionRewardPickup FromRewardPickupDto(ExpeditionRewardPickupSaveData dto, ExpeditionConfig config)
        {
            if (dto == null)
                return null;

            var pickup = new ExpeditionRewardPickup
            {
                HeaderText = dto.headerText ?? "",
                Kind = (RewardPickupKind)dto.kind,
                Gold = dto.gold,
                GoldClaimed = dto.goldClaimed,
                GoldSkipped = dto.goldSkipped,
                RelicId = dto.relicId ?? "",
                RelicClaimed = dto.relicClaimed,
                RelicSkipped = dto.relicSkipped,
                CardDefinitionId = dto.cardDefinitionId ?? "",
                CardOwnerCharacterId = dto.cardOwnerCharacterId ?? "",
                CardDisplayName = dto.cardDisplayName ?? "",
                CardClaimed = dto.cardClaimed,
                CardSkipped = dto.cardSkipped,
                ConsumableId = dto.consumableId ?? "",
                ConsumableCount = dto.consumableCount,
                ConsumableClaimed = dto.consumableClaimed,
                ConsumableSkipped = dto.consumableSkipped,
                RelicEvolveFromId = dto.relicEvolveFromId ?? "",
                RelicEvolveToId = dto.relicEvolveToId ?? "",
                StatCharacterId = dto.statCharacterId ?? "",
                StatCharacterName = dto.statCharacterName ?? "",
                TeamAttackBonus = dto.teamAttackBonus,
                TeamDefenseBonus = dto.teamDefenseBonus,
                TeamBlockGainBonusPercent = dto.teamBlockGainBonusPercent,
                EnergyCapBonus = dto.energyCapBonus,
                PersonalAttackBonus = dto.personalAttackBonus,
                GrantXp = dto.grantXp,
                EnableSoulRiftBattleStartRandomHpLoss = dto.enableSoulRiftBattleStartRandomHpLoss,
                EnableDivinePunishment = dto.enableDivinePunishment,
                ResolveStatCharacterFromInteraction = dto.resolveStatCharacterFromInteraction,
                StatClaimed = dto.statClaimed,
                StatSkipped = dto.statSkipped
            };

            if (dto.cardPacks != null)
            {
                foreach (var entryDto in dto.cardPacks)
                {
                    if (entryDto == null)
                        continue;

                    pickup.CardPacks.Add(new CardPackRewardEntry
                    {
                        PackId = entryDto.packId ?? "",
                        Claimed = entryDto.claimed,
                        Skipped = entryDto.skipped
                    });
                }
            }

            return pickup;
        }

        static ExpeditionPendingEventSaveData ToPendingEventDto(ExpeditionPendingEvent pending)
        {
            if (pending == null)
                return null;

            return new ExpeditionPendingEventSaveData
            {
                eventId = pending.EventId ?? "",
                sourceLayer = pending.SourceLayer
            };
        }

        static ExpeditionPendingEvent FromPendingEventDto(ExpeditionPendingEventSaveData dto)
        {
            if (dto == null)
                return null;

            return new ExpeditionPendingEvent
            {
                EventId = dto.eventId ?? "",
                SourceLayer = dto.sourceLayer
            };
        }

        static ExpeditionPendingEventAftermathSaveData ToPendingEventAftermathDto(ExpeditionPendingEventAftermath pending)
        {
            if (pending == null)
                return null;

            return new ExpeditionPendingEventAftermathSaveData
            {
                eventId = pending.EventId ?? "",
                choiceIndex = pending.ChoiceIndex,
                sourceLayer = pending.SourceLayer,
                afterChoiceText = pending.AfterChoiceText ?? "",
                hasFixedRoll100 = pending.FixedRoll100.HasValue,
                fixedRoll100 = pending.FixedRoll100 ?? 0
            };
        }

        static ExpeditionPendingEventAftermath FromPendingEventAftermathDto(ExpeditionPendingEventAftermathSaveData dto)
        {
            if (dto == null)
                return null;

            return new ExpeditionPendingEventAftermath
            {
                EventId = dto.eventId ?? "",
                ChoiceIndex = dto.choiceIndex,
                SourceLayer = dto.sourceLayer,
                AfterChoiceText = dto.afterChoiceText ?? "",
                FixedRoll100 = dto.hasFixedRoll100 ? dto.fixedRoll100 : null
            };
        }

        static ExpeditionPendingShrineSaveData ToPendingShrineDto(ExpeditionPendingShrine pending)
        {
            if (pending == null)
                return null;

            return new ExpeditionPendingShrineSaveData
            {
                shrineId = pending.ShrineId ?? "",
                sourceLayer = pending.SourceLayer
            };
        }

        static ExpeditionPendingShrine FromPendingShrineDto(ExpeditionPendingShrineSaveData dto)
        {
            if (dto == null)
                return null;

            return new ExpeditionPendingShrine
            {
                ShrineId = dto.shrineId ?? "",
                SourceLayer = dto.sourceLayer
            };
        }

        static ExpeditionShopSaveData ToShopDto(ExpeditionShopState shop)
        {
            if (shop == null)
                return new ExpeditionShopSaveData();

            var dto = new ExpeditionShopSaveData { refreshCount = shop.RefreshCount };
            if (shop.Offers.Count == 0)
                return dto;

            var offers = new ShopOfferSaveData[shop.Offers.Count];
            for (var i = 0; i < shop.Offers.Count; i++)
            {
                var offer = shop.Offers[i];
                offers[i] = new ShopOfferSaveData
                {
                    kind = (int)offer.Kind,
                    price = offer.Price,
                    sold = offer.Sold,
                    cardPackId = offer.CardPackId ?? "",
                    consumableId = offer.ConsumableId ?? "",
                    consumableDisplayName = offer.ConsumableDisplayName ?? "",
                    relicId = offer.RelicId ?? "",
                    relicDisplayName = offer.RelicDisplayName ?? "",
                    relicRarity = (int)offer.RelicRarity
                };
            }

            dto.offers = offers;
            return dto;
        }

        static void FromShopDto(ExpeditionShopSaveData dto, ExpeditionShopState shop)
        {
            shop.Clear();
            if (dto == null)
                return;

            shop.RefreshCount = dto.refreshCount;
            if (dto.offers == null)
                return;

            foreach (var offerDto in dto.offers)
            {
                if (offerDto == null)
                    continue;

                shop.Offers.Add(new ShopOffer
                {
                    Kind = (ShopOfferKind)offerDto.kind,
                    Price = offerDto.price,
                    Sold = offerDto.sold,
                    CardPackId = offerDto.cardPackId ?? "",
                    ConsumableId = offerDto.consumableId ?? "",
                    ConsumableDisplayName = offerDto.consumableDisplayName ?? "",
                    RelicId = offerDto.relicId ?? "",
                    RelicDisplayName = offerDto.relicDisplayName ?? "",
                    RelicRarity = (RelicRarity)offerDto.relicRarity
                });
            }
        }

        static ExpeditionTalentRunSaveData ToTalentRunDto(ExpeditionTalentRunState talentRun)
        {
            if (talentRun == null)
                return new ExpeditionTalentRunSaveData();

            return new ExpeditionTalentRunSaveData
            {
                mageReviveUsed = talentRun.MageReviveUsed,
                rangerSacrificeHpTotal = talentRun.RangerSacrificeHpTotal,
                endlessBladeInjected = talentRun.EndlessBladeInjected,
                snakeDetonateVenomInjected = talentRun.SnakeDetonateVenomInjected,
                lichRealmSealInjected = talentRun.LichRealmSealInjected
            };
        }

        static void FromTalentRunDto(ExpeditionTalentRunSaveData dto, ExpeditionTalentRunState talentRun)
        {
            if (dto == null || talentRun == null)
                return;

            talentRun.MageReviveUsed = dto.mageReviveUsed;
            talentRun.RangerSacrificeHpTotal = dto.rangerSacrificeHpTotal;
            talentRun.EndlessBladeInjected = dto.endlessBladeInjected;
            talentRun.SnakeDetonateVenomInjected = dto.snakeDetonateVenomInjected;
            talentRun.LichRealmSealInjected = dto.lichRealmSealInjected;
        }

        static ExpeditionCardAltarSaveData ToCardAltarDto(ExpeditionCardAltarState altar)
        {
            if (altar == null)
                return null;

            var dto = new ExpeditionCardAltarSaveData
            {
                sourceLayer = altar.SourceLayer,
                engraveSlotUsed = altar.EngraveSlotUsed
            };
            if (altar.Drafts.Count == 0)
                return dto;

            var drafts = new ExpeditionCardAltarMemberDraftSaveData[altar.Drafts.Count];
            var index = 0;
            foreach (var pair in altar.Drafts)
            {
                var draft = pair.Value;
                drafts[index++] = new ExpeditionCardAltarMemberDraftSaveData
                {
                    memberId = pair.Key ?? "",
                    collectionCardIndex = draft?.CollectionCardIndex ?? -1,
                    replaceDeckCardKey = draft?.ReplaceDeckCardKey ?? "",
                    confirmed = draft?.Confirmed ?? false
                };
            }

            dto.drafts = drafts;
            return dto;
        }

        static ExpeditionCardAltarState FromCardAltarDto(ExpeditionCardAltarSaveData dto)
        {
            if (dto == null)
                return null;

            var altar = new ExpeditionCardAltarState
            {
                SourceLayer = dto.sourceLayer,
                EngraveSlotUsed = dto.engraveSlotUsed
            };
            if (dto.drafts == null)
                return altar;

            foreach (var draftDto in dto.drafts)
            {
                if (draftDto == null || string.IsNullOrEmpty(draftDto.memberId))
                    continue;

                altar.Drafts[draftDto.memberId] = new ExpeditionCardAltarMemberDraft
                {
                    CollectionCardIndex = draftDto.collectionCardIndex,
                    ReplaceDeckCardKey = draftDto.replaceDeckCardKey ?? "",
                    Confirmed = draftDto.confirmed
                };
            }

            return altar;
        }

        static string[] ToStringArray(HashSet<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            var list = new List<string>(source.Count);
            foreach (var id in source)
            {
                if (!string.IsNullOrEmpty(id))
                    list.Add(id);
            }

            return list.ToArray();
        }

        static PendingCardEngravingSaveData[] ToPendingEngravingsDto(List<PendingCardEngraving> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<PendingCardEngravingSaveData>();

            var list = new List<PendingCardEngravingSaveData>(source.Count);
            foreach (var pending in source)
            {
                if (pending == null || string.IsNullOrEmpty(pending.DeckInstanceId))
                    continue;
                list.Add(new PendingCardEngravingSaveData
                {
                    memberId = pending.MemberId ?? "",
                    deckInstanceId = pending.DeckInstanceId ?? "",
                    definitionId = pending.DefinitionId ?? "",
                    displayName = pending.DisplayName ?? "",
                    battlesRequired = pending.BattlesRequired,
                    battlesCompleted = pending.BattlesCompleted
                });
            }

            return list.ToArray();
        }

        static ExpeditionPendingCardOfferSaveData ToPendingCardOfferDto(ExpeditionPendingCardOffer offer)
        {
            if (offer == null)
                return null;

            return new ExpeditionPendingCardOfferSaveData
            {
                ownerCharacterId = offer.OwnerCharacterId ?? "",
                template = ToCardRef(offer.Template),
                context = (int)offer.Context,
                sourceRewardPackIndex = offer.SourceRewardPackIndex,
                sourceShopSlotIndex = offer.SourceShopSlotIndex,
                sourcePackId = offer.SourcePackId ?? ""
            };
        }

        static ExpeditionPendingCardOffer FromPendingCardOfferDto(
            ExpeditionPendingCardOfferSaveData dto,
            ExpeditionConfig config)
        {
            if (dto == null)
                return null;

            return new ExpeditionPendingCardOffer
            {
                OwnerCharacterId = dto.ownerCharacterId ?? "",
                Template = FromCardRef(dto.template, config),
                Context = (ExpeditionCardOfferContext)dto.context,
                SourceRewardPackIndex = dto.sourceRewardPackIndex,
                SourceShopSlotIndex = dto.sourceShopSlotIndex,
                SourcePackId = dto.sourcePackId ?? ""
            };
        }

        static ExpeditionPendingCardPackOfferSaveData ToPendingCardPackOfferDto(ExpeditionPendingCardPackOffer offer)
        {
            if (offer == null)
                return null;

            var dto = new ExpeditionPendingCardPackOfferSaveData
            {
                packId = offer.PackId ?? "",
                context = (int)offer.Context,
                rewardPackIndex = offer.RewardPackIndex,
                shopSlotIndex = offer.ShopSlotIndex
            };

            if (offer.Choices.Count == 0)
                return dto;

            var choices = new CardPackChoiceSaveData[offer.Choices.Count];
            for (var i = 0; i < offer.Choices.Count; i++)
            {
                var choice = offer.Choices[i];
                choices[i] = new CardPackChoiceSaveData
                {
                    ownerCharacterId = choice?.OwnerCharacterId ?? "",
                    template = ToCardRef(choice?.Template)
                };
            }

            dto.choices = choices;
            return dto;
        }

        static ExpeditionPendingCardPackOffer FromPendingCardPackOfferDto(
            ExpeditionPendingCardPackOfferSaveData dto,
            ExpeditionConfig config)
        {
            if (dto == null)
                return null;

            var offer = new ExpeditionPendingCardPackOffer
            {
                PackId = dto.packId ?? "",
                Context = (ExpeditionCardOfferContext)dto.context,
                RewardPackIndex = dto.rewardPackIndex,
                ShopSlotIndex = dto.shopSlotIndex
            };

            if (dto.choices != null)
            {
                foreach (var choiceDto in dto.choices)
                {
                    if (choiceDto == null)
                        continue;

                    offer.Choices.Add(new CardPackChoice
                    {
                        OwnerCharacterId = choiceDto.ownerCharacterId ?? "",
                        Template = FromCardRef(choiceDto.template, config)
                    });
                }
            }

            return offer;
        }

        static ExpeditionEventInteractionSaveData ToEventInteractionDto(ExpeditionEventInteractionState interaction)
        {
            if (interaction == null)
                return null;

            return new ExpeditionEventInteractionSaveData
            {
                eventId = interaction.EventId ?? "",
                choiceIndex = interaction.ChoiceIndex,
                steps = ToInteractionStepsDto(interaction.Steps),
                stepIndex = interaction.StepIndex,
                selectedCharacterId = interaction.SelectedCharacterId ?? "",
                selectedCardKey = interaction.SelectedCardKey ?? "",
                fusionFirstCardKey = interaction.FusionFirstCardKey ?? "",
                fusionCardType = (int)interaction.FusionCardType,
                deferredOutcome = ToEventOutcomeDto(interaction.DeferredOutcome),
                pendingApplyKind = (int)interaction.PendingApplyKind,
                hasPendingCardAction = interaction.HasPendingCardAction,
                pendingPrimaryCardKey = interaction.PendingPrimaryCardKey ?? "",
                pendingSecondaryCardKey = interaction.PendingSecondaryCardKey ?? "",
                pendingUpgradeBonus = interaction.PendingUpgradeBonus
            };
        }

        static ExpeditionEventInteractionStepSaveData[] ToInteractionStepsDto(
            IReadOnlyList<ExpeditionEventInteractionStep> steps)
        {
            if (steps == null || steps.Count == 0)
                return Array.Empty<ExpeditionEventInteractionStepSaveData>();

            var result = new ExpeditionEventInteractionStepSaveData[steps.Count];
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                result[i] = new ExpeditionEventInteractionStepSaveData
                {
                    kind = (int)step.Kind,
                    percentHpDelta = step.PercentHpDelta,
                    percentFromMaxHp = step.PercentFromMaxHp,
                    flatHpDelta = step.FlatHpDelta,
                    personalAttackBonus = step.PersonalAttackBonus,
                    message = step.Message ?? "",
                    targetCharacterId = step.TargetCharacterId ?? "",
                    requiredFusionType = (int)step.RequiredFusionType
                };
            }

            return result;
        }

        static ExpeditionEventOutcomeSaveData ToEventOutcomeDto(ExpeditionEventOutcome outcome)
        {
            if (outcome == null)
                return null;

            return new ExpeditionEventOutcomeSaveData
            {
                message = outcome.Message ?? "",
                startsCombat = outcome.StartsCombat,
                combatEncounterIndex = outcome.CombatEncounterIndex,
                advanceNode = outcome.AdvanceNode,
                pendingRewardPickup = ToRewardPickupDto(outcome.PendingRewardPickup),
                eventBattleKey = outcome.EventBattleKey ?? "",
                interactionSteps = ToInteractionStepsDto(outcome.InteractionSteps),
                deferredOutcome = ToEventOutcomeDto(outcome.DeferredOutcome)
            };
        }

        static ExpeditionEventInteractionState FromEventInteractionDto(
            ExpeditionEventInteractionSaveData dto,
            ExpeditionConfig config)
        {
            if (dto == null)
                return null;

            var interaction = new ExpeditionEventInteractionState
            {
                EventId = dto.eventId ?? "",
                ChoiceIndex = dto.choiceIndex,
                StepIndex = dto.stepIndex,
                SelectedCharacterId = dto.selectedCharacterId ?? "",
                SelectedCardKey = dto.selectedCardKey ?? "",
                FusionFirstCardKey = dto.fusionFirstCardKey ?? "",
                FusionCardType = (CardType)dto.fusionCardType,
                DeferredOutcome = FromEventOutcomeDto(dto.deferredOutcome, config),
                PendingApplyKind = (ExpeditionEventStepKind)dto.pendingApplyKind,
                HasPendingCardAction = dto.hasPendingCardAction,
                PendingPrimaryCardKey = dto.pendingPrimaryCardKey ?? "",
                PendingSecondaryCardKey = dto.pendingSecondaryCardKey ?? "",
                PendingUpgradeBonus = dto.pendingUpgradeBonus
            };

            FromInteractionStepsDto(dto.steps, interaction.Steps);
            return interaction;
        }

        static void FromInteractionStepsDto(
            ExpeditionEventInteractionStepSaveData[] dtos,
            List<ExpeditionEventInteractionStep> steps)
        {
            steps.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                if (dto == null)
                    continue;

                steps.Add(new ExpeditionEventInteractionStep
                {
                    Kind = (ExpeditionEventStepKind)dto.kind,
                    PercentHpDelta = dto.percentHpDelta,
                    PercentFromMaxHp = dto.percentFromMaxHp,
                    FlatHpDelta = dto.flatHpDelta,
                    PersonalAttackBonus = dto.personalAttackBonus,
                    Message = dto.message ?? "",
                    TargetCharacterId = dto.targetCharacterId ?? "",
                    RequiredFusionType = (CardType)dto.requiredFusionType
                });
            }
        }

        static ExpeditionEventOutcome FromEventOutcomeDto(ExpeditionEventOutcomeSaveData dto, ExpeditionConfig config)
        {
            if (dto == null)
                return null;

            var outcome = new ExpeditionEventOutcome
            {
                Message = dto.message ?? "",
                StartsCombat = dto.startsCombat,
                CombatEncounterIndex = dto.combatEncounterIndex,
                AdvanceNode = dto.advanceNode,
                PendingRewardPickup = FromRewardPickupDto(dto.pendingRewardPickup, config),
                EventBattleKey = dto.eventBattleKey ?? "",
                DeferredOutcome = FromEventOutcomeDto(dto.deferredOutcome, config)
            };

            FromInteractionStepsDto(dto.interactionSteps, outcome.InteractionSteps);
            return outcome;
        }

        static StringStringListPair[] ToRunStartCampDecksDto(Dictionary<string, List<string>> decks)
        {
            if (decks == null || decks.Count == 0)
                return Array.Empty<StringStringListPair>();

            var result = new StringStringListPair[decks.Count];
            var index = 0;
            foreach (var pair in decks)
            {
                result[index++] = new StringStringListPair
                {
                    key = pair.Key ?? "",
                    values = pair.Value?.ToArray() ?? Array.Empty<string>()
                };
            }

            return result;
        }

        static StringIntListPair[] ToExtractedCampIndicesDto(Dictionary<string, HashSet<int>> indices)
        {
            if (indices == null || indices.Count == 0)
                return Array.Empty<StringIntListPair>();

            var result = new StringIntListPair[indices.Count];
            var index = 0;
            foreach (var pair in indices)
            {
                result[index++] = new StringIntListPair
                {
                    key = pair.Key ?? "",
                    values = ToIntArray(pair.Value)
                };
            }

            return result;
        }

        static void FromRunStartCampDecksDto(StringStringListPair[] dtos, Dictionary<string, List<string>> decks)
        {
            decks.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                if (dto == null || string.IsNullOrEmpty(dto.key))
                    continue;

                var list = new List<string>();
                if (dto.values != null)
                    list.AddRange(dto.values);
                decks[dto.key] = list;
            }
        }

        static void FromExtractedCampIndicesDto(StringIntListPair[] dtos, Dictionary<string, HashSet<int>> indices)
        {
            indices.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                if (dto == null || string.IsNullOrEmpty(dto.key))
                    continue;

                var set = new HashSet<int>();
                if (dto.values != null)
                {
                    foreach (var value in dto.values)
                        set.Add(value);
                }

                indices[dto.key] = set;
            }
        }

        static SimplifiedCardRef ToCardRef(CardTemplate template)
        {
            if (template == null)
                return null;

            return new SimplifiedCardRef
            {
                definitionId = template.DefinitionId ?? "",
                deckInstanceId = template.DeckInstanceId ?? "",
                upgradeLevel = template.UpgradeLevel,
                ownerCharacterId = template.OwnerCharacterId ?? ""
            };
        }

        static SimplifiedCardRef[] ToCardRefs(IReadOnlyList<CardTemplate> cards)
        {
            if (cards == null || cards.Count == 0)
                return Array.Empty<SimplifiedCardRef>();

            var result = new SimplifiedCardRef[cards.Count];
            for (var i = 0; i < cards.Count; i++)
                result[i] = ToCardRef(cards[i]);
            return result;
        }

        static CardTemplate FromCardRef(SimplifiedCardRef dto, ExpeditionConfig config)
        {
            if (dto == null || string.IsNullOrEmpty(dto.definitionId))
                return null;

            CardTemplate template = null;
            var fromCatalog = false;
            if (config != null)
            {
                template = FindCardTemplate(config, dto.definitionId);
                fromCatalog = template != null;
            }

            if (!fromCatalog)
            {
                template = new CardTemplate
                {
                    DefinitionId = dto.definitionId,
                    OwnerCharacterId = dto.ownerCharacterId ?? ""
                };
            }
            else
            {
                template = ExpeditionBattleConfigBuilder.CloneTemplate(template);
            }

            template.DeckInstanceId = dto.deckInstanceId ?? "";
            template.UpgradeLevel = dto.upgradeLevel;
            if (!string.IsNullOrEmpty(dto.ownerCharacterId))
                template.OwnerCharacterId = dto.ownerCharacterId;

            if (config != null)
                ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(template, config.PlayerCardCatalog);

            return template;
        }

        static void FromCardRefs(SimplifiedCardRef[] dtos, List<CardTemplate> cards, ExpeditionConfig config)
        {
            cards.Clear();
            if (dtos == null)
                return;

            foreach (var dto in dtos)
            {
                var card = FromCardRef(dto, config);
                if (card != null)
                    cards.Add(card);
            }
        }

        static CardTemplate FindCardTemplate(ExpeditionConfig config, string definitionId)
        {
            if (config == null || string.IsNullOrEmpty(definitionId))
                return null;

            foreach (var template in config.PlayerCardCatalog)
            {
                if (template?.DefinitionId == definitionId)
                    return template;
            }

            foreach (var encounter in config.CombatEncounters)
            {
                if (encounter?.Combatants == null)
                    continue;

                foreach (var cc in encounter.Combatants)
                {
                    if (cc?.DeckTemplates == null)
                        continue;

                    foreach (var template in cc.DeckTemplates)
                    {
                        if (template?.DefinitionId == definitionId)
                            return template;
                    }
                }
            }

            return null;
        }

        static StringIntPair[] ToStringIntPairs(Dictionary<string, int> dict)
        {
            if (dict == null || dict.Count == 0)
                return Array.Empty<StringIntPair>();

            var result = new StringIntPair[dict.Count];
            var index = 0;
            foreach (var pair in dict)
            {
                result[index++] = new StringIntPair
                {
                    key = pair.Key ?? "",
                    value = pair.Value
                };
            }

            return result;
        }

        static void FillIntDict(Dictionary<string, int> dict, StringIntPair[] pairs)
        {
            dict.Clear();
            if (pairs == null)
                return;

            foreach (var pair in pairs)
            {
                if (pair == null || string.IsNullOrEmpty(pair.key))
                    continue;

                dict[pair.key] = pair.value;
            }
        }

        static int[] ToIntArray(IEnumerable<int> values)
        {
            if (values == null)
                return Array.Empty<int>();

            if (values is ICollection<int> collection)
            {
                var array = new int[collection.Count];
                collection.CopyTo(array, 0);
                return array;
            }

            var list = new List<int>();
            foreach (var value in values)
                list.Add(value);
            return list.ToArray();
        }
    }
}
