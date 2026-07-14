using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;

namespace Grimhand.Expedition
{
    public static class ExpeditionRunStateCopy
    {
        public static ExpeditionRunState Clone(ExpeditionRunState source, ExpeditionConfig config = null)
        {
            if (source == null)
                return null;

            var clone = new ExpeditionRunState();
            CopyInto(source, clone, config);
            return clone;
        }

        public static void CopyInto(ExpeditionRunState source, ExpeditionRunState target, ExpeditionConfig config = null)
        {
            if (source == null || target == null)
                return;

            var hydrate = config != null;
            target.Phase = source.Phase;
            target.BattlesWon = source.BattlesWon;
            target.TargetBattleCount = source.TargetBattleCount;
            target.Gold = source.Gold;
            target.LastGoldReward = source.LastGoldReward;
            target.LastXpReward = source.LastXpReward;
            target.SharedXpPool = source.SharedXpPool;
            target.LastBattleWasElite = source.LastBattleWasElite;
            target.LastBattleWasBoss = source.LastBattleWasBoss;
            target.LastBattleFloor = source.LastBattleFloor;
            target.EventResolutionFixedRoll100 = source.EventResolutionFixedRoll100;
            target.LastEventMessage = source.LastEventMessage ?? "";
            target.PendingConsumableOfferId = source.PendingConsumableOfferId ?? "";
            target.MiracleLeafUsesRemaining = source.MiracleLeafUsesRemaining;
            target.CurrentBossDisplayName = source.CurrentBossDisplayName ?? "";
            target.PendingEventBattleKey = source.PendingEventBattleKey ?? "";
            target.PendingEventBattleBonusXp = source.PendingEventBattleBonusXp;
            target.PendingTravelerGiftRelicId = source.PendingTravelerGiftRelicId ?? "";
            target.PendingTravelerGiftCurseOwnerId = source.PendingTravelerGiftCurseOwnerId ?? "";
            target.V09EtherealEntryCount = source.V09EtherealEntryCount;
            target.V09ExpeditionRespondSuccessCount = source.V09ExpeditionRespondSuccessCount;
            target.V09SandSpearExhaustCardsPlayed = source.V09SandSpearExhaustCardsPlayed;
            target.ChestRewardRevealed = source.ChestRewardRevealed;
            target.CurrentBattleConfig = null;

            CopyParty(source.Party, target.Party, config, hydrate);
            CopyStringList(source.Relics, target.Relics);
            CopyIntDict(source.RelicGrowthTiers, target.RelicGrowthTiers);
            CopyStringHashSet(source.UsedEventIds, target.UsedEventIds);
            CopyStringHashSet(source.EventFlags, target.EventFlags);
            CopyStringList(source.ConsumableSlots, target.ConsumableSlots);
            CopyStringList(source.RunAcquisitionLog, target.RunAcquisitionLog);
            CopyModifiers(source.Modifiers, target.Modifiers);
            target.Map = CopyMap(source.Map);
            CopyRoutes(source.PendingRoutes, target.PendingRoutes);
            target.PendingRewardPickup = CopyRewardPickup(source.PendingRewardPickup);
            target.PendingEvent = CopyPendingEvent(source.PendingEvent);
            target.PendingEventAftermath = CopyPendingEventAftermath(source.PendingEventAftermath);
            target.EventInteraction = CopyEventInteraction(source.EventInteraction, config, hydrate);
            target.PendingEventBattleVictoryReward = CopyRewardPickup(source.PendingEventBattleVictoryReward);
            target.PendingDeferredReward = CopyRewardPickup(source.PendingDeferredReward);
            target.PendingShrine = CopyPendingShrine(source.PendingShrine);
            CopyShop(source.Shop, target.Shop);
            CopyTalentRun(source.TalentRun, target.TalentRun);
            CopyRunWideBonusCards(source.RunWideBonusCards, target.RunWideBonusCards, config, hydrate);
            target.CardAltar = CopyCardAltar(source.CardAltar);
            target.PendingCardOffer = CopyPendingCardOffer(source.PendingCardOffer, config, hydrate);
            target.PendingCardPackOffer = CopyPendingCardPackOffer(source.PendingCardPackOffer, config, hydrate);
            CopyRunStartCampDecks(source.RunStartCampDecks, target.RunStartCampDecks);
            CopyExtractedCampCollectionIndices(source.ExtractedCampCollectionIndices, target.ExtractedCampCollectionIndices);
        }

