namespace Grimhand.Expedition.Model
{
    public sealed class CardPackRewardEntry
    {
        public string PackId { get; set; } = "";
        public bool Claimed { get; set; }
        public bool Skipped { get; set; }

        public bool IsResolved => Claimed || Skipped;
    }
}
