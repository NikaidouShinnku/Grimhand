using System.Collections.Generic;
using System.Text;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public enum TalentCardState
    {
        Locked,
        Unlocked,
        Selected
    }

    public static class TalentRules
    {
        public static bool IsUnlocked(TalentDefinition talent, CharacterMetaProgress progress)
        {
            if (talent == null || progress == null)
                return false;

            return progress.OutOfRunLevel >= talent.UnlockLevel;
        }

        public static TalentCardState GetCardState(
            TalentDefinition talent,
            CharacterMetaProgress progress)
        {
            if (!IsUnlocked(talent, progress))
                return TalentCardState.Locked;

            var selectedId = progress.GetSelectedTalentId(talent.Slot);
            return selectedId == talent.Id ? TalentCardState.Selected : TalentCardState.Unlocked;
        }

        /// <summary>点击天赋卡：未解锁忽略；已选中则取消；否则替换该槽位选择。</summary>
        public static bool TryToggleSelection(TalentDefinition talent, CharacterMetaProgress progress)
        {
            if (talent == null || progress == null || !IsUnlocked(talent, progress))
                return false;

            var selectedId = progress.GetSelectedTalentId(talent.Slot);
            if (selectedId == talent.Id)
            {
                progress.SetSelectedTalentId(talent.Slot, "");
                return true;
            }

            progress.SetSelectedTalentId(talent.Slot, talent.Id);
            return true;
        }

        public static string BuildActiveEffectsSummary(CharacterMetaProgress progress)
        {
            if (progress == null)
                return "当前无生效天赋。";

            var parts = new List<string>();
            AppendSelected(parts, progress, 1);
            AppendSelected(parts, progress, 2);

            return parts.Count == 0 ? "当前无生效天赋。" : string.Join("  ·  ", parts);
        }

        static void AppendSelected(List<string> parts, CharacterMetaProgress progress, int slot)
        {
            var talentId = progress.GetSelectedTalentId(slot);
            if (string.IsNullOrEmpty(talentId))
                return;

            var talent = TalentCatalog.Get(talentId);
            if (talent == null)
                return;

            parts.Add($"槽位{slot}：{talent.ShortTitle}");
        }

        public static string BuildSelectedDetail(CharacterMetaProgress progress)
        {
            if (progress == null)
                return "";

            var sb = new StringBuilder();
            AppendDetail(sb, progress, 1);
            AppendDetail(sb, progress, 2);
            return sb.ToString().Trim();
        }

        static void AppendDetail(StringBuilder sb, CharacterMetaProgress progress, int slot)
        {
            var talentId = progress.GetSelectedTalentId(slot);
            if (string.IsNullOrEmpty(talentId))
                return;

            var talent = TalentCatalog.Get(talentId);
            if (talent == null)
                return;

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append($"【槽位{slot}·{talent.ShortTitle}】");
            sb.Append(talent.Description);
        }
    }
}
