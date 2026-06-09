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
        public int LifestealPercent;
        public int HealMaxHpPercent;
        public int OnKillHealAmount;

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
                LifestealPercent = LifestealPercent,
                HealMaxHpPercent = HealMaxHpPercent,
                OnKillHealAmount = OnKillHealAmount
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
                LifestealPercent = spec.LifestealPercent,
                HealMaxHpPercent = spec.HealMaxHpPercent,
                OnKillHealAmount = spec.OnKillHealAmount
            };
        }
    }
}
