using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Model
{
    public sealed class CardInstanceState
    {
        public int InstanceId { get; set; }
        public string DefinitionId { get; set; } = "";
        public string OwnerCharacterId { get; set; } = "";
        public int Cost { get; set; }
        public CardType CardType { get; set; }
        public bool IsUsable { get; set; } = true;
        public string DisplayName { get; set; } = "";
        public List<string> Keywords { get; } = new();
        public List<EffectActionSpec> Actions { get; } = new();
    }
}
