using System;
using Grimhand.Battle.Model;

namespace Grimhand.Content
{
    [Serializable]
    public class EffectActionDefinition
    {
        public EffectActionType Type;
        public EffectTarget Target = EffectTarget.DefaultEnemy;
        public int Value;
        public string StatusId = "";
        public int Stacks = 1;
        public int Duration = -1;
        public bool ScaleWithAttack;
        public bool ScaleWithDefense;
        public int AttackScalePercent = 100;
        public int DefenseScalePercent = 100;
        public ReactionConditionType Condition = ReactionConditionType.None;
        public TargetReach Reach = TargetReach.FrontAndMiddle;
        public bool SplashBehindTarget;
        public int SplashPowerPercent = 100;
        public int BackRowPowerPercent = 100;
        public int IgnoreDefPercent;
        public int BonusIfTargetHpBelowPercent;
        public int BonusIfTargetHpBelowFlat;
        public int BonusIfTargetHitThisTurnPercent;
        public string BonusIfTargetHasStatusId = "";
        public int BonusIfTargetHasStatusFlat;
        public int BonusIfActorFasterThanAllEnemiesFlat;
        public int LifestealPercent;
        public int HealMaxHpPercent;
        public int OnKillHealAmount;
        public int HitCount = 1;
        public int AlternateAttackScalePercent;
        public int AlternateValue;
        public bool UseAlternateIfTargetHasDebuff;
        public bool UseAlternateIfTargetHasAnyStatus;
        public int AlternateAttackScaleIfActorUsedAttack;
        public int AlternateValueIfActorUsedAttack;
        public int DamageMultiplierPercentIfRespondArmed = 100;
        public int SelfDamageFlat;
        public int RepeatPerEnemyAttackCardThisTurn;
        public int FallbackBlockDefenseScalePercent = 100;
        public int FallbackBlockValue;
        public string SummonCharacterId = "";
        public bool GrantInvulnerableOnRespondArm;
        public bool LifestealUnblockedOnly;
        // v0.9 新增字段
        /// <summary>怒火焚身：每损失此百分比最大HP，额外 +HpLossStepValue 伤害。</summary>
        public int HpLossStepPercent;
        /// <summary>怒火焚身：每个HP损失步长的额外伤害值。</summary>
        public int HpLossStepValue;
        /// <summary>鲜血撕咬：本回合回复过生命时改用的固定伤害值。</summary>
        public int AlternateValueIfHealed;
        /// <summary>毒蛇/巫妖 v0.9：AddTokenCardToHand 要置入手牌的卡牌 DefinitionId。</summary>
        public string TokenCardId = "";
        /// <summary>召唤卡牌之灵：抽到的牌费用减免值（占位）。</summary>
        public int CostReduction;
        /// <summary>施加状态成功概率（1-100）。≤0 视为 100%。</summary>
        public int ChancePercent;
        /// <summary>施法者本回合未受击时改用 AlternateValue。</summary>
        public bool UseAlternateIfActorNotHitThisTurn;
        /// <summary>自身护甲大于此阈值时改用 AlternateValueIfSelfBlockAbove；0 表示禁用。</summary>
        public int SelfBlockAboveThreshold;
        /// <summary>自身护甲高于阈值时的固定伤害。</summary>
        public int AlternateValueIfSelfBlockAbove;
        /// <summary>按 RepeatPerStatusId 层数重复执行。</summary>
        public string RepeatPerStatusId = "";
        /// <summary>应对触发时对随机匹配 CharacterId 的友方造成额外伤害。</summary>
        public int RespondSideEffectAllyDamage;
        public string RespondSideEffectAllyCharacterId = "";

        public EffectActionSpec ToSpec()
        {
            return new EffectActionSpec
            {
                Type = Type,
                Target = Target,
                Value = Value,
                StatusId = StatusId,
                Stacks = Stacks,
                Duration = Duration,
                ScaleWithAttack = ScaleWithAttack,
                ScaleWithDefense = ScaleWithDefense,
                AttackScalePercent = AttackScalePercent,
                DefenseScalePercent = DefenseScalePercent,
                Condition = Condition,
                Reach = Reach,
                SplashBehindTarget = SplashBehindTarget,
                SplashPowerPercent = SplashPowerPercent,
                BackRowPowerPercent = BackRowPowerPercent,
                IgnoreDefPercent = IgnoreDefPercent,
                BonusIfTargetHpBelowPercent = BonusIfTargetHpBelowPercent,
                BonusIfTargetHpBelowFlat = BonusIfTargetHpBelowFlat,
                BonusIfTargetHitThisTurnPercent = BonusIfTargetHitThisTurnPercent,
                BonusIfTargetHasStatusId = BonusIfTargetHasStatusId,
                BonusIfTargetHasStatusFlat = BonusIfTargetHasStatusFlat,
                BonusIfActorFasterThanAllEnemiesFlat = BonusIfActorFasterThanAllEnemiesFlat,
                LifestealPercent = LifestealPercent,
                HealMaxHpPercent = HealMaxHpPercent,
                OnKillHealAmount = OnKillHealAmount,
                HitCount = HitCount,
                AlternateAttackScalePercent = AlternateAttackScalePercent,
                AlternateValue = AlternateValue,
                UseAlternateIfTargetHasDebuff = UseAlternateIfTargetHasDebuff,
                UseAlternateIfTargetHasAnyStatus = UseAlternateIfTargetHasAnyStatus,
                AlternateAttackScaleIfActorUsedAttack = AlternateAttackScaleIfActorUsedAttack,
                AlternateValueIfActorUsedAttack = AlternateValueIfActorUsedAttack,
                DamageMultiplierPercentIfRespondArmed = DamageMultiplierPercentIfRespondArmed,
                SelfDamageFlat = SelfDamageFlat,
                RepeatPerEnemyAttackCardThisTurn = RepeatPerEnemyAttackCardThisTurn,
                FallbackBlockDefenseScalePercent = FallbackBlockDefenseScalePercent,
                FallbackBlockValue = FallbackBlockValue,
                SummonCharacterId = SummonCharacterId,
                GrantInvulnerableOnRespondArm = GrantInvulnerableOnRespondArm,
                LifestealUnblockedOnly = LifestealUnblockedOnly,
                HpLossStepPercent = HpLossStepPercent,
                HpLossStepValue = HpLossStepValue,
                AlternateValueIfHealed = AlternateValueIfHealed,
                TokenCardId = TokenCardId,
                CostReduction = CostReduction,
                ChancePercent = ChancePercent,
                UseAlternateIfActorNotHitThisTurn = UseAlternateIfActorNotHitThisTurn,
                SelfBlockAboveThreshold = SelfBlockAboveThreshold,
                AlternateValueIfSelfBlockAbove = AlternateValueIfSelfBlockAbove,
                RepeatPerStatusId = RepeatPerStatusId,
                RespondSideEffectAllyDamage = RespondSideEffectAllyDamage,
                RespondSideEffectAllyCharacterId = RespondSideEffectAllyCharacterId
            };
        }

