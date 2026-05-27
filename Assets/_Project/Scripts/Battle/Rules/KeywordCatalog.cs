using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class KeywordCatalog
    {
        static readonly Dictionary<string, KeywordDefinition> Definitions = BuildDefinitions();

        public static bool TryGet(string keywordId, out KeywordDefinition definition) =>
            Definitions.TryGetValue(keywordId, out definition);

        public static string BuildTooltipText(IReadOnlyList<string> keywordIds)
        {
            if (keywordIds == null || keywordIds.Count == 0)
                return "";

            var sb = new StringBuilder();
            for (var i = 0; i < keywordIds.Count; i++)
            {
                var id = keywordIds[i];
                if (string.IsNullOrEmpty(id))
                    continue;

                if (!TryGet(id, out var def))
                {
                    if (sb.Length > 0)
                        sb.Append('\n');
                    sb.Append(id);
                    continue;
                }

                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(def.DisplayName).Append("：").Append(def.Description);
            }

            return sb.ToString();
        }

        static Dictionary<string, KeywordDefinition> BuildDefinitions()
        {
            return new Dictionary<string, KeywordDefinition>
            {
                ["block"] = new("block", "护甲", "优先吸收受到的生命伤害；回合结束时清零。"),
                ["parry"] = new("parry", "弹反", "武装后，下一次受到攻击时减伤并按攻击威力反射伤害。"),
                ["poison"] = new("poison", "中毒", "回合开始时受到等于层数的伤害；层数可叠加。"),
                ["slow"] = new("slow", "减速", "降低速度，影响同速结算顺序；持续若干回合。"),
                ["melee"] = new("melee", "近战", "只能指定敌方前排或中排单位。"),
                ["snipe"] = new("snipe", "狙击", "可指定任意敌方单位，包括后排。"),
                ["pierce"] = new("pierce", "贯通", "命中主目标后，对其后方槽位的敌人造成溅射伤害。"),
                ["far_shot"] = new("far_shot", "远射", "可攻击后排，但对后排目标伤害降低。"),
                ["slot"] = new("slot", "槽位", "按敌方前/中/后排自动选择目标，无需点选。")
            };
        }
    }
}
