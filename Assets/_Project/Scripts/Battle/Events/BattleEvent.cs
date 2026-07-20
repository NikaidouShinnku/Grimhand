using Grimhand.Battle.Model;

namespace Grimhand.Battle.Events
{
    public enum BattleEventKind
    {
        PhaseChanged,
        EnergyChanged,
        CardDrawn,
        CardDiscarded,
        DeckShuffled,
        DeckPolluted,
        TargetSelectionRequired,
        CardSelectedForPlay,
        CardDeselectedFromPlay,
        PlanCommitted,
        CardResolvedStarted,
        CardResolvedEnded,
        DamageApplied,
        BlockGained,
        /// <summary>战士铁壁转化：本应获得的护甲转为下一张攻击牌额外伤害，不增加 Block。</summary>
        IronWallConverted,
        HealApplied,
        CharacterRevived,
        CharacterDied,
        StatusApplied,
        StatusRemoved,
        StatusExpired,
        StatusTickDamage,
        PositionSwapped,
        ReactionTriggered,
        ParryTriggered,
        PortraitPoseChanged,
        PortraitIdleRestored,
        EnemyIntentPrepared,
        TurnSkipped,
        BattleEnded,
        ConsumableUsed,
        CombatantSpawned
    }

    public sealed class BattleEvent
    {
        public BattleEvent(BattleEventKind kind, string message = "")
        {
            Kind = kind;
            Message = message ?? "";
        }

        public BattleEventKind Kind { get; }
        public string Message { get; }
        public TurnPhase Phase { get; set; }
        public int Energy { get; set; }
        public int EnergyMax { get; set; }
        public int EnergyRemaining { get; set; }
        public int CardInstanceId { get; set; }
        public string CombatantId { get; set; } = "";
        public string TargetId { get; set; } = "";
        public int Amount { get; set; }
        public int BlockedAmount { get; set; }
        public int RespondMitigatedAmount { get; set; }
        public bool HadRespondDefense { get; set; }
        /// <summary>应对格挡演出目标（转嫁时为原防御者，与 TargetId 伤害落点可不同）。</summary>
        public string RespondBlockerId { get; set; } = "";
        public bool IsSacrificeDamage { get; set; }
        public bool IsAoEWave { get; set; }
        /// <summary>吸血回复（用于播放专属治疗特效）。</summary>
        public bool IsLifesteal { get; set; }
        public CardType CardType { get; set; }
        public BattleOutcome Outcome { get; set; }
        public bool DeferPresentation { get; set; }
        /// <summary>本回合演出批次内的事件序号，用于按动画进度还原展示属性。</summary>
        public int EventIndex { get; set; } = -1;
    }
}
