using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>策划表「Gramhand实际卡牌遗物表.xlsx」关键词页。</summary>
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
                    sb.Append("\n\n");
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
                if (string.IsNullOrEmpty(id))
                    continue;

                if (!TryGet(id, out var def))
                {
                    if (sb.Length > 0)
                        sb.Append("\n\n");
                    sb.Append(id);
                    continue;
                }

                if (sb.Length > 0)
                    sb.Append("\n\n");
                sb.Append("<b>").Append(def.DisplayName).Append("</b>\n").Append(def.Description);
            }

            return sb.ToString();
        }

        static Dictionary<string, KeywordDefinition> BuildDefinitions()
        {
            return new Dictionary<string, KeywordDefinition>
            {
                ["exhaust"] = new("exhaust", "消耗", "使用后从本场战斗中移除，不再进入抽牌堆。"),
                ["sacrifice"] = new("sacrifice", "献祭", "打出时需要先扣除指定生命值，再结算其余效果。"),
                ["parry"] = new("parry", "应对攻击", "当该角色受到攻击前，此应对效果会先生效。"),
                ["respond_defense"] = new("respond_defense", "应对防御", "当选择的目标使用防御牌时，应对的效果会生效。"),
                ["respond_status"] = new("respond_status", "应对状态", "当选择的目标使用状态牌时，应对的效果会生效。"),
                ["position"] = new("position", "位置", "只能指定敌方前/中/后排中允许的位置作为目标。"),
                ["aoe"] = new("aoe", "AOE", "对敌方全体生效，无需逐个选择目标。"),
                ["poison"] = new("poison", "中毒", "回合开始时每层造成1点伤害；层数可叠加。"),
                ["burn"] = new("burn", "灼烧", "回合结束时每层造成1点火焰伤害（无视DEF）；层数可叠加。"),
                ["slow"] = new("slow", "减速", "每层使角色减少 2 点速度；持续若干回合。"),
                ["polluted"] = new("polluted", "污染", "卡牌拥有者已死亡，此牌无法使用。")
            };
        }
    }
}
