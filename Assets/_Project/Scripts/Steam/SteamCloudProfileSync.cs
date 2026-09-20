#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Globalization;
using System.Text;
using Grimhand.Persistence;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Grimhand.Steam
{
    /// <summary>
    /// Syncs the single-player profile with Steam Cloud via ISteamRemoteStorage.
    /// Also compatible with Auto-Cloud when the local path is InstallDir/SavesDir/*.sav.
    /// </summary>
    public static class SteamCloudProfileSync
    {
        public const string CloudFileName = "profile.sav";

        public static void EnsureInitialized()
        {
#if !DISABLESTEAMWORKS && !UNITY_EDITOR
            if (UnityEngine.Object.FindAnyObjectByType<SteamManager>() != null)
                return;
            new GameObject("SteamManager").AddComponent<SteamManager>();
#endif
        }

        public static bool IsAvailable
        {
            get
            {
#if DISABLESTEAMWORKS || UNITY_EDITOR
                return false;
#else
                return SteamManager.Initialized
                       && SteamRemoteStorage.IsCloudEnabledForAccount()
                       && SteamRemoteStorage.IsCloudEnabledForApp();
#endif
            }
        }

        /// <summary>
        /// Before LoadOrCreate: if Cloud has a newer profile, write it into local storage.
        /// </summary>
        public static SteamCloudSyncResult PullIfCloudNewer(ISaveStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

#if DISABLESTEAMWORKS || UNITY_EDITOR
            return SteamCloudSyncResult.Skipped("Editor or Steamworks disabled.");
#else
            if (!IsAvailable)
                return SteamCloudSyncResult.Skipped("Steam Cloud unavailable.");

            if (!SteamRemoteStorage.FileExists(CloudFileName))
                return SteamCloudSyncResult.Skipped("No cloud profile.");

            if (!TryReadCloudJson(out var cloudJson, out var cloudError))
                return SteamCloudSyncResult.Failed(cloudError);

            if (!TryGetLastSavedUtc(cloudJson, out var cloudUtc))
                return SteamCloudSyncResult.Failed("Cloud profile missing lastSavedUtc.");

            var hasLocal = storage.TryReadPrimary(out var localJson)
                           || storage.TryReadBackup(out localJson);
            if (hasLocal && TryGetLastSavedUtc(localJson, out var localUtc) && localUtc >= cloudUtc)
            {
                return SteamCloudSyncResult.KeptLocal(localUtc, cloudUtc);
            }

            if (!storage.TryWrite(cloudJson, out var writeError))
                return SteamCloudSyncResult.Failed(writeError);

            Debug.Log($"[SteamCloud] Pulled cloud profile (utc={cloudUtc:o}).");
            return SteamCloudSyncResult.Pulled(cloudUtc);
#endif
        }

        /// <summary>
        /// After a successful local save: upload primary profile to Steam Cloud.
        /// </summary>
        public static SteamCloudSyncResult PushFromLocal(ISaveStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

#if DISABLESTEAMWORKS || UNITY_EDITOR
            return SteamCloudSyncResult.Skipped("Editor or Steamworks disabled.");
#else
            if (!IsAvailable)
                return SteamCloudSyncResult.Skipped("Steam Cloud unavailable.");

            if (!storage.TryReadPrimary(out var json))
                return SteamCloudSyncResult.Failed("Local primary missing.");

            var bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length == 0)
                return SteamCloudSyncResult.Failed("Local profile empty.");

            SteamRemoteStorage.BeginFileWriteBatch();
            var ok = SteamRemoteStorage.FileWrite(CloudFileName, bytes, bytes.Length);
            SteamRemoteStorage.EndFileWriteBatch();

            if (!ok)
                return SteamCloudSyncResult.Failed("SteamRemoteStorage.FileWrite failed.");

            Debug.Log($"[SteamCloud] Pushed profile ({bytes.Length} bytes).");
            return SteamCloudSyncResult.Pushed(bytes.Length);
#endif
        }

#if !DISABLESTEAMWORKS && !UNITY_EDITOR
        static bool TryReadCloudJson(out string json, out string error)
        {
            json = "";
            error = "";
            var size = SteamRemoteStorage.GetFileSize(CloudFileName);
            if (size <= 0)
            {
                error = "Cloud file size is 0.";
                return false;
            }

            var buffer = new byte[size];
            var read = SteamRemoteStorage.FileRead(CloudFileName, buffer, size);
            if (read <= 0)
            {
                error = "Cloud FileRead failed.";
                return false;
            }

            json = Encoding.UTF8.GetString(buffer, 0, read);
            return !string.IsNullOrWhiteSpace(json);
        }
#endif

        public static bool TryGetLastSavedUtc(string json, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var dto = JsonUtility.FromJson<PlayerProfileSaveData>(json);
                if (dto == null || string.IsNullOrWhiteSpace(dto.lastSavedUtc))
                    return false;

                return DateTime.TryParse(
                    dto.lastSavedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out utc);
            }
            catch
            {
                return false;
            }
        }
    }

    public readonly struct SteamCloudSyncResult
    {
        public enum Kind
        {
            Skipped,
            Pulled,
            Pushed,
            KeptLocal,
            Failed
        }

        public Kind Result { get; }
        public string Message { get; }

        SteamCloudSyncResult(Kind result, string message)
        {
            Result = result;
            Message = message ?? "";
        }

        public static SteamCloudSyncResult Skipped(string message) => new(Kind.Skipped, message);
        public static SteamCloudSyncResult Pulled(DateTime utc) => new(Kind.Pulled, utc.ToString("o"));
        public static SteamCloudSyncResult Pushed(int bytes) => new(Kind.Pushed, $"{bytes} bytes");
        public static SteamCloudSyncResult KeptLocal(DateTime local, DateTime cloud) =>
            new(Kind.KeptLocal, $"local={local:o} cloud={cloud:o}");
        public static SteamCloudSyncResult Failed(string message) => new(Kind.Failed, message);
    }
}
