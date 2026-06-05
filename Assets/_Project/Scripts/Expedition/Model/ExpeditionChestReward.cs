namespace Grimhand.Expedition.Model
{
    public sealed class ExpeditionChestReward
    {
        public int Gold { get; set; }
        public bool GoldClaimed { get; set; }
        public string RelicId { get; set; } = "";
        public bool RelicClaimed { get; set; }

        public bool HasRelic => !string.IsNullOrEmpty(RelicId);

        public bool IsFullyResolved =>
            GoldClaimed &&
            (!HasRelic || RelicClaimed);
    }
}
