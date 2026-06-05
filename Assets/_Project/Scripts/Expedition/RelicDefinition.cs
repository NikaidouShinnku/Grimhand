namespace Grimhand.Expedition
{
    public enum RelicRarity
    {
        Common,
        Rare,
        Epic
    }

    public sealed class RelicDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public RelicRarity Rarity { get; set; }
        public string Description { get; set; } = "";
    }
}
