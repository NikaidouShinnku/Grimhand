using System;
using System.IO;
using UnityEngine;

namespace Grimhand.Persistence
{
    public sealed class LocalFileSaveStorage : ISaveStorage
    {
        /// <summary>Matches Steam Auto-Cloud pattern <c>*.sav</c>.</summary>
        public const string ProfileFileName = "profile.sav";
        public const string BackupFileName = "profile_bak.sav";
        public const string TempFileName = "profile.tmp";
        public const string LegacyProfileFileName = "profile.json";
        public const string LegacyBackupFileName = "profile.bak";
        public const string AutoCloudSubdirectory = "SavesDir";

        readonly string _saveDirectory;

        public LocalFileSaveStorage(string saveDirectory)
        {
            _saveDirectory = saveDirectory ?? throw new ArgumentNullException(nameof(saveDirectory));
        }

        public string SaveDirectory => _saveDirectory;
        public string PrimaryPath => Path.Combine(_saveDirectory, ProfileFileName);

        string BackupPath => Path.Combine(_saveDirectory, BackupFileName);
        string TempPath => Path.Combine(_saveDirectory, TempFileName);
        string LegacyPrimaryPath => Path.Combine(_saveDirectory, LegacyProfileFileName);
        string LegacyBackupPath => Path.Combine(_saveDirectory, LegacyBackupFileName);
        string CorruptDirectory => Path.Combine(_saveDirectory, "corrupt");

        public bool ExistsPrimary() =>
            File.Exists(PrimaryPath) || File.Exists(LegacyPrimaryPath);

        public bool TryReadPrimary(out string json) =>
            TryReadFile(PrimaryPath, out json) || TryReadFile(LegacyPrimaryPath, out json);

        public bool TryReadBackup(out string json) =>
            TryReadFile(BackupPath, out json) || TryReadFile(LegacyBackupPath, out json);

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
                else if (File.Exists(LegacyPrimaryPath))
                {
                    File.Copy(LegacyPrimaryPath, BackupPath, overwrite: true);
                    FlushToDisk(BackupPath);
                }

                if (File.Exists(PrimaryPath))
                    File.Delete(PrimaryPath);

                File.Move(TempPath, PrimaryPath);
                FlushToDisk(PrimaryPath);

                // Prefer .sav going forward; remove legacy names after a successful write.
                TryDeleteQuiet(LegacyPrimaryPath);
                TryDeleteQuiet(LegacyBackupPath);
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
                var path = Path.Combine(CorruptDirectory, $"profile_{stamp}_{safeReason}.sav");
                File.WriteAllText(path, json ?? "");
            }
            catch
            {
                // 归档失败不阻断启动。
            }
        }

        /// <summary>
        /// Player builds write under InstallDir/SavesDir (Steam Auto-Cloud).
        /// Migrates an older persistentDataPath/saves profile if the new folder is empty.
        /// </summary>
        public static void MigrateFromLegacyPersistentPathIfNeeded(string targetDirectory)
        {
#if UNITY_EDITOR
            return;
#else
            if (string.IsNullOrEmpty(targetDirectory))
                return;

            var hasTarget = File.Exists(Path.Combine(targetDirectory, ProfileFileName))
                            || File.Exists(Path.Combine(targetDirectory, LegacyProfileFileName));
            if (hasTarget)
                return;

            var legacyDir = Path.Combine(Application.persistentDataPath, "saves");
            if (!Directory.Exists(legacyDir))
                return;

            var legacyPrimary = Path.Combine(legacyDir, LegacyProfileFileName);
            var legacySav = Path.Combine(legacyDir, ProfileFileName);
            var source = File.Exists(legacySav) ? legacySav
                : File.Exists(legacyPrimary) ? legacyPrimary
                : null;
            if (source == null)
                return;

            try
            {
                Directory.CreateDirectory(targetDirectory);
                var dest = Path.Combine(targetDirectory, ProfileFileName);
                File.Copy(source, dest, overwrite: false);

                var legacyBak = Path.Combine(legacyDir, LegacyBackupFileName);
                var legacyBakSav = Path.Combine(legacyDir, BackupFileName);
                var bakSource = File.Exists(legacyBakSav) ? legacyBakSav
                    : File.Exists(legacyBak) ? legacyBak
                    : null;
                if (bakSource != null)
                    File.Copy(bakSource, Path.Combine(targetDirectory, BackupFileName), overwrite: false);

                Debug.Log($"[Save] Migrated legacy profile to Auto-Cloud path: {dest}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Save] Legacy migrate failed: {ex.Message}");
            }
#endif
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

        static void TryDeleteQuiet(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore
            }
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
