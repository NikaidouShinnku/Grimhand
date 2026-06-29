using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>关键词（对照 Grimhand实际内容总览表 v0.8 · 卡牌 sheet）。</summary>
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
            ["poison"] = new("poison", "中毒×层数", "回合开始每层造成1伤害，无视护甲"),
            ["damage_reduction"] = new("damage_reduction", "减伤×层数", "减少单位受到的伤害，每层减1%"),
            ["slow"] = new("slow", "减速×层数", "每层使角色减少1点SPD"),
            ["damage_up"] = new("damage_up", "增伤×层数", "攻击卡获得伤害加成，每层加1%"),
            ["parry"] = new("parry", "应对攻击", "当该角色受到攻击前，应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["respond_status"] = new("respond_status", "应对状态", "当选择的目标使用状态牌时，应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["respond_defense"] = new("respond_defense", "应对防御", "当选择的目标使用防御牌时， 应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["armor_up"] = new("armor_up", "强固×层数", "增加防御卡给予的护甲，每层加1%"),
            ["vulnerable"] = new("vulnerable", "易伤×层数", "增加单位受到的伤害，每层加1%"),
            ["polluted"] = new("polluted", "污染", "卡牌拥有者死亡，无法使用"),
            ["exhaust"] = new("exhaust", "消耗", "使用后移除本场战斗"),
            ["burn"] = new("burn", "灼烧×层数", "回合结束每层造成1伤害，无视防御力"),
            ["sacrifice"] = new("sacrifice", "献祭", "使用后扣除自己生命值"),
            ["armor_down"] = new("armor_down", "破损×层数", "减少防御卡给予的护甲，每层减1%"),
            ["ethereal"] = new("ethereal", "虚化", "本回合最多只受到1点伤害"),
            ["weaken"] = new("weaken", "虚弱×层数", "攻击卡获得伤害减益，每层减1%"),
        };
    }
}
