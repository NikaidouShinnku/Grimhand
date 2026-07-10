namespace Grimhand.Persistence
{
    public enum SaveLoadSource
    {
        NewProfile,
        Primary,
        Backup,
        FallbackNew
    }

    public sealed class SaveLoadResult
    {
        public PlayerProfileState Profile { get; }
        public SaveLoadSource Source { get; }
        public string Message { get; }

        SaveLoadResult(PlayerProfileState profile, SaveLoadSource source, string message)
        {
            Profile = profile;
            Source = source;
            Message = message ?? "";
        }

        public static SaveLoadResult New(PlayerProfileState profile) =>
            new(profile, SaveLoadSource.NewProfile, "无存档，已创建新局。");

        public static SaveLoadResult FromPrimary(PlayerProfileState profile) =>
            new(profile, SaveLoadSource.Primary, "已从 profile.json 读档。");

        public static SaveLoadResult FromBackup(PlayerProfileState profile) =>
            new(profile, SaveLoadSource.Backup, "主档损坏，已从 profile.bak 恢复。");

        public static SaveLoadResult FallbackNew(PlayerProfileState profile, string reason) =>
            new(profile, SaveLoadSource.FallbackNew, reason ?? "读档失败，已创建新局。");
    }
}
