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
    }
}
