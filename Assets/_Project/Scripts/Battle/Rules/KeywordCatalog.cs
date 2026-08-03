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
            ["respond_defense"] = new("respond_defense", "应对防御", "当选择的目标使用防御牌时，应对的效果会生效（无法应对带有应对关键词的牌）"),
            ["armor_up"] = new("armor_up", "强固×层数", "增加防御卡给予的护甲，每层加1%"),
            ["vulnerable"] = new("vulnerable", "易伤×层数", "增加单位受到的伤害，每层加1%"),
            ["polluted"] = new("polluted", "污染", "卡牌拥有者死亡，无法使用"),
            ["exhaust"] = new("exhaust", "消耗", "使用后进入消耗堆，本场不再抽到；神圣轮回可将其洗回"),
            ["burn"] = new("burn", "灼烧×层数", "回合结束每层造成1伤害，无视防御力"),
            ["sacrifice"] = new("sacrifice", "献祭", "使用后扣除自己生命值"),
            ["armor_down"] = new("armor_down", "破损×层数", "减少防御卡给予的护甲，每层减1%"),
            ["ethereal"] = new("ethereal", "虚化", "本回合最多只受到1点伤害"),
            ["weaken"] = new("weaken", "虚弱×层数", "造成的伤害减少，每层减1%"),
            ["brand"] = new("brand", "烙印", "累计三层时该角色会即死"),
            ["summon"] = new("summon", "召唤", "效果会召唤单位或造物进入战场"),
            ["token"] = new("token", "衍生", "战斗中生成的临时卡牌，通常不进入常规构筑"),
            ["inherit"] = new("inherit", "继承", "回合结束仍留在手牌，不会因回合结束被弃置"),
            ["self_destruct"] = new("self_destruct", "自爆", "使用或触发后会牺牲自身并造成相应效果"),
            ["bonus_hand"] = new("bonus_hand", "额外手牌", "额外置入手牌，不占用本回合常规抽牌名额"),
            ["usable_while_constricted"] = new("usable_while_constricted", "缠绕可用", "即使处于缠绕禁牌状态也可以打出"),
            ["quick_start"] = new("quick_start", "快速启动", "打出后立即生效，无需等到本回合结算"),
            ["melee"] = new("melee", "近战", "主要作用于前排与中排目标"),
            ["snipe"] = new("snipe", "狙击", "可选择后排等指定站位目标"),
            ["pierce"] = new("pierce", "贯穿", "命中主目标后可能继续影响后方单位"),
            ["far_shot"] = new("far_shot", "远射", "可攻击后排；对后排可能有伤害修正"),
        };
    }
}
