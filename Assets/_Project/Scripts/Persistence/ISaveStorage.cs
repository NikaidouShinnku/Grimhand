namespace Grimhand.Persistence
{
    public interface ISaveStorage
    {
        bool ExistsPrimary();
        bool TryReadPrimary(out string json);
        bool TryReadBackup(out string json);
        bool TryWrite(string json, out string error);
        void ArchiveCorrupt(string json, string reason);
        string PrimaryPath { get; }
        string SaveDirectory { get; }
    }
}
