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
        public ReactionConditionType Condition { get; set; } = ReactionConditionType.None;

        /// <summary>选手动目标时的可攻击站位；默认前+中排。</summary>
        public TargetReach Reach { get; set; } = TargetReach.FrontAndMiddle;

        /// <summary>命中主目标后，对其后方（更深槽位）单位造成二次伤害。</summary>
        public bool SplashBehindTarget { get; set; }

        /// <summary>后方溅射伤害 = 主目标威力 × 此百分比 / 100。</summary>
        public int SplashPowerPercent { get; set; } = 100;

        /// <summary>当主目标在后排时，威力 × 此百分比 / 100（远射等）。100 表示无衰减。</summary>
        public int BackRowPowerPercent { get; set; } = 100;
    }
}
