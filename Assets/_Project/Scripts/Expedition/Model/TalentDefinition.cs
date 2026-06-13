namespace Grimhand.Expedition.Model
{
    /// <summary>单个天赋节点：随局外等级解锁，装入对应槽位候选池。</summary>
    public sealed class TalentDefinition
    {
        public string Id { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public int Slot { get; set; }
        public int UnlockLevel { get; set; }
        public string ShortTitle { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
