using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Persistence
{
    /// <summary>运行时玩家档案（局外 Meta；ActiveRun 留 P2）。</summary>
    public sealed class PlayerProfileState : IExpeditionMetaProfile
    {
        public const int CurrentSaveVersion = 4;

        public CampMetaState Meta { get; set; } = new();
        public CampRosterState Roster { get; set; } = new();
        public CampCollectionState Collection { get; set; } = new();
        public CodexProgressState Codex { get; set; } = new();
        public int AccountGold { get; set; }
        public int CollectionCapacity { get; set; } = CampCollectionState.DefaultCapacity;
        public ActiveRunSnapshot ActiveRun { get; set; }

        public bool HasActiveRun => ActiveRun != null && ActiveRun.HasRun;
    }
}
