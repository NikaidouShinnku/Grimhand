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

        /// <summary>造成伤害后，按实际伤害回复生命的百分比。</summary>
        public int LifestealPercent { get; set; }

        /// <summary>按目标 MaxHP 百分比治疗（忽略 Value 与攻击缩放）。</summary>
        public int HealMaxHpPercent { get; set; }

        /// <summary>击杀目标后，施法者回复的 HP。</summary>
        public int OnKillHealAmount { get; set; }
    }
}
