using System.Text;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Battle
{
    /// <summary>属性框用：格式化角色已选天赋。</summary>
    public static class TalentDisplayFormatter
    {
        public static string FormatSelectedTalents(PartyMemberSnapshot member)
        {
            if (member == null)
                return "";

            var sb = new StringBuilder();
            AppendTalent(sb, member.SelectedTalentSlot1Id);
            AppendTalent(sb, member.SelectedTalentSlot2Id);
            return sb.ToString();
        }

        static void AppendTalent(StringBuilder sb, string talentId)
        {
            if (string.IsNullOrEmpty(talentId))
                return;

            var def = TalentCatalog.Get(talentId);
            if (def == null)
                return;

            if (sb.Length > 0)
                sb.Append('\n');

            var title = string.IsNullOrEmpty(def.ShortTitle) ? def.Id : def.ShortTitle;
            sb.Append("<b>").Append(title).Append("</b>");
            if (!string.IsNullOrEmpty(def.Description))
                sb.Append('\n').Append(def.Description);
        }
    }
}
