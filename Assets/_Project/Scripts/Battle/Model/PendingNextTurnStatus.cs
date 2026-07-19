namespace Grimhand.Battle.Model
{
    /// <summary>下回合开始时对指定角色施加的状态。</summary>
    public sealed class PendingNextTurnStatus
    {
        public string CombatantId { get; set; } = "";
        public string StatusId { get; set; } = "";
        public int Stacks { get; set; }
        public int Duration { get; set; }
        public string SourceLabel { get; set; } = "";
    }
}
