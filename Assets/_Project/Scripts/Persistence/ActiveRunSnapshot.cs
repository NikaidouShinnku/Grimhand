namespace Grimhand.Persistence
{
    /// <summary>进行中的远征快照（P2 checkpoint）。</summary>
    public sealed class ActiveRunSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public int MapStartLayer { get; set; } = 1;
        public int RunSeed { get; set; }
        public ulong RngState { get; set; } = 1;
        public int MetaGoldSyncedRunGold { get; set; }
        public string RunJson { get; set; } = "";

        public bool HasRun => !string.IsNullOrWhiteSpace(RunJson);
    }
}
