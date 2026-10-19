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
            ["parry"] = new("parry", "应对攻击", "当该角色受到攻击前，应对的效果会生效"),
            ["respond_status"] = new("respond_status", "应对状态", "当选择的目标使用状态牌时，应对的效果会生效"),
            ["respond_defense"] = new("respond_defense", "应对防御", "当选择的目标使用防御牌时，应对的效果会生效"),
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
            ["berserker_repeat"] = new("berserker_repeat", "再战", "狂战之魂每回合首张符合条件的攻击产生一次虚幻的再次使用机会。独立付费和选目标，按行动顺序执行，不复制进牌库。取消原牌会连带取消再次使用。"),
            ["berserker_duel"] = new("berserker_duel", "死斗", "双方存活时，使用者的单体攻击只能选择死斗目标，且可跨排攻击。目标的普通单体攻击在射程允许时优先攻击使用者；AOE和指定目标的技能不受影响。目标死亡后，若使用者仍存活且战斗继续，下回合正常抽牌后，可从自己的弃牌堆选择最多3张攻击牌加入手牌，在手中时费用为0；不能超过手牌上限。"),
            ["berserker_fuel"] = new("berserker_fuel", "狂怒供能", "全队打牌时自动优先消耗现有狂怒抵扣费用，每点抵扣1费，每张牌最多2费。选牌预留，确认计划时支付，取消选牌释放预留。X费牌不参与。狂怒不再流失，也不再强化卡牌效果。"),
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
