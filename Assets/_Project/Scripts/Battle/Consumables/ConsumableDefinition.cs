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
        TurnAttackBonusPercent,
        TurnDefenseBonusPercent,
        /// <summary>给目标立即获得固定护甲。</summary>
        GainBlock,
        EnergyThisTurn,
        DodgeAllThisTurn,
        MirrorLastAttack,
        /// <summary>抽取指定角色 N 张牌。</summary>
        DrawCharacterCards
    }

    public sealed class ConsumableDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public ConsumableTargetKind TargetKind { get; set; } = ConsumableTargetKind.None;
        public ConsumableEffectKind EffectKind { get; set; }
        public int Value { get; set; }
        /// <summary>仅特殊事件发放，不出现在宝箱/商店/战后掉落池。</summary>
        public bool EventOnly { get; set; }
    }
}
