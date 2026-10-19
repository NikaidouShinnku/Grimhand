#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif
using System;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Grimhand.Steam
{
    public sealed class SteamworksAchievementAdapter : ISteamAchievementAdapter
    {
        readonly bool allowEditor;
        bool disposed;
        public event Action<ulong, bool> StatsReceived;
        public event Action<bool> StatsStored;
#if !DISABLESTEAMWORKS
        CallResult<UserStatsReceived_t> received;
        Callback<UserStatsStored_t> stored;
#endif
        public SteamworksAchievementAdapter(bool allowEditorSubmissions = false) => allowEditor = allowEditorSubmissions;

        public static bool TryGetAccount(bool allowEditorSubmissions, out ulong account)
        {
            account = 0;
#if !DISABLESTEAMWORKS
#if UNITY_EDITOR
            if (!allowEditorSubmissions) return false;
#endif
            if (!SteamManager.Initialized || !SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId != SteamManager.AppId) return false;
            account = SteamUser.GetSteamID().m_SteamID;
#endif
            return account != 0;
        }

        public bool IsAvailable => !disposed && TryGetAccount(allowEditor, out _);
        public ulong AccountId => !disposed && TryGetAccount(allowEditor, out var account) ? account : 0;

        public bool RequestCurrentStats()
        {
#if !DISABLESTEAMWORKS
            if (!IsAvailable) return false;
            if (received == null) received = CallResult<UserStatsReceived_t>.Create(OnReceived);
            if (stored == null) stored = Callback<UserStatsStored_t>.Create(OnStored);
            // Steamworks.NET 2025.163 removed RequestCurrentStats (client now preloads stats).
            // Explicitly request the current user's stats to retain callback-gated readiness.
            var call = SteamUserStats.RequestUserStats(SteamUser.GetSteamID());
            if (call == SteamAPICall_t.Invalid) return false;
            received.Set(call);
            return true;
#else
            return false;
#endif
        }

        public bool GetAchievement(string id, out bool unlocked)
        {
            unlocked = false;
#if !DISABLESTEAMWORKS
            return IsAvailable && SteamUserStats.GetAchievement(id, out unlocked);
#else
            return false;
#endif
        }
        public bool SetAchievement(string id)
        {
#if !DISABLESTEAMWORKS
            return IsAvailable && SteamUserStats.SetAchievement(id);
#else
            return false;
#endif
        }
        public bool StoreStats()
        {
#if !DISABLESTEAMWORKS
            return IsAvailable && SteamUserStats.StoreStats();
#else
            return false;
#endif
        }
#if !DISABLESTEAMWORKS
        void OnReceived(UserStatsReceived_t result, bool ioFailure)
        {
            if (ioFailure) { if (IsAvailable) StatsReceived?.Invoke(AccountId, false); return; }
            if (IsAvailable && result.m_nGameID == SteamManager.AppId && result.m_steamIDUser.m_SteamID == AccountId)
                StatsReceived?.Invoke(result.m_steamIDUser.m_SteamID, result.m_eResult == EResult.k_EResultOK);
        }
        void OnStored(UserStatsStored_t result)
        {
            if (IsAvailable && result.m_nGameID == SteamManager.AppId)
                StatsStored?.Invoke(result.m_eResult == EResult.k_EResultOK);
        }
#endif
        public void Dispose()
        {
            disposed = true;
#if !DISABLESTEAMWORKS
            received?.Dispose();
            stored?.Dispose();
#endif
        }
    }
}
