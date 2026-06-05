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

        public string CardDefinitionId { get; set; } = "";
        public string CardOwnerCharacterId { get; set; } = "";
        public string CardDisplayName { get; set; } = "";
        public bool CardClaimed { get; set; }
        public bool CardSkipped { get; set; }

        public string ConsumableId { get; set; } = "";
        public bool ConsumableClaimed { get; set; }
        public bool ConsumableSkipped { get; set; }

        public bool HasGold => Gold > 0;
        public bool HasRelic => !string.IsNullOrEmpty(RelicId);
        public bool HasCard => !string.IsNullOrEmpty(CardDefinitionId);
        public bool HasConsumable => !string.IsNullOrEmpty(ConsumableId);
        public bool HasAnyReward => HasGold || HasRelic || HasCard || HasConsumable;

        public bool IsGoldResolved => !HasGold || GoldClaimed || GoldSkipped;
        public bool IsRelicResolved => !HasRelic || RelicClaimed || RelicSkipped;
        public bool IsCardResolved => !HasCard || CardClaimed || CardSkipped;
        public bool IsConsumableResolved => !HasConsumable || ConsumableClaimed || ConsumableSkipped;

        public bool IsFullyResolved =>
            IsGoldResolved && IsRelicResolved && IsCardResolved && IsConsumableResolved;
    }
}
