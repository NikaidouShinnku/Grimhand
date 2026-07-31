using System;

namespace Grimhand.Persistence
{
    [Serializable]
    public sealed class ExpeditionRunSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int phase;
        public int battlesWon;
        public int targetBattleCount;
        public int gold;
        public int lastGoldReward;
        public int lastXpReward;
        public int totalGoldGained;
        public int totalXpGained;
        public int sharedXpPool;
        public bool lastBattleWasElite;
        public bool lastBattleWasBoss;
        public int lastBattleFloor;
        public bool hasEventResolutionFixedRoll100;
        public int eventResolutionFixedRoll100;
        public string lastEventMessage = "";
        public string pendingConsumableOfferId = "";
        public int miracleLeafUsesRemaining = -1;
        public int currentBattleSeed;
        public string currentBossDisplayName = "";
        public string pendingEventBattleKey = "";
        public int pendingEventBattleBonusXp;
        public string pendingTravelerGiftRelicId = "";
        public string pendingTravelerGiftCurseOwnerId = "";
        public int v09EtherealEntryCount;
        public int v09ExpeditionRespondSuccessCount;
        public int v09SandSpearExhaustCardsPlayed;
        public bool chestRewardRevealed;

        public PartyMemberSaveData[] party = Array.Empty<PartyMemberSaveData>();
        public string[] relics = Array.Empty<string>();
        public StringIntPair[] relicGrowthTiers = Array.Empty<StringIntPair>();
        public string[] usedEventIds = Array.Empty<string>();
        public string[] eventFlags = Array.Empty<string>();
        public string[] consumableSlots = Array.Empty<string>();
        public string[] runAcquisitionLog = Array.Empty<string>();
        public ExpeditionRunModifiersSaveData modifiers = new();
        public ExpeditionMapSaveData map;
        public ExpeditionRouteOptionSaveData[] pendingRoutes = Array.Empty<ExpeditionRouteOptionSaveData>();
        public ExpeditionRewardPickupSaveData pendingRewardPickup;
        public ExpeditionPendingEventSaveData pendingEvent;
        public ExpeditionPendingEventAftermathSaveData pendingEventAftermath;
        public ExpeditionEventInteractionSaveData eventInteraction;
        public ExpeditionRewardPickupSaveData pendingEventBattleVictoryReward;
        public ExpeditionRewardPickupSaveData pendingDeferredReward;
        public ExpeditionPendingShrineSaveData pendingShrine;
        public ExpeditionShopSaveData shop = new();
        public ExpeditionTalentRunSaveData talentRun = new();
        public SimplifiedCardRef[] runWideBonusCards = Array.Empty<SimplifiedCardRef>();
        public ExpeditionCardAltarSaveData cardAltar;
        public string[] engravedDeckInstanceIds = Array.Empty<string>();
        public string[] altarExtractedDeckInstanceIds = Array.Empty<string>();
        public PendingCardEngravingSaveData[] pendingCardEngravings = Array.Empty<PendingCardEngravingSaveData>();
        public ExpeditionPendingCardOfferSaveData pendingCardOffer;
        public ExpeditionPendingCardPackOfferSaveData pendingCardPackOffer;
        public StringStringListPair[] runStartCampDecks = Array.Empty<StringStringListPair>();
        public StringIntListPair[] extractedCampCollectionIndices = Array.Empty<StringIntListPair>();
    }

    [Serializable]
    public sealed class StringIntPair
    {
        public string key = "";
        public int value;
    }

    [Serializable]
    public sealed class StringStringListPair
    {
        public string key = "";
        public string[] values = Array.Empty<string>();
    }

    [Serializable]
    public sealed class StringIntListPair
    {
        public string key = "";
        public int[] values = Array.Empty<int>();
    }

    [Serializable]
    public sealed class SimplifiedCardRef
    {
        public string definitionId = "";
        public string deckInstanceId = "";
        public int upgradeLevel;
        public string ownerCharacterId = "";
    }

    [Serializable]
    public sealed class PartyMemberSaveData
    {
        public string characterDefinitionId = "";
        public string displayName = "";
        public int level = 1;
        public int xp;
        public int hp;
        public int maxHp;
        public int maxHpPenalty;
        public int altarMaxHpBonus;
        public int personalAttackBonus;
        public int altarSpeedUpgrades;
        public int personalSpeedBonus;
        public bool usesCampDeckAsBattleBase;
        public string selectedTalentSlot1Id = "";
        public string selectedTalentSlot2Id = "";
        public StringIntPair[] removedCardCounts = Array.Empty<StringIntPair>();
        public StringIntPair[] cardUpgradeLevels = Array.Empty<StringIntPair>();
        public string[] baseDeckInstanceIds = Array.Empty<string>();
        public StringIntPair[] cardPowerBonusPercent = Array.Empty<StringIntPair>();
        public StringIntPair[] cardFlatDamageBonuses = Array.Empty<StringIntPair>();
        public SimplifiedCardRef[] bonusCards = Array.Empty<SimplifiedCardRef>();
        public string[] campDeckCardIds = Array.Empty<string>();
        public int[] extractedCampCardIndices = Array.Empty<int>();
    }

    [Serializable]
    public sealed class ExpeditionRunModifiersSaveData
    {
        public int teamAttackBonus;
        public int teamDefenseBonus;
        public float teamBlockGainBonusPercent;
        public int energyCapBonus;
        public int handLimitBonus;
        public int drawPerTurnBonus;
        public int altarHpPlus5Purchases;
        public int altarHpPlus10Purchases;
        public bool nextCombatEnemyAttackBonus;
        public int foreseenLayerCount;
        public bool skipNextRouteSelect;
        public bool lootedInjuredAdventurer;
        public bool divinePunishmentActive;
        public int soulRiftBattleStartRandomHpLoss;
    }

    [Serializable]
    public sealed class ExpeditionMapSaveData
    {
        public int chapterLayerCount;
        public int nodesCompleted;
        public ExpeditionMapLayerSaveData[] layers = Array.Empty<ExpeditionMapLayerSaveData>();
    }

    [Serializable]
    public sealed class ExpeditionMapLayerSaveData
    {
        public int layerNumber;
        public bool isBoss;
        public bool isRevealed;
        public bool hasChosenOptionIndex;
        public int chosenOptionIndex;
        public ExpeditionMapOptionSaveData[] options = Array.Empty<ExpeditionMapOptionSaveData>();
    }

    [Serializable]
    public sealed class ExpeditionMapOptionSaveData
    {
        public int nodeType;
        public string displayName = "";
        public string description = "";
        public int pathSpriteIndex;
        public int encounterIndex;
        public string monsterEncounterId = "";
        public string eventId = "";
        public string shrineId = "";
        public string treasureTier = "";
        public bool isElite;
    }

    [Serializable]
    public sealed class ExpeditionRouteOptionSaveData
    {
        public string id = "";
        public string displayName = "";
        public string description = "";
        public int nodeType;
        public int encounterIndex;
        public string monsterEncounterId = "";
        public string eventId = "";
        public string shrineId = "";
        public string treasureTier = "";
        public bool isElite;
        public int layerNumber;
        public int mapOptionIndex;
        public int pathSpriteIndex;
    }

    [Serializable]
    public sealed class ExpeditionRewardPickupSaveData
    {
        public string headerText = "";
        public int kind;
        public int gold;
        public bool goldClaimed;
        public bool goldSkipped;
        public string relicId = "";
        public bool relicClaimed;
        public bool relicSkipped;
        public string cardDefinitionId = "";
        public string cardOwnerCharacterId = "";
        public string cardDisplayName = "";
        public bool cardClaimed;
        public bool cardSkipped;
        public CardPackRewardEntrySaveData[] cardPacks = Array.Empty<CardPackRewardEntrySaveData>();
        public string consumableId = "";
        public int consumableCount = 1;
        public bool consumableClaimed;
        public bool consumableSkipped;
        public string relicEvolveFromId = "";
        public string relicEvolveToId = "";
        public string statCharacterId = "";
        public string statCharacterName = "";
        public int teamAttackBonus;
        public int teamDefenseBonus;
        public float teamBlockGainBonusPercent;
        public int energyCapBonus;
        public int personalAttackBonus;
        public int grantXp;
        public bool enableSoulRiftBattleStartRandomHpLoss;
        public bool enableDivinePunishment;
        public bool resolveStatCharacterFromInteraction;
        public bool statClaimed;
        public bool statSkipped;
    }

    [Serializable]
    public sealed class CardPackRewardEntrySaveData
    {
        public string packId = "";
        public bool claimed;
        public bool skipped;
    }

    [Serializable]
    public sealed class ExpeditionPendingEventSaveData
    {
        public string eventId = "";
        public int sourceLayer;
    }

    [Serializable]
    public sealed class ExpeditionPendingEventAftermathSaveData
    {
        public string eventId = "";
        public int choiceIndex;
        public int sourceLayer;
        public string afterChoiceText = "";
        public bool hasFixedRoll100;
        public int fixedRoll100;
    }

    [Serializable]
    public sealed class ExpeditionPendingShrineSaveData
    {
        public string shrineId = "";
        public int sourceLayer;
    }

    [Serializable]
    public sealed class ExpeditionShopSaveData
    {
        public ShopOfferSaveData[] offers = Array.Empty<ShopOfferSaveData>();
        public int refreshCount;
    }

    [Serializable]
    public sealed class ShopOfferSaveData
    {
        public int kind;
        public int price;
        public bool sold;
        public string cardPackId = "";
        public string consumableId = "";
        public string consumableDisplayName = "";
        public string relicId = "";
        public string relicDisplayName = "";
        public int relicRarity;
    }

    [Serializable]
    public sealed class ExpeditionTalentRunSaveData
    {
        public bool mageReviveUsed;
        public int rangerSacrificeHpTotal;
        public bool endlessBladeInjected;
        public bool snakeDetonateVenomInjected;
        public bool lichRealmSealInjected;
    }

    [Serializable]
    public sealed class ExpeditionCardAltarSaveData
    {
        public int sourceLayer;
        public bool engraveSlotUsed;
        public ExpeditionCardAltarMemberDraftSaveData[] drafts = Array.Empty<ExpeditionCardAltarMemberDraftSaveData>();
    }

    [Serializable]
    public sealed class ExpeditionCardAltarMemberDraftSaveData
    {
        public string memberId = "";
        public int collectionCardIndex = -1;
        public string replaceDeckCardKey = "";
        public bool confirmed;
    }

    [Serializable]
    public sealed class PendingCardEngravingSaveData
    {
        public string memberId = "";
        public string deckInstanceId = "";
        public string definitionId = "";
        public string displayName = "";
        public int battlesRequired;
        public int battlesCompleted;
    }

    [Serializable]
    public sealed class ExpeditionPendingCardOfferSaveData
    {
        public string ownerCharacterId = "";
        public SimplifiedCardRef template;
        public int context;
        public int sourceRewardPackIndex = -1;
        public int sourceShopSlotIndex = -1;
        public string sourcePackId = "";
    }

    [Serializable]
    public sealed class ExpeditionPendingCardPackOfferSaveData
    {
        public string packId = "";
        public int context;
        public int rewardPackIndex = -1;
        public int shopSlotIndex = -1;
        public CardPackChoiceSaveData[] choices = Array.Empty<CardPackChoiceSaveData>();
    }

    [Serializable]
    public sealed class CardPackChoiceSaveData
    {
        public string ownerCharacterId = "";
        public SimplifiedCardRef template;
    }

    [Serializable]
    public sealed class ExpeditionEventInteractionSaveData
    {
        public string eventId = "";
        public int choiceIndex;
        public ExpeditionEventInteractionStepSaveData[] steps = Array.Empty<ExpeditionEventInteractionStepSaveData>();
        public int stepIndex;
        public string selectedCharacterId = "";
        public string selectedCardKey = "";
        public string fusionFirstCardKey = "";
        public int fusionCardType;
        public ExpeditionEventOutcomeSaveData deferredOutcome;
        public int pendingApplyKind;
        public bool hasPendingCardAction;
        public string pendingPrimaryCardKey = "";
        public string pendingSecondaryCardKey = "";
        public int pendingUpgradeBonus;
    }

    [Serializable]
    public sealed class ExpeditionEventInteractionStepSaveData
    {
        public int kind;
        public int percentHpDelta;
        public bool percentFromMaxHp;
        public int flatHpDelta;
        public int personalAttackBonus;
        public string message = "";
        public string targetCharacterId = "";
        public int requiredFusionType;
    }

    [Serializable]
    public sealed class ExpeditionEventOutcomeSaveData
    {
        public string message = "";
        public bool startsCombat;
        public int combatEncounterIndex;
        public bool advanceNode = true;
        public ExpeditionRewardPickupSaveData pendingRewardPickup;
        public string eventBattleKey = "";
        public ExpeditionEventInteractionStepSaveData[] interactionSteps = Array.Empty<ExpeditionEventInteractionStepSaveData>();
        public ExpeditionEventOutcomeSaveData deferredOutcome;
    }
}