        static void CopyParty(
            IReadOnlyList<PartyMemberSnapshot> source,
            List<PartyMemberSnapshot> target,
            ExpeditionConfig config,
            bool hydrate)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var member in source)
            {
                if (member == null)
                    continue;

                var copy = new PartyMemberSnapshot
                {
                    CharacterDefinitionId = member.CharacterDefinitionId,
                    DisplayName = member.DisplayName,
                    Level = member.Level,
                    Xp = member.Xp,
                    Hp = member.Hp,
                    MaxHp = member.MaxHp,
                    MaxHpPenalty = member.MaxHpPenalty,
                    AltarMaxHpBonus = member.AltarMaxHpBonus,
                    PersonalAttackBonus = member.PersonalAttackBonus,
                    AltarSpeedUpgrades = member.AltarSpeedUpgrades,
                    PersonalSpeedBonus = member.PersonalSpeedBonus,
                    UsesCampDeckAsBattleBase = member.UsesCampDeckAsBattleBase,
                    SelectedTalentSlot1Id = member.SelectedTalentSlot1Id,
                    SelectedTalentSlot2Id = member.SelectedTalentSlot2Id
                };
                CopyIntDict(member.RemovedCardCounts, copy.RemovedCardCounts);
                CopyIntDict(member.CardUpgradeLevels, copy.CardUpgradeLevels);
                copy.BaseDeckInstanceIds.AddRange(member.BaseDeckInstanceIds);
                CopyIntDict(member.CardPowerBonusPercent, copy.CardPowerBonusPercent);
                CopyIntDict(member.CardFlatDamageBonuses, copy.CardFlatDamageBonuses);
                foreach (var card in member.BonusCards)
                {
                    if (card == null)
                        continue;

                    var cloned = ExpeditionBattleConfigBuilder.CloneTemplate(card);
                    if (hydrate)
                        ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(cloned, config.PlayerCardCatalog);
                    copy.BonusCards.Add(cloned);
                }

                copy.CampDeckCardIds.AddRange(member.CampDeckCardIds);
                foreach (var index in member.ExtractedCampCardIndices)
                    copy.ExtractedCampCardIndices.Add(index);
                target.Add(copy);
            }
        }

        static void CopyModifiers(ExpeditionRunModifiers source, ExpeditionRunModifiers target)
        {
            if (source == null || target == null)
                return;

            target.TeamAttackBonus = source.TeamAttackBonus;
            target.TeamDefenseBonus = source.TeamDefenseBonus;
            target.EnergyCapBonus = source.EnergyCapBonus;
            target.HandLimitBonus = source.HandLimitBonus;
            target.DrawPerTurnBonus = source.DrawPerTurnBonus;
            target.AltarHpPlus5Purchases = source.AltarHpPlus5Purchases;
            target.AltarHpPlus10Purchases = source.AltarHpPlus10Purchases;
            target.NextCombatEnemyAttackBonus = source.NextCombatEnemyAttackBonus;
            target.ForeseenLayerCount = source.ForeseenLayerCount;
            target.SkipNextRouteSelect = source.SkipNextRouteSelect;
            target.LootedInjuredAdventurer = source.LootedInjuredAdventurer;
            target.DivinePunishmentActive = source.DivinePunishmentActive;
            target.SoulRiftBattleStartRandomHpLoss = source.SoulRiftBattleStartRandomHpLoss;
        }

        static ExpeditionMapState CopyMap(ExpeditionMapState source)
        {
            if (source == null)
                return null;

            var map = new ExpeditionMapState
            {
                ChapterLayerCount = source.ChapterLayerCount,
                NodesCompleted = source.NodesCompleted
            };

            foreach (var layer in source.Layers)
            {
                if (layer == null)
                    continue;

                var copy = new ExpeditionMapLayer
                {
                    LayerNumber = layer.LayerNumber,
                    IsBoss = layer.IsBoss,
                    IsRevealed = layer.IsRevealed,
                    ChosenOptionIndex = layer.ChosenOptionIndex
                };
                foreach (var option in layer.Options)
                {
                    copy.Options.Add(new ExpeditionMapOption
                    {
                        NodeType = option.NodeType,
                        DisplayName = option.DisplayName,
                        Description = option.Description,
                        PathSpriteIndex = option.PathSpriteIndex,
                        EncounterIndex = option.EncounterIndex,
                        MonsterEncounterId = option.MonsterEncounterId,
                        EventId = option.EventId,
                        ShrineId = option.ShrineId,
                        TreasureTier = option.TreasureTier,
                        IsElite = option.IsElite
                    });
                }

                map.Layers.Add(copy);
            }

            return map;
        }

        static void CopyRoutes(IReadOnlyList<ExpeditionRouteOption> source, List<ExpeditionRouteOption> target)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var route in source)
            {
                if (route == null)
                    continue;

                target.Add(new ExpeditionRouteOption
                {
                    Id = route.Id,
                    DisplayName = route.DisplayName,
                    Description = route.Description,
                    NodeType = route.NodeType,
                    EncounterIndex = route.EncounterIndex,
                    MonsterEncounterId = route.MonsterEncounterId,
                    EventId = route.EventId,
                    ShrineId = route.ShrineId,
                    TreasureTier = route.TreasureTier,
                    IsElite = route.IsElite,
                    LayerNumber = route.LayerNumber,
                    MapOptionIndex = route.MapOptionIndex,
                    PathSpriteIndex = route.PathSpriteIndex
                });
            }
        }

        static ExpeditionRewardPickup CopyRewardPickup(ExpeditionRewardPickup source)
        {
            if (source == null)
                return null;

            var copy = new ExpeditionRewardPickup
            {
                HeaderText = source.HeaderText,
                Kind = source.Kind,
                Gold = source.Gold,
                GoldClaimed = source.GoldClaimed,
                GoldSkipped = source.GoldSkipped,
                RelicId = source.RelicId,
                RelicClaimed = source.RelicClaimed,
                RelicSkipped = source.RelicSkipped,
                CardDefinitionId = source.CardDefinitionId,
                CardOwnerCharacterId = source.CardOwnerCharacterId,
                CardDisplayName = source.CardDisplayName,
                CardClaimed = source.CardClaimed,
                CardSkipped = source.CardSkipped,
                ConsumableId = source.ConsumableId,
                ConsumableCount = source.ConsumableCount,
                ConsumableClaimed = source.ConsumableClaimed,
                ConsumableSkipped = source.ConsumableSkipped,
                RelicEvolveFromId = source.RelicEvolveFromId,
                RelicEvolveToId = source.RelicEvolveToId,
                StatCharacterId = source.StatCharacterId,
                StatCharacterName = source.StatCharacterName,
                TeamAttackBonus = source.TeamAttackBonus,
                TeamDefenseBonus = source.TeamDefenseBonus,
                EnergyCapBonus = source.EnergyCapBonus,
                PersonalAttackBonus = source.PersonalAttackBonus,
                GrantXp = source.GrantXp,
                EnableSoulRiftBattleStartRandomHpLoss = source.EnableSoulRiftBattleStartRandomHpLoss,
                EnableDivinePunishment = source.EnableDivinePunishment,
                ResolveStatCharacterFromInteraction = source.ResolveStatCharacterFromInteraction,
                StatClaimed = source.StatClaimed,
                StatSkipped = source.StatSkipped
            };

            foreach (var entry in source.CardPacks)
            {
                copy.CardPacks.Add(new CardPackRewardEntry
                {
                    PackId = entry.PackId,
                    Claimed = entry.Claimed,
                    Skipped = entry.Skipped
                });
            }

            return copy;
        }

        static ExpeditionPendingEvent CopyPendingEvent(ExpeditionPendingEvent source)
        {
            if (source == null)
                return null;

            return new ExpeditionPendingEvent
            {
                EventId = source.EventId,
                SourceLayer = source.SourceLayer
            };
        }

        static ExpeditionPendingEventAftermath CopyPendingEventAftermath(ExpeditionPendingEventAftermath source)
        {
            if (source == null)
                return null;

            return new ExpeditionPendingEventAftermath
            {
                EventId = source.EventId,
                ChoiceIndex = source.ChoiceIndex,
                SourceLayer = source.SourceLayer,
                AfterChoiceText = source.AfterChoiceText,
                FixedRoll100 = source.FixedRoll100
            };
        }

        static ExpeditionPendingShrine CopyPendingShrine(ExpeditionPendingShrine source)
        {
            if (source == null)
                return null;

            return new ExpeditionPendingShrine
            {
                ShrineId = source.ShrineId,
                SourceLayer = source.SourceLayer
            };
        }

        static void CopyShop(ExpeditionShopState source, ExpeditionShopState target)
        {
            target.Clear();
            if (source == null)
                return;

            target.RefreshCount = source.RefreshCount;
            foreach (var offer in source.Offers)
            {
                target.Offers.Add(new ShopOffer
                {
                    Kind = offer.Kind,
                    Price = offer.Price,
                    Sold = offer.Sold,
                    CardPackId = offer.CardPackId,
                    ConsumableId = offer.ConsumableId,
                    ConsumableDisplayName = offer.ConsumableDisplayName,
                    RelicId = offer.RelicId,
                    RelicDisplayName = offer.RelicDisplayName,
                    RelicRarity = offer.RelicRarity
                });
            }
        }

        static void CopyTalentRun(ExpeditionTalentRunState source, ExpeditionTalentRunState target)
        {
            if (source == null || target == null)
                return;

            target.MageReviveUsed = source.MageReviveUsed;
            target.RangerSacrificeHpTotal = source.RangerSacrificeHpTotal;
            target.EndlessBladeInjected = source.EndlessBladeInjected;
            target.SnakeDetonateVenomInjected = source.SnakeDetonateVenomInjected;
            target.LichRealmSealInjected = source.LichRealmSealInjected;
        }

        static ExpeditionCardAltarState CopyCardAltar(ExpeditionCardAltarState source)
        {
            if (source == null)
                return null;

            var copy = new ExpeditionCardAltarState { SourceLayer = source.SourceLayer };
            foreach (var pair in source.Drafts)
            {
                var draft = pair.Value;
                copy.Drafts[pair.Key] = new ExpeditionCardAltarMemberDraft
                {
                    CollectionCardIndex = draft.CollectionCardIndex,
                    ReplaceDeckCardKey = draft.ReplaceDeckCardKey,
                    Confirmed = draft.Confirmed
                };
            }

            return copy;
        }

        static ExpeditionPendingCardOffer CopyPendingCardOffer(
            ExpeditionPendingCardOffer source,
            ExpeditionConfig config,
            bool hydrate)
        {
            if (source == null)
                return null;

            CardTemplate template = null;
            if (source.Template != null)
            {
                template = ExpeditionBattleConfigBuilder.CloneTemplate(source.Template);
                if (hydrate)
                    ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(template, config.PlayerCardCatalog);
            }

            return new ExpeditionPendingCardOffer
            {
                OwnerCharacterId = source.OwnerCharacterId,
                Template = template,
                Context = source.Context,
                SourceRewardPackIndex = source.SourceRewardPackIndex,
                SourceShopSlotIndex = source.SourceShopSlotIndex,
                SourcePackId = source.SourcePackId
            };
        }

        static ExpeditionPendingCardPackOffer CopyPendingCardPackOffer(
            ExpeditionPendingCardPackOffer source,
            ExpeditionConfig config,
            bool hydrate)
        {
            if (source == null)
                return null;

            var copy = new ExpeditionPendingCardPackOffer
            {
                PackId = source.PackId,
                Context = source.Context,
                RewardPackIndex = source.RewardPackIndex,
                ShopSlotIndex = source.ShopSlotIndex
            };

            foreach (var choice in source.Choices)
            {
                if (choice == null)
                    continue;

                CardTemplate template = null;
                if (choice.Template != null)
                {
                    template = ExpeditionBattleConfigBuilder.CloneTemplate(choice.Template);
                    if (hydrate)
                        ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(template, config.PlayerCardCatalog);
                }

                copy.Choices.Add(new CardPackChoice
                {
                    OwnerCharacterId = choice.OwnerCharacterId,
                    Template = template
                });
            }

            return copy;
        }

        static ExpeditionEventInteractionState CopyEventInteraction(
            ExpeditionEventInteractionState source,
            ExpeditionConfig config,
            bool hydrate)
        {
            if (source == null)
                return null;

            var copy = new ExpeditionEventInteractionState
            {
                EventId = source.EventId,
                ChoiceIndex = source.ChoiceIndex,
                StepIndex = source.StepIndex,
                SelectedCharacterId = source.SelectedCharacterId,
                SelectedCardKey = source.SelectedCardKey,
                FusionFirstCardKey = source.FusionFirstCardKey,
                FusionCardType = source.FusionCardType,
                DeferredOutcome = CopyEventOutcome(source.DeferredOutcome, config, hydrate),
                PendingApplyKind = source.PendingApplyKind,
                HasPendingCardAction = source.HasPendingCardAction,
                PendingPrimaryCardKey = source.PendingPrimaryCardKey,
                PendingSecondaryCardKey = source.PendingSecondaryCardKey,
                PendingUpgradeBonus = source.PendingUpgradeBonus
            };

            foreach (var step in source.Steps)
            {
                copy.Steps.Add(new ExpeditionEventInteractionStep
                {
                    Kind = step.Kind,
                    PercentHpDelta = step.PercentHpDelta,
                    PercentFromMaxHp = step.PercentFromMaxHp,
                    FlatHpDelta = step.FlatHpDelta,
                    PersonalAttackBonus = step.PersonalAttackBonus,
                    Message = step.Message,
                    TargetCharacterId = step.TargetCharacterId,
                    RequiredFusionType = step.RequiredFusionType
                });
            }

            return copy;
        }

        static ExpeditionEventOutcome CopyEventOutcome(
            ExpeditionEventOutcome source,
            ExpeditionConfig config,
            bool hydrate)
        {
            if (source == null)
                return null;

            var copy = new ExpeditionEventOutcome
            {
                Message = source.Message,
                StartsCombat = source.StartsCombat,
                CombatEncounterIndex = source.CombatEncounterIndex,
                AdvanceNode = source.AdvanceNode,
                PendingRewardPickup = CopyRewardPickup(source.PendingRewardPickup),
                EventBattleKey = source.EventBattleKey,
                DeferredOutcome = CopyEventOutcome(source.DeferredOutcome, config, hydrate)
            };

            foreach (var step in source.InteractionSteps)
            {
                copy.InteractionSteps.Add(new ExpeditionEventInteractionStep
                {
                    Kind = step.Kind,
                    PercentHpDelta = step.PercentHpDelta,
                    PercentFromMaxHp = step.PercentFromMaxHp,
                    FlatHpDelta = step.FlatHpDelta,
                    PersonalAttackBonus = step.PersonalAttackBonus,
                    Message = step.Message,
                    TargetCharacterId = step.TargetCharacterId,
                    RequiredFusionType = step.RequiredFusionType
                });
            }

            return copy;
        }

        static void CopyRunWideBonusCards(
            IReadOnlyList<CardTemplate> source,
            List<CardTemplate> target,
            ExpeditionConfig config,
            bool hydrate)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var card in source)
            {
                if (card == null)
                    continue;

                var clone = ExpeditionBattleConfigBuilder.CloneTemplate(card);
                if (hydrate)
                    ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(clone, config.PlayerCardCatalog);
                target.Add(clone);
            }
        }

        static void CopyRunStartCampDecks(
            Dictionary<string, List<string>> source,
            Dictionary<string, List<string>> target)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var pair in source)
                target[pair.Key] = new List<string>(pair.Value);
        }

        static void CopyExtractedCampCollectionIndices(
            Dictionary<string, HashSet<int>> source,
            Dictionary<string, HashSet<int>> target)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var pair in source)
                target[pair.Key] = new HashSet<int>(pair.Value);
        }

        static void CopyIntDict(Dictionary<string, int> source, Dictionary<string, int> target)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var pair in source)
                target[pair.Key] = pair.Value;
        }

        static void CopyStringList(IReadOnlyList<string> source, List<string> target)
        {
            target.Clear();
            if (source == null)
                return;

            target.AddRange(source);
        }

        static void CopyStringHashSet(IEnumerable<string> source, HashSet<string> target)
        {
            target.Clear();
            if (source == null)
                return;

            foreach (var value in source)
                target.Add(value);
        }
    }
}
