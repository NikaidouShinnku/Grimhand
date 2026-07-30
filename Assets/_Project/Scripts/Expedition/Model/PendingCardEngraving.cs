namespace Grimhand.Expedition.Model
{
    /// <summary>祭坛刻印：战斗进度路径的待完成项。</summary>
    public sealed class PendingCardEngraving
    {
        public string MemberId { get; set; } = "";
        public string DeckInstanceId { get; set; } = "";
        public string DefinitionId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int BattlesRequired { get; set; }
        public int BattlesCompleted { get; set; }
        public int BattlesRemaining =>
            BattlesRequired > BattlesCompleted ? BattlesRequired - BattlesCompleted : 0;
    }
}
