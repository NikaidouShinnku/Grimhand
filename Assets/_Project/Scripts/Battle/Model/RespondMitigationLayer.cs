namespace Grimhand.Battle.Model
{
    public sealed class RespondMitigationLayer
    {
        public string TargetCombatantId { get; set; } = "";
        public int DamageReductionPercent { get; set; }
        public string ResponderCombatantId { get; set; } = "";
    }
}
