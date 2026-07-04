using System.Collections.Generic;

namespace Grimhand.Expedition.Model
{
    public enum RewardPickupKind
    {
        BattleVictory,
        Chest,
        EventOrShrine
    }

    public sealed class ExpeditionRewardPickup
    {
        public string HeaderText { get; set; } = "拾取奖励";
        public RewardPickupKind Kind { get; set; } = RewardPickupKind.EventOrShrine;

        public int Gold { get; set; }
        public bool GoldClaimed { get; set; }
        public bool GoldSkipped { get; set; }

        public string RelicId { get; set; } = "";
        public bool RelicClaimed { get; set; }
        public bool RelicSkipped { get; set; }

        /// <summary>特殊事件等仍可直接发单卡；常规战斗/宝箱/商店走卡包。</summary>
        public string CardDefinitionId { get; set; } = "";
        public string CardOwnerCharacterId { get; set; } = "";
        public string CardDisplayName { get; set; } = "";
        public bool CardClaimed { get; set; }
        public bool CardSkipped { get; set; }

        public List<CardPackRewardEntry> CardPacks { get; } = new();

        public string ConsumableId { get; set; } = "";
        public int ConsumableCount { get; set; } = 1;
        public bool ConsumableClaimed { get; set; }
        public bool ConsumableSkipped { get; set; }

        public string RelicEvolveFromId { get; set; } = "";
        public string RelicEvolveToId { get; set; } = "";

        public string StatCharacterId { get; set; } = "";
        public string StatCharacterName { get; set; } = "";
        public int TeamAttackBonus { get; set; }
        public int TeamDefenseBonus { get; set; }
        public int EnergyCapBonus { get; set; }
        public int PersonalAttackBonus { get; set; }
        public int GrantXp { get; set; }
        public bool EnableSoulRiftBattleStartRandomHpLoss { get; set; }
        public bool EnableDivinePunishment { get; set; }
        public bool ResolveStatCharacterFromInteraction { get; set; }
        public bool StatClaimed { get; set; }
        public bool StatSkipped { get; set; }

        public bool HasGold => Gold > 0;
        public bool HasRelic =>
            !string.IsNullOrEmpty(RelicId) || HasRelicEvolution;
        public bool HasRelicEvolution =>
            !string.IsNullOrEmpty(RelicEvolveFromId) && !string.IsNullOrEmpty(RelicEvolveToId);
        public bool HasCard => !string.IsNullOrEmpty(CardDefinitionId);
        public bool HasCardPacks => CardPacks.Count > 0;
        public bool HasConsumable => !string.IsNullOrEmpty(ConsumableId);
        public bool HasStatBonus =>
            TeamAttackBonus != 0
            || TeamDefenseBonus != 0
            || EnergyCapBonus != 0
            || PersonalAttackBonus != 0
            || GrantXp > 0
            || EnableSoulRiftBattleStartRandomHpLoss
            || (EnableDivinePunishment && !HasGold);

        public bool HasAnyReward =>
            HasGold || HasRelic || HasCard || HasCardPacks || HasConsumable || HasStatBonus;

        public bool IsGoldResolved => !HasGold || GoldClaimed || GoldSkipped;
        public bool IsRelicResolved => !HasRelic || RelicClaimed || RelicSkipped;
        public bool IsCardResolved => !HasCard || CardClaimed || CardSkipped;
        public bool AreCardPacksResolved
        {
            get
            {
                if (!HasCardPacks)
                    return true;

                foreach (var pack in CardPacks)
                {
                    if (!pack.IsResolved)
                        return false;
                }

                return true;
            }
        }

        public bool IsConsumableResolved => !HasConsumable || ConsumableClaimed || ConsumableSkipped;
        public bool IsStatResolved => !HasStatBonus || StatClaimed || StatSkipped;

        public bool IsFullyResolved =>
            IsGoldResolved
            && IsRelicResolved
            && IsCardResolved
            && AreCardPacksResolved
            && IsConsumableResolved
            && IsStatResolved;
    }
}
