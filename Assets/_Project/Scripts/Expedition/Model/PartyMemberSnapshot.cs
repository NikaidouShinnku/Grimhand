namespace Grimhand.Expedition.Model
{
    public sealed class PartyMemberSnapshot
    {
        public string CharacterDefinitionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Level { get; set; } = 1;
        public int Hp { get; set; }
        public int MaxHp { get; set; }
    }
}
