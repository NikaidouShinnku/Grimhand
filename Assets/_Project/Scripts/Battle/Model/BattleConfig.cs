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
        /// <summary>远征战间 Hp=0，本场以 1 HP 复活进战（鼓舞等不应再叠当前 HP）。</summary>
        public bool EnteredFromExpeditionDeath { get; set; }
        public int Xp { get; set; }
        public List<CardTemplate> DeckTemplates { get; } = new();
        /// <summary>敌人技能池；开战时按池内条目（默认每种 1 张）加入团队混池。</summary>
        public List<CardTemplate> SkillPoolCandidates { get; } = new();
        public bool UseSkillPool { get; set; }
        public List<string> Traits { get; } = new();
    }

    public sealed class BattleConfig
    {
        public int Seed { get; set; } = 1;
        public int EnergyCap { get; set; } = 8;
        public int TurnStartEnergyRegen { get; set; } = 4;
        public int HandLimit { get; set; } = 8;
        public int CardsDrawnPerTurn { get; set; } = 5;
        /// <summary>敌方每回合抽牌数；0 表示与 <see cref="CardsDrawnPerTurn"/> 相同。</summary>
        public int EnemyCardsDrawnPerTurn { get; set; }
        /// <summary>敌方每回合出牌能量预算；0 表示与 <see cref="TurnStartEnergyRegen"/> 相同。</summary>
        public int EnemyTurnEnergyBudget { get; set; }
        /// <summary>为 true 时远征楼层缩放不作用于本场敌人（Boss 固定数值）。</summary>
        public bool SkipFloorScaling { get; set; }
        /// <summary>该角色定义 Id 的敌人死亡时立即判定玩家胜利（可仍有其他敌人存活）。</summary>
        public string VictoryOnCharacterDeathId { get; set; } = "";
        /// <summary>为 true 时敌方不自动规划意图，仅允许外部手动排队（训练场）。</summary>
        public bool ManualEnemyIntentsOnly { get; set; }
        public List<CombatantConfig> Combatants { get; } = new();
        public Dictionary<string, CombatantConfig> SummonTemplates { get; } = new();
        public Dictionary<string, CardTemplate> CardCatalog { get; } = new();
        public RunModifierSnapshot RunModifiers { get; set; } = RunModifierSnapshot.Empty;
        public int MiracleLeafRevivesRemaining { get; set; } = -1;
        public TalentBattleContext Talents { get; set; } = new();
    }
}
