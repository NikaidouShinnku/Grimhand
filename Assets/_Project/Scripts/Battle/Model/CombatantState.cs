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
        public int Xp { get; set; }
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

        public bool FirstAttackBonusPending { get; set; } = true;
        public bool BossFirstHitBlockPending { get; set; } = true;
        public List<string> Traits { get; } = new();
        public bool FirstDefenseBonusPending { get; set; } = true;
        public bool FirstHitReductionPending { get; set; } = true;
        public bool WarriorFirstHitBlockPending { get; set; } = true;
        public bool UsedAttackThisTurn { get; set; }
        public bool UsedDefenseThisTurn { get; set; }
        /// <summary>消耗品等：本回合 ATK 额外 +N%（回合开始时清零）。</summary>
        public int TurnAttackBonusPercent { get; set; }
        /// <summary>消耗品等：本回合 DEF 额外 +N%（回合开始时清零）。</summary>
        public int TurnDefenseBonusPercent { get; set; }
        public int PendingRevengeAttackBonus { get; set; }
        public int InvulnerableTurnsRemaining { get; set; }
        public int SacrificeAttackStacks { get; set; }

        /// <summary>本回合是否已被攻击命中（用于致命打击等条件加伤）。</summary>
        public bool HitThisTurn { get; set; }

        /// <summary>剩余无法出牌回合数（阿努比斯化身等）。</summary>
        public int CardsLockedTurnsRemaining { get; set; }

        public bool IsAlive => Hp > 0;

        public bool IsCardsLocked => CardsLockedTurnsRemaining > 0;
    }
}
