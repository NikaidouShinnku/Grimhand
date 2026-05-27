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
}
