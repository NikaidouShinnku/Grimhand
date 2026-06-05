namespace Grimhand.Battle.Model
{
    public sealed class StatusDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public StatusDurationKind DurationKind { get; set; }
        public int DefaultDuration { get; set; } = 2;
        public int SpeedModifierPerStack { get; set; }
        public int AttackModifierPerStack { get; set; }
        public int DefenseModifierPerStack { get; set; }
        public int TurnStartDamagePerStack { get; set; }
    }
}
