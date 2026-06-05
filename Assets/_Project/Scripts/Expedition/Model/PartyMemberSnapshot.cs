using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Model
{
    public sealed class PartyMemberSnapshot
    {
        public string CharacterDefinitionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Level { get; set; } = 1;
        public int Xp { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public List<CardTemplate> BonusCards { get; } = new();
    }
}
