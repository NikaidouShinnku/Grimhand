using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>关键词（对照 Grimhand实际内容总览表.xlsx · 卡牌 sheet）。</summary>
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
                if (string.IsNullOrEmpty(id) || !TryGet(id, out var def))
                    continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(def.DisplayName).Append("：").Append(def.Description);
            }
            return sb.ToString();
        }

        public static string BuildRichTooltipText(IReadOnlyList<string> keywordIds)
        {
            if (keywordIds == null || keywordIds.Count == 0)
                return "";
            var sb = new StringBuilder();
            for (var i = 0; i < keywordIds.Count; i++)
            {
                var id = keywordIds[i];
                if (string.IsNullOrEmpty(id) || !TryGet(id, out var def))
                    continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("<b>").Append(def.DisplayName).Append("</b>\n").Append(def.Description);
            }
            return sb.ToString();
        }

        static Dictionary<string, KeywordDefinition> BuildDefinitions() => new()
        {
            ["aoe"] = new("aoe", "AOE", "对敌方全体生效"),
            ["parry"] = new("parry", "应对攻击", "当该角色受到攻击前，应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["respond_status"] = new("respond_status", "应对状态", "当选择的目标使用状态牌时，应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["respond_defense"] = new("respond_defense", "应对防御", "当选择的目标使用防御牌时， 应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["polluted"] = new("polluted", "污染", "卡牌拥有者死亡，无法使用"),
            ["sacrifice"] = new("sacrifice", "献祭", "使用后扣除自己生命值"),
        };
    }
}
