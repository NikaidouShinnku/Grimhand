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
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string SpecialFlag { get; set; } = "";
        public int AtkBonus { get; set; }
        public int DefBonus { get; set; }
        public int HpBonus { get; set; }
        public string RequiredCharacterId { get; set; } = "";
        public bool EvolutionOnly { get; set; }
    }
}
