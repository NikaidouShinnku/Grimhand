using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    public sealed class CombatantConfig
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public TeamSide Team { get; set; }
        public FormationSlot Slot { get; set; }
        public string CharacterDefinitionId { get; set; } = "";
        public int Level { get; set; } = 1;
        public int MaxHp { get; set; }
        public int BaseAttack { get; set; }
        public int BaseDefense { get; set; }
        public int Speed { get; set; }
        /// <summary>不为 null 时作为开战 HP（远征跨场继承）；仍受 MaxHp 上限约束。</summary>
        public int? StartHp { get; set; }
        public int Xp { get; set; }
        public List<CardTemplate> DeckTemplates { get; } = new();
        public List<CardTemplate> SkillPoolCandidates { get; } = new();
        public bool UseRandomSkillPool { get; set; }
        public int RandomDeckSize { get; set; } = 8;
        public int RandomSkillPickMin { get; set; } = 2;
        public int RandomSkillPickMax { get; set; } = 4;
    }

    public sealed class BattleConfig
    {
        public int Seed { get; set; } = 1;
        public int EnergyCap { get; set; } = 8;
        public int TurnStartEnergyRegen { get; set; } = 3;
        public int HandLimit { get; set; } = 8;
        public int CardsDrawnPerTurn { get; set; } = 5;
        public List<CombatantConfig> Combatants { get; } = new();
        public Dictionary<string, CardTemplate> CardCatalog { get; } = new();
        public RunModifierSnapshot RunModifiers { get; set; } = RunModifierSnapshot.Empty;
    }
}
