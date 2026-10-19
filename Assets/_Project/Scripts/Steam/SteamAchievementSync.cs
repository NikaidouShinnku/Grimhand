using System.Collections.Generic;
using System.Globalization;
using Grimhand.Persistence;
using UnityEngine;

namespace Grimhand.Steam
{
    /// <summary>
    /// GameFlow calls Bind after loading/replacing its profile. Local IDs must equal Steam API names.
    /// Local persistence owns unlocks and saves the SteamAccountId assigned after a real player login.
    /// This component never removes local or Steam unlocks.
    /// Only one instance should own submissions. SteamManager pumps callbacks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamAchievementSync : MonoBehaviour
    {
        [SerializeField] bool allowEditorSubmissions;
        SteamAchievementQueue queue;
        PlayerProfileState profile;
        ulong owner;
        string warnedOwner;

        /// <summary>
        /// Reads Achievements.SteamAccountId and stamps it only when empty after real player login.
        /// Call on load and before the regular save to persist that association. No disk write is performed.
        /// Bind(null) stops synchronization. A different recorded owner skips Steam uploads only.
        /// </summary>
        public void Bind(PlayerProfileState value)
        {
            bool changed = !ReferenceEquals(profile, value);
            profile = value;
            if (changed) { owner = 0; warnedOwner = null; BindQueue(); }
            RefreshOwnership();
            if (queue == null) ResetQueue();
        }

        public ulong BoundSteamAccountId => owner;

        void ResetQueue()
        {
            queue?.Dispose();
            queue = null;
            if (profile == null || !isActiveAndEnabled) return;
            queue = new SteamAchievementQueue(new SteamworksAchievementAdapter(allowEditorSubmissions),
                message => Debug.LogWarning("[SteamAchievements] " + message, this));
            BindQueue();
        }

        void BindQueue() => queue?.Bind(owner == 0 || profile == null
            ? (System.Func<IEnumerable<string>>)null : () => profile.Achievements?.Unlocked, owner);

        void RefreshOwnership()
        {
            var progress = profile?.Achievements;
            ulong account = 0;
            bool available = progress != null && SteamworksAchievementAdapter.TryGetAccount(allowEditorSubmissions, out account);
            // Editor opt-in may submit an already associated profile, but must never claim one.
            bool canRecord = available && !Application.isEditor;
            var recorded = progress?.SteamAccountId;
            var status = SteamAchievementOwnership.Resolve(recorded, available ? account : 0, canRecord,
                out var resolvedOwner, out var stamp);
            if (stamp) progress.SteamAccountId = resolvedOwner.ToString(CultureInfo.InvariantCulture);
            if (status == SteamAchievementOwnership.Status.Mismatch || status == SteamAchievementOwnership.Status.Invalid)
            {
                var warningKey = (recorded ?? "") + ":" + account;
                if (warnedOwner != warningKey)
                {
                    warnedOwner = warningKey;
                    Debug.LogWarning("[SteamAchievements] Profile Steam owner does not match the logged-in account or is invalid. Achievement uploads paused; gameplay and local saves remain available.", this);
                }
            }
            else warnedOwner = null;
            // A missing owner never reaches the queue's optional in-memory auto-pin path.
            var nextOwner = status == SteamAchievementOwnership.Status.Ready ? resolvedOwner : 0;
            if (owner != nextOwner) { owner = nextOwner; BindQueue(); }
        }

        void OnEnable() => ResetQueue();
        void OnDisable() { queue?.Dispose(); queue = null; }
        void Update() => Tick();

        public void Tick()
        {
            if (profile == null) return;
            RefreshOwnership();
            queue?.Tick(Time.realtimeSinceStartupAsDouble);
        }
    }
}
