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
        public ReactionConditionType Condition = ReactionConditionType.None;
        public TargetReach Reach = TargetReach.FrontAndMiddle;
        public bool SplashBehindTarget;
        public int SplashPowerPercent = 100;
        public int BackRowPowerPercent = 100;

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
                Condition = Condition,
                Reach = Reach,
                SplashBehindTarget = SplashBehindTarget,
                SplashPowerPercent = SplashPowerPercent,
                BackRowPowerPercent = BackRowPowerPercent
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
                Condition = spec.Condition,
                Reach = spec.Reach,
                SplashBehindTarget = spec.SplashBehindTarget,
                SplashPowerPercent = spec.SplashPowerPercent,
                BackRowPowerPercent = spec.BackRowPowerPercent
            };
        }
    }
}
