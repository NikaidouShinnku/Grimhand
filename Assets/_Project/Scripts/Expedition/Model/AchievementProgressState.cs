using System.Collections.Generic;

namespace Grimhand.Expedition.Model
{
    /// <summary>Earned facts, not Steam's acknowledgement cache. Never remove earned IDs.</summary>
    public sealed class AchievementProgressState
    {
        public HashSet<string> Unlocked { get; } = new();
        public int CompletedEngravings { get; set; }
        public string SteamAccountId { get; set; } = "";
        public bool Unlock(string id) => !string.IsNullOrEmpty(id) && Unlocked.Add(id);
    }
}
