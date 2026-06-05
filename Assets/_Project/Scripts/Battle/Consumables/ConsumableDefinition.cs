namespace Grimhand.Battle.Consumables
{
    public enum ConsumableTargetKind
    {
        None,
        SingleAlly,
        SingleEnemy,
        MirrorAttack
    }

    public enum ConsumableEffectKind
    {
        HealSingle,
        HealTeam,
        BattleAttackBonus,
        BattleDefenseBonus,
        EnergyThisTurn,
        DodgeAllThisTurn,
        MirrorLastAttack
    }

    public sealed class ConsumableDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public ConsumableTargetKind TargetKind { get; set; } = ConsumableTargetKind.None;
        public ConsumableEffectKind EffectKind { get; set; }
        public int Value { get; set; }
    }
}
