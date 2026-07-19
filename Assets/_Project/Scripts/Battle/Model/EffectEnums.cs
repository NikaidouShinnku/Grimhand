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
        GrantDodgeChance,
        // v0.9 新增动作类型 —— 集中放在末尾，保持与现有 case 兼容
        /// <summary>护盾猛击：消耗施法者所有护甲，造成 (护甲量 + Value) 伤害。</summary>
        ConsumeBlockDealDamage,
        /// <summary>见招拆招：应对攻击时 100% 免疫此次伤害，并对攻击者施加 Stacks 层减速。</summary>
        ParryImmuneAndSlowAttacker,
        /// <summary>战术大师的终结技：造成 (整场远征应对成功次数 × Value) 伤害。</summary>
        DamagePerRespondCount,
        /// <summary>诅咒加深：使目标身上所有中毒与灼烧层数翻倍。</summary>
        DoubleStatusStacks,
        /// <summary>神圣轮回：将消耗堆中所有已使用消耗牌洗回抽牌堆，并祛除其消耗关键词。</summary>
        RecycleExhaustCardsFromDiscard,
        /// <summary>怒火焚身：造成 Value 伤害，施法者每损失 HpLossStepPercent% 最大HP，额外 +HpLossStepValue 伤害。</summary>
        DealDamageScaledByActorHpLoss,
        /// <summary>鲜血撕咬：造成 Value 伤害；若本回合回复过生命，改用 AlternateValue（如 +100% 即翻倍值）。</summary>
        DealDamageAlternateIfHealedThisTurn,
        /// <summary>腐烂之触：造成 Value 伤害，目标每拥有1层负面状态额外 +Stacks 伤害。</summary>
        DealDamageBonusPerTargetDebuffStack,
        // ===== v0.9 毒蛇女王 / 巫妖女王 新增动作类型 =====
        /// <summary>获得 Value 点临时能量（可超过 EnergyMax，不改变上限）。</summary>
        GainEnergy,
        /// <summary>抽牌至手牌上限。</summary>
        DrawToHandLimit,
        /// <summary>鳞片硬化：获得 Value 护甲，自身有中毒时额外 +Stacks 护甲。</summary>
        GainBlockBonusIfSelfPoisoned,
        /// <summary>剧毒之触：造成 Value 伤害；若目标速度慢于施法者，施加 Stacks 层中毒 Duration 回合，否则施加 1 层。</summary>
        ApplyPoisonBySpeedCompare,
        /// <summary>蜕皮：清除自身所有中毒，每层治疗 Value HP。</summary>
        RemovePoisonHealPerStack,
        /// <summary>剧毒反哺：将自身一半中毒层数转移给随机敌人。</summary>
        TransferHalfPoisonToRandomEnemy,
        /// <summary>缠绕：目标每回合开始受 Value 伤害，持续 Duration 回合；施法者在此期间无法出牌。</summary>
        ApplyConstrict,
        /// <summary>引爆毒囊：目标身上所有中毒即时结算（层数×剩余回合，永久视为3）并清除。</summary>
        SettlePoisonAndClear,
        /// <summary>延迟伤害：目标下回合开始时受 Value 伤害。</summary>
        ApplyDelayedDamage,
        /// <summary>灵魂挽歌：AOE 造成 Value 伤害，本场远征每进入过一次虚化 +Stacks 伤害。</summary>
        EtherealCountBonusDamage,
        /// <summary>将 TokenCardId 指定的卡牌置入玩家手牌（不占抽牌）。</summary>
        AddTokenCardToHand,
        /// <summary>召唤混乱之灵：将手牌所有卡牌费用随机重排。</summary>
        ShuffleHandCosts,
        /// <summary>蛇神的回应：随机触发 AOE Value 伤害 / AOE 中毒 Stacks 层 / 随机敌人 AlternateValue 伤害。</summary>
        RandomSnakeGodEffect,
        /// <summary>灵界封印：使敌方使用的下一张卡失效（占位）。</summary>
        SealNextEnemyCard,
        /// <summary>锁定施法者出牌 Value 回合（虚化形态/祈求远古蛇神）。</summary>
        LockSelfCards,
        /// <summary>空洞凝视：抽 Value 张牌，自身虚化时改抽 AlternateValue 张。</summary>
        DrawCardsIfEthereal,
        /// <summary>灵魂强化：使其他我方角色获得 StatusId 状态 Stacks 层 Duration 回合。</summary>
        BuffAllOtherAllies,
        /// <summary>恐惧低语：看破敌人意图（占位：抽1牌）。</summary>
        RevealEnemyIntent,
        /// <summary>蛛网包裹等：下回合无法使用攻击牌。</summary>
        LockAttackCards,
        /// <summary>对随机一名指定 characterId 的友方造成伤害（如打开囚笼）。</summary>
        DealDamageRandomCharacterAlly,
        /// <summary>清除目标护甲后造成伤害；Stacks 为每清除 10 护甲的额外伤害。</summary>
        StripBlockThenDealDamage,
        /// <summary>随机交换 Value 名敌方角色的站位。</summary>
        SwapRandomEnemies,
        /// <summary>随机为自身 StatusId 增减 1 层（涨潮掌握）。</summary>
        AdjustSelfStatusRandom,
        /// <summary>按自身 RepeatPerStatusId 层数重复施加 AttackUpPercent。</summary>
        ApplyAttackUpPerSelfStatusStack,
        /// <summary>锁定涨潮为 Stacks 层并施加 TideLocked。</summary>
        LockRisingTideStacks,
        /// <summary>下回合开始时对目标施加 StatusId（Stacks/Duration）。</summary>
        ApplyStatusNextTurn,
        /// <summary>下回合开始时获得 Value 点临时能量（可超过 EnergyMax）。</summary>
        GainEnergyNextTurn
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
        RandomEnemies,
        /// <summary>随机一名存活友方（含自身）。</summary>
        RandomAlly,
        /// <summary>随机一名匹配 SummonCharacterId 的友方。</summary>
        RandomAllyByCharacterId
    }

    public enum ReactionConditionType
    {
        None,
        LastActionAttackOnSelf,
        /// <summary>玩家监视的目标敌人打出防御牌（应对防御/应对状态）。</summary>
        LastActionDefenseOnTarget,
        LastActionStatusOnTarget,
        /// <summary>应对卡未成功触发时（本回合无匹配攻击）生效。</summary>
        RespondArmFailed
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
