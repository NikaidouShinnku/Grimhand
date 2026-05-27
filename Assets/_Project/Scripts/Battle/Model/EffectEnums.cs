namespace Grimhand.Battle.Model
{
    public enum StatusDurationKind
    {
        Permanent,
        Turns
    }

    public enum EffectActionType
    {
        DealDamage,
        GainBlock,
        Heal,
        ApplyStatus,
        RemoveStatus,
        SwapPositionWithFrontAlly,
        DrawCardsNextTurn,
        ReflectLastDamageToAttacker,
        GainBlockFromLastDamagePercent
    }

    public enum EffectTarget
    {
        DefaultEnemy,
        Self,
        FrontAlly,
        BackAlly,
        LastActionActor,
        ManualSelected,
        EnemyFrontSlot,
        EnemyMiddleSlot,
        EnemyBackSlot,
        AllyFrontSlot,
        AllyMiddleSlot,
        AllyBackSlot
    }

    public enum ReactionConditionType
    {
        None,
        LastActionAttackOnSelf
    }

    /// <summary>
    /// 手动选敌时，允许攻击的敌方站位范围。
    /// </summary>
    public enum TargetReach
    {
        /// <summary>前、中、后排均可（如狙击、远射）。</summary>
        Any = 0,
        /// <summary>仅前排与中排（默认近战/普通射击）。</summary>
        FrontAndMiddle = 1,
        /// <summary>仅后排（特殊卡）。</summary>
        BackOnly = 2
    }
}
