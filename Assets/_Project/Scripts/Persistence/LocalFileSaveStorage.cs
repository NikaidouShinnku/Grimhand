using System;
using System.IO;

namespace Grimhand.Persistence
{
    public sealed class LocalFileSaveStorage : ISaveStorage
    {
        public const string ProfileFileName = "profile.json";
        public const string BackupFileName = "profile.bak";
        public const string TempFileName = "profile.tmp";

        readonly string _saveDirectory;

        public LocalFileSaveStorage(string saveDirectory)
        {
            _saveDirectory = saveDirectory ?? throw new ArgumentNullException(nameof(saveDirectory));
        }

        public string SaveDirectory => _saveDirectory;
        public string PrimaryPath => Path.Combine(_saveDirectory, ProfileFileName);

        string BackupPath => Path.Combine(_saveDirectory, BackupFileName);
        string TempPath => Path.Combine(_saveDirectory, TempFileName);
        string CorruptDirectory => Path.Combine(_saveDirectory, "corrupt");

        public bool ExistsPrimary() => File.Exists(PrimaryPath);

        public bool TryReadPrimary(out string json) => TryReadFile(PrimaryPath, out json);

        public bool TryReadBackup(out string json) => TryReadFile(BackupPath, out json);

        public bool TryWrite(string json, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(json))
            {
                error = "写入内容为空。";
                return false;
            }

            try
            {
                Directory.CreateDirectory(_saveDirectory);

                File.WriteAllText(TempPath, json);
                FlushToDisk(TempPath);

                if (File.Exists(PrimaryPath))
                {
                    File.Copy(PrimaryPath, BackupPath, overwrite: true);
                    FlushToDisk(BackupPath);
                }

                if (File.Exists(PrimaryPath))
                    File.Delete(PrimaryPath);

                File.Move(TempPath, PrimaryPath);
                FlushToDisk(PrimaryPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void ArchiveCorrupt(string json, string reason)
        {
            try
            {
                Directory.CreateDirectory(CorruptDirectory);
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var safeReason = SanitizeFileToken(reason);
                var path = Path.Combine(CorruptDirectory, $"profile_{stamp}_{safeReason}.json");
                File.WriteAllText(path, json ?? "");
            }
            catch
            {
                // 归档失败不阻断启动。
            }
        }

        static bool TryReadFile(string path, out string json)
        {
            json = "";
            if (!File.Exists(path))
                return false;

            try
            {
                json = File.ReadAllText(path);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch
            {
                return false;
            }
        }

        static void FlushToDisk(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }

        static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';
            }

            var token = new string(chars);
            return token.Length > 32 ? token.Substring(0, 32) : token;
        }
    }
}
