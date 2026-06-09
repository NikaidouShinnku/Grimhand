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
        public bool IsSacrificeDamage { get; set; }
        public bool IsAoEWave { get; set; }
        public CardType CardType { get; set; }
        public BattleOutcome Outcome { get; set; }
        public bool DeferPresentation { get; set; }
    }
}
