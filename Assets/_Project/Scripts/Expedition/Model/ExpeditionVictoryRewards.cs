namespace Grimhand.Expedition.Model
{
    public sealed class ExpeditionVictoryRewards
    {
        public int Gold { get; set; }
        public bool GoldClaimed { get; set; }
        public string RelicId { get; set; } = "";
        public bool RelicClaimed { get; set; }
        public string CardDefinitionId { get; set; } = "";
        public string CardOwnerCharacterId { get; set; } = "";
        public string CardDisplayName { get; set; } = "";
        public bool CardClaimed { get; set; }

        public bool HasRelic => !string.IsNullOrEmpty(RelicId);
        public bool HasCard => !string.IsNullOrEmpty(CardDefinitionId);

        public bool IsFullyResolved =>
            GoldClaimed &&
            (!HasRelic || RelicClaimed) &&
            (!HasCard || CardClaimed);
    }
}
