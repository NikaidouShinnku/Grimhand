namespace Grimhand.Battle.Model
{
    public sealed class EffectActionSpec
    {
        public EffectActionType Type { get; set; }
        public EffectTarget Target { get; set; } = EffectTarget.DefaultEnemy;
        public int Value { get; set; }
        public string StatusId { get; set; } = "";
        public int Stacks { get; set; } = 1;
        public int Duration { get; set; } = -1;
        public bool ScaleWithAttack { get; set; }
        public bool ScaleWithDefense { get; set; }

        /// <summary>与 ScaleWithAttack 配合：100 = ATK×1.0，80 = ATK×0.8。0 表示按 100 处理。</summary>
        public int AttackScalePercent { get; set; } = 100;

        /// <summary>与 ScaleWithDefense 配合：150 = DEF×1.5。0 表示按 100 处理。</summary>
        public int DefenseScalePercent { get; set; } = 100;

        public ReactionConditionType Condition { get; set; } = ReactionConditionType.None;

        /// <summary>选手动目标时的可攻击站位；默认前+中排。</summary>
        public TargetReach Reach { get; set; } = TargetReach.FrontAndMiddle;

        /// <summary>命中主目标后，对其后方（更深槽位）单位造成二次伤害。</summary>
        public bool SplashBehindTarget { get; set; }

        /// <summary>后方溅射伤害 = 主目标威力 × 此百分比 / 100。</summary>
        public int SplashPowerPercent { get; set; } = 100;

        /// <summary>当主目标在后排时，威力 × 此百分比 / 100（远射等）。100 表示无衰减。</summary>
        public int BackRowPowerPercent { get; set; } = 100;

        /// <summary>无视目标 DEF 的百分比（0-100）。</summary>
        public int IgnoreDefPercent { get; set; }

        /// <summary>目标 HP 低于此百分比时，额外加上 BonusIfTargetHpBelowFlat。</summary>
        public int BonusIfTargetHpBelowPercent { get; set; }

        public int BonusIfTargetHpBelowFlat { get; set; }

        /// <summary>目标本回合已被攻击时，额外增加基础威力的此百分比。</summary>
        public int BonusIfTargetHitThisTurnPercent { get; set; }

        /// <summary>目标拥有此状态时，额外加上 BonusIfTargetHasStatusFlat（如怨链投掷对减速目标 +6）。</summary>
        public string BonusIfTargetHasStatusId { get; set; } = "";

        public int BonusIfTargetHasStatusFlat { get; set; }

        /// <summary>造成伤害后，按实际伤害回复生命的百分比。</summary>
        public int LifestealPercent { get; set; }

        /// <summary>按目标 MaxHP 百分比治疗（忽略 Value 与攻击缩放）。</summary>
        public int HealMaxHpPercent { get; set; }

        /// <summary>击杀目标后，施法者回复的 HP。</summary>
        public int OnKillHealAmount { get; set; }

        /// <summary>同目标重复造成伤害的次数（如重拳打两次）。</summary>
        public int HitCount { get; set; } = 1;

        /// <summary>目标带负面状态时改用的 ATK 缩放百分比。</summary>
        public int AlternateAttackScalePercent { get; set; }

        /// <summary>目标带负面状态时改用的固定加值。</summary>
        public int AlternateValue { get; set; }

        /// <summary>为 true 时，目标有负面状态则改用 Alternate* 字段计算威力。</summary>
        public bool UseAlternateIfTargetHasDebuff { get; set; }

        /// <summary>本回合已出过攻击牌时改用的 ATK 缩放。</summary>
        public int AlternateAttackScaleIfActorUsedAttack { get; set; }

        /// <summary>本回合已出过攻击牌时改用的固定加值。</summary>
        public int AlternateValueIfActorUsedAttack { get; set; }

        /// <summary>处于应对武装状态时，最终威力 × 此百分比 / 100。</summary>
        public int DamageMultiplierPercentIfRespondArmed { get; set; } = 100;

        /// <summary>结算后对施法者造成固定自伤。</summary>
        public int SelfDamageFlat { get; set; }

        /// <summary>按本回合敌方已出的攻击牌次数，额外重复此伤害动作。</summary>
        public int RepeatPerEnemyAttackCardThisTurn { get; set; }

        /// <summary>召唤失败时 DEF 缩放护甲的百分比。</summary>
        public int FallbackBlockDefenseScalePercent { get; set; } = 100;

        /// <summary>召唤失败时的固定护甲加值。</summary>
        public int FallbackBlockValue { get; set; }

        /// <summary>SummonOrGainBlock 要召唤的角色定义 Id。</summary>
        public string SummonCharacterId { get; set; } = "";

        /// <summary>应对武装时额外赋予本回合无敌。</summary>
        public bool GrantInvulnerableOnRespondArm { get; set; }

        /// <summary>按实际 HP 伤害（非格挡部分）吸血。</summary>
        public bool LifestealUnblockedOnly { get; set; }

        // v0.9 新增字段
        /// <summary>怒火焚身：每损失此百分比最大HP，额外 +HpLossStepValue 伤害。</summary>
        public int HpLossStepPercent { get; set; }
        /// <summary>怒火焚身：每个HP损失步长的额外伤害值。</summary>
        public int HpLossStepValue { get; set; }
        /// <summary>鲜血撕咬：本回合回复过生命时改用的固定伤害值。</summary>
        public int AlternateValueIfHealed { get; set; }

        /// <summary>毒蛇/巫妖 v0.9：AddTokenCardToHand 要置入手牌的卡牌 DefinitionId。</summary>
        public string TokenCardId { get; set; } = "";
        /// <summary>召唤卡牌之灵：抽到的牌费用减免值（占位，待精修）。</summary>
        public int CostReduction { get; set; }

        public static EffectActionSpec Clone(EffectActionSpec source)
        {
            if (source == null)
                return null;

            return new EffectActionSpec
            {
                Type = source.Type,
                Target = source.Target,
                Value = source.Value,
                StatusId = source.StatusId,
                Stacks = source.Stacks,
                Duration = source.Duration,
                ScaleWithAttack = source.ScaleWithAttack,
                ScaleWithDefense = source.ScaleWithDefense,
                AttackScalePercent = source.AttackScalePercent,
                DefenseScalePercent = source.DefenseScalePercent,
                Condition = source.Condition,
                Reach = source.Reach,
                SplashBehindTarget = source.SplashBehindTarget,
                SplashPowerPercent = source.SplashPowerPercent,
                BackRowPowerPercent = source.BackRowPowerPercent,
                IgnoreDefPercent = source.IgnoreDefPercent,
                BonusIfTargetHpBelowPercent = source.BonusIfTargetHpBelowPercent,
                BonusIfTargetHpBelowFlat = source.BonusIfTargetHpBelowFlat,
                BonusIfTargetHitThisTurnPercent = source.BonusIfTargetHitThisTurnPercent,
                BonusIfTargetHasStatusId = source.BonusIfTargetHasStatusId,
                BonusIfTargetHasStatusFlat = source.BonusIfTargetHasStatusFlat,
                LifestealPercent = source.LifestealPercent,
                HealMaxHpPercent = source.HealMaxHpPercent,
                OnKillHealAmount = source.OnKillHealAmount,
                HitCount = source.HitCount,
                AlternateAttackScalePercent = source.AlternateAttackScalePercent,
                AlternateValue = source.AlternateValue,
                UseAlternateIfTargetHasDebuff = source.UseAlternateIfTargetHasDebuff,
                AlternateAttackScaleIfActorUsedAttack = source.AlternateAttackScaleIfActorUsedAttack,
                AlternateValueIfActorUsedAttack = source.AlternateValueIfActorUsedAttack,
                DamageMultiplierPercentIfRespondArmed = source.DamageMultiplierPercentIfRespondArmed,
                SelfDamageFlat = source.SelfDamageFlat,
                RepeatPerEnemyAttackCardThisTurn = source.RepeatPerEnemyAttackCardThisTurn,
                FallbackBlockDefenseScalePercent = source.FallbackBlockDefenseScalePercent,
                FallbackBlockValue = source.FallbackBlockValue,
                SummonCharacterId = source.SummonCharacterId,
                GrantInvulnerableOnRespondArm = source.GrantInvulnerableOnRespondArm,
                LifestealUnblockedOnly = source.LifestealUnblockedOnly,
                HpLossStepPercent = source.HpLossStepPercent,
                HpLossStepValue = source.HpLossStepValue,
                AlternateValueIfHealed = source.AlternateValueIfHealed,
                TokenCardId = source.TokenCardId,
                CostReduction = source.CostReduction
            };
        }
    }
}
