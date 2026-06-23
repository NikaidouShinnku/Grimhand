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
        DrawCards,
        ReflectLastDamageToAttacker,
        GainBlockFromLastDamagePercent,
        /// <summary>阿努比斯化身：本场 +50% 生命上限/攻击/防御，并禁出牌 2 回合。</summary>
        ApplyAnubisAvatar,
        /// <summary>使随机一名玩家本回合后续出牌被跳过。</summary>
        LockRandomPlayerPlaysThisTurn,
        /// <summary>下回合玩家能量回复减少 Value 点。</summary>
        ReducePlayerEnergyRegenNextTurn,
        /// <summary>敌方应对：下次受到玩家攻击时将伤害×2并转嫁给随机队友。</summary>
        ArmRespondDamageRedirect,
        /// <summary>有空位则召唤 SummonCharacterId，否则获得 DEF 缩放护甲。</summary>
        SummonOrGainBlock,
        /// <summary>获得闪避率（写入 DodgeChanceBonus，持续若干回合）。</summary>
        GrantDodgeChance
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
        AllyBackSlot,
        /// <summary>结算开始时快照的所有存活敌人（全体攻击）。</summary>
        AllEnemies,
        /// <summary>随机一名敌方（自爆等）。</summary>
        RandomEnemy,
        /// <summary>随机 N 名敌方；人数由 EffectActionSpec.Value 指定（如骨王怒吼 Value=2）。</summary>
        RandomEnemies
    }

    public enum ReactionConditionType
    {
        None,
        LastActionAttackOnSelf,
        /// <summary>玩家监视的目标敌人打出防御牌（应对防御/应对状态）。</summary>
        LastActionDefenseOnTarget,
        LastActionStatusOnTarget
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
        BackOnly = 2,
        /// <summary>中排与后排。</summary>
        MiddleAndBack = 3
    }
}
