using System;
using System.Collections.Generic;
using Grimhand.Persistence;
using UnityEngine;

namespace Grimhand.Steam
{
    /// <summary>
    /// Pure startup reconciliation of signed payloads. The caller chooses the core-state winner.
    /// Never consults Steam, changes timestamps, or combines currencies, cards, runs or other game state.
    /// </summary>
    public static class CloudAchievementMerge
    {
        public static bool TryMerge(string winnerJson, string otherJson, ulong currentSteamAccountId,
            SaveValidationContext context, out string mergedJson, out bool changed, out string error)
        {
            mergedJson = winnerJson;
            changed = false;
            if (!CloudProfileSafety.TryValidate(winnerJson, context, out _, out error)
                || !CloudProfileSafety.TryValidate(otherJson, context, out _, out error)) return false;
            try
            {
                var winner = JsonUtility.FromJson<PlayerProfileSaveData>(winnerJson);
                var other = JsonUtility.FromJson<PlayerProfileSaveData>(otherJson);
                // Always map before reading achievement extensions: legacy signatures do not cover them.
                var winnerProgress = SaveDataMapper.FromDto(winner).Achievements;
                var otherProgress = SaveDataMapper.FromDto(other).Achievements;
                if (currentSteamAccountId == 0
                    || !Compatible(winnerProgress.SteamAccountId, currentSteamAccountId)
                    || !Compatible(otherProgress.SteamAccountId, currentSteamAccountId)) return true;

                var ids = new HashSet<string>(winnerProgress.Unlocked, StringComparer.Ordinal);
                ids.UnionWith(otherProgress.Unlocked);
                var count = Math.Max(winnerProgress.CompletedEngravings, otherProgress.CompletedEngravings);
                // Preserve an existing verified association; empty/empty remains unclaimed until login binding.
                var owner = string.IsNullOrEmpty(winnerProgress.SteamAccountId)
                    ? otherProgress.SteamAccountId : winnerProgress.SteamAccountId;
                if (ids.SetEquals(winnerProgress.Unlocked) && count == winnerProgress.CompletedEngravings
                    && string.Equals(owner, winnerProgress.SteamAccountId, StringComparison.Ordinal)) return true;

                // Current-version DTOs retain every core field verbatim. Legacy winners must migrate normally
                // before signing v8, so historical tutorial/unlock defaults cannot become new false flags.
                var result = winner.saveVersion < 8 ? SaveDataMapper.ToDto(SaveDataMapper.FromDto(winner)) : winner;
                result.lastSavedUtc = winner.lastSavedUtc;
                var sorted = new List<string>(ids);
                sorted.Sort(StringComparer.Ordinal);
                result.achievementIds = sorted.ToArray();
                result.completedEngravings = count;
                result.achievementSteamAccountId = owner ?? "";
                SaveIntegrity.ApplyHash(result);
                var json = JsonUtility.ToJson(result);
                if (!CloudProfileSafety.TryValidate(json, context, out _, out error)) return false;
                mergedJson = json;
                changed = true;
                return true;
            }
            catch (Exception ex)
            {
                error = "Achievement merge failed: " + ex.Message;
                return false;
            }
        }

        static bool Compatible(string owner, ulong account) => string.IsNullOrEmpty(owner)
            || SteamAchievementOwnership.Resolve(owner, account, false, out _, out _) == SteamAchievementOwnership.Status.Ready;
    }
}
