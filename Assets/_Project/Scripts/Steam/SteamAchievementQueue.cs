using System;
using System.Collections.Generic;

namespace Grimhand.Steam
{
    /// <summary>Pure ownership policy. Only the production caller may attest a real player login.</summary>
    public static class SteamAchievementOwnership
    {
        public enum Status { Waiting, Ready, Mismatch, Invalid }

        public static Status Resolve(string recorded, ulong account, bool verifiedPlayerLogin,
            out ulong owner, out bool stamp)
        {
            owner = 0;
            stamp = false;
            if (string.IsNullOrEmpty(recorded))
            {
                if (!verifiedPlayerLogin || account == 0) return Status.Waiting;
                owner = account;
                stamp = true;
                return Status.Ready;
            }
            if (!ulong.TryParse(recorded, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out owner) || owner == 0)
                return Status.Invalid;
            if (account == 0) return Status.Waiting;
            return account == owner ? Status.Ready : Status.Mismatch;
        }
    }

    /// <summary>All methods/events run on the game thread. Tests inject a fake; no native API is required.</summary>
    public interface ISteamAchievementAdapter : IDisposable
    {
        bool IsAvailable { get; }
        ulong AccountId { get; }
        event Action<ulong, bool> StatsReceived;
        event Action<bool> StatsStored;
        bool RequestCurrentStats();
        bool GetAchievement(string id, out bool unlocked);
        bool SetAchievement(string id);
        bool StoreStats();
    }

    /// <summary>Monotonic local-to-Steam reconciliation. Successful StoreStats callback commits a batch.</summary>
    public sealed class SteamAchievementQueue : IDisposable
    {
        readonly ISteamAchievementAdapter adapter;
        readonly Action<string> log;
        readonly HashSet<string> confirmed = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> batch = new List<string>();
        readonly HashSet<string> uncommitted = new HashSet<string>(StringComparer.Ordinal);
        Func<IEnumerable<string>> earned;
        ulong owner;
        bool ready, requesting, storing, disposed;
        double now, deadline, nextAttempt;
        int failures;

        public SteamAchievementQueue(ISteamAchievementAdapter adapter, Action<string> log = null)
        {
            this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            this.log = log;
            adapter.StatsReceived += OnStatsReceived;
            adapter.StatsStored += OnStatsStored;
        }

        /// <param name="accountId">Persisted profile owner if available; zero pins to the next observed account.</param>
        public void Bind(Func<IEnumerable<string>> unlocked, ulong accountId = 0)
        {
            earned = unlocked;
            owner = accountId;
            Invalidate();
            failures = 0;
            nextAttempt = now;
        }

        void Invalidate()
        {
            ready = requesting = storing = false;
            confirmed.Clear();
            batch.Clear();
        }

        bool IsCurrentAccount => adapter.IsAvailable && owner != 0 && adapter.AccountId == owner;

        /// <summary>Pass monotonic, unscaled seconds. Calls never wait for Steam or perform disk/cloud writes.</summary>
        public void Tick(double unscaledSeconds)
        {
            if (disposed) return;
            now = unscaledSeconds;
            if (earned == null) return;
            if (!adapter.IsAvailable) { Invalidate(); return; }
            var account = adapter.AccountId;
            if (account == 0) return;
            if (owner == 0) owner = account;
            if (account != owner) { Invalidate(); return; }
            if (requesting || storing)
            {
                if (now >= deadline) Retry("Steam achievement callback timed out.");
                return;
            }
            if (now < nextAttempt) return;
            if (!ready)
            {
                requesting = true;
                deadline = now + 30;
                if (!adapter.RequestCurrentStats()) Retry("RequestCurrentStats rejected.");
                return;
            }

            bool rejected = false;
            var ids = earned();
            if (ids == null) return;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || confirmed.Contains(id) || batch.Contains(id)) continue;
                if (!adapter.GetAchievement(id, out var unlocked)) { rejected = true; continue; }
                if (unlocked && !uncommitted.Contains(id)) { confirmed.Add(id); continue; }
                if (adapter.SetAchievement(id)) { batch.Add(id); uncommitted.Add(id); }
                else rejected = true;
            }
            if (batch.Count > 0)
            {
                storing = true;
                deadline = now + 30;
                if (!adapter.StoreStats()) Retry("StoreStats rejected.");
            }
            else if (rejected) Retry("Achievement API rejected an ID; check published Steam backend definitions.");
            else { failures = 0; nextAttempt = now + 2; }
        }

        void OnStatsReceived(ulong account, bool success)
        {
            if (disposed || !requesting || !IsCurrentAccount || account != owner) return;
            requesting = false;
            if (!success) { Retry("Steam stats download failed."); return; }
            ready = true;
        }

        void OnStatsStored(bool success)
        {
            if (disposed || !storing || !IsCurrentAccount) return;
            storing = false;
            if (!success) { Retry("Steam stats upload failed."); return; }
            foreach (var id in batch) { confirmed.Add(id); uncommitted.Remove(id); }
            batch.Clear();
            failures = 0;
            nextAttempt = now + 2;
        }

        void Retry(string reason)
        {
            // Reload before retry: SetAchievement changes Steam's cache even if StoreStats fails.
            Invalidate();
            var delay = Math.Min(300, 2 * Math.Pow(2, Math.Min(failures++, 8)));
            nextAttempt = now + delay;
            log?.Invoke(reason + " Retrying in " + delay + "s.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            adapter.StatsReceived -= OnStatsReceived;
            adapter.StatsStored -= OnStatsStored;
            adapter.Dispose();
        }
    }
}