        public static EffectActionDefinition FromSpec(EffectActionSpec spec)
        {
            return new EffectActionDefinition
            {
                Type = spec.Type,
                Target = spec.Target,
                Value = spec.Value,
                StatusId = spec.StatusId,
                Stacks = spec.Stacks,
                Duration = spec.Duration,
                ScaleWithAttack = spec.ScaleWithAttack,
                ScaleWithDefense = spec.ScaleWithDefense,
                AttackScalePercent = spec.AttackScalePercent,
                DefenseScalePercent = spec.DefenseScalePercent,
                Condition = spec.Condition,
                Reach = spec.Reach,
                SplashBehindTarget = spec.SplashBehindTarget,
                SplashPowerPercent = spec.SplashPowerPercent,
                BackRowPowerPercent = spec.BackRowPowerPercent,
                IgnoreDefPercent = spec.IgnoreDefPercent,
                BonusIfTargetHpBelowPercent = spec.BonusIfTargetHpBelowPercent,
                BonusIfTargetHpBelowFlat = spec.BonusIfTargetHpBelowFlat,
                BonusIfTargetHitThisTurnPercent = spec.BonusIfTargetHitThisTurnPercent,
                BonusIfTargetHasStatusId = spec.BonusIfTargetHasStatusId,
                BonusIfTargetHasStatusFlat = spec.BonusIfTargetHasStatusFlat,
                BonusIfActorFasterThanAllEnemiesFlat = spec.BonusIfActorFasterThanAllEnemiesFlat,
                LifestealPercent = spec.LifestealPercent,
                HealMaxHpPercent = spec.HealMaxHpPercent,
                OnKillHealAmount = spec.OnKillHealAmount,
                HitCount = spec.HitCount,
                AlternateAttackScalePercent = spec.AlternateAttackScalePercent,
                AlternateValue = spec.AlternateValue,
                UseAlternateIfTargetHasDebuff = spec.UseAlternateIfTargetHasDebuff,
                UseAlternateIfTargetHasAnyStatus = spec.UseAlternateIfTargetHasAnyStatus,
                AlternateAttackScaleIfActorUsedAttack = spec.AlternateAttackScaleIfActorUsedAttack,
                AlternateValueIfActorUsedAttack = spec.AlternateValueIfActorUsedAttack,
                DamageMultiplierPercentIfRespondArmed = spec.DamageMultiplierPercentIfRespondArmed,
                SelfDamageFlat = spec.SelfDamageFlat,
                RepeatPerEnemyAttackCardThisTurn = spec.RepeatPerEnemyAttackCardThisTurn,
                FallbackBlockDefenseScalePercent = spec.FallbackBlockDefenseScalePercent,
                FallbackBlockValue = spec.FallbackBlockValue,
                SummonCharacterId = spec.SummonCharacterId,
                GrantInvulnerableOnRespondArm = spec.GrantInvulnerableOnRespondArm,
                LifestealUnblockedOnly = spec.LifestealUnblockedOnly,
                HpLossStepPercent = spec.HpLossStepPercent,
                HpLossStepValue = spec.HpLossStepValue,
                AlternateValueIfHealed = spec.AlternateValueIfHealed,
                TokenCardId = spec.TokenCardId,
                CostReduction = spec.CostReduction,
                ChancePercent = spec.ChancePercent,
                UseAlternateIfActorNotHitThisTurn = spec.UseAlternateIfActorNotHitThisTurn,
                SelfBlockAboveThreshold = spec.SelfBlockAboveThreshold,
                AlternateValueIfSelfBlockAbove = spec.AlternateValueIfSelfBlockAbove,
                RepeatPerStatusId = spec.RepeatPerStatusId,
                RespondSideEffectAllyDamage = spec.RespondSideEffectAllyDamage,
                RespondSideEffectAllyCharacterId = spec.RespondSideEffectAllyCharacterId
            };
        }
    }
}
