using System.Collections.Generic;

namespace Grimhand.Battle.Model
{
    public sealed class CombatantState
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public TeamSide Team { get; set; }
        public FormationSlot Slot { get; set; }
        public string CharacterDefinitionId { get; set; } = "";

        public int Level { get; set; } = 1;
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int BaseAttack { get; set; }
        public int BaseDefense { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public int Block { get; set; }

        public List<StatusInstance> Statuses { get; } = new();

        /// <summary>出牌后武装，等待下一次受到攻击时消耗。</summary>
        public ParryStance ActiveParry { get; set; }

        public bool IsAlive => Hp > 0;
    }
}
