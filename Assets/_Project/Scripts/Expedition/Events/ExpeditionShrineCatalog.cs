using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public sealed class ExpeditionShrineChoiceDefinition
    {
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class ExpeditionShrineDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SceneText { get; set; } = "";
        public List<ExpeditionShrineChoiceDefinition> Choices { get; } = new();
    }

    public static class ExpeditionShrineCatalog
    {
        public static bool TryGet(string shrineId, out ExpeditionShrineDefinition definition)
        {
            foreach (var shrine in All)
            {
                if (shrine.Id == shrineId)
                {
                    definition = shrine;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public static IReadOnlyList<ExpeditionShrineDefinition> All { get; } = BuildAll();

        static List<ExpeditionShrineDefinition> BuildAll()
        {
            return new List<ExpeditionShrineDefinition>
            {
                Shrine(
                    ExpeditionShrineIds.Blood,
                    "血之祭坛",
                    "鲜血在石槽中流动，献祭可换取力量。",
                    Choice("A", "献祭一名角色 50% 当前 HP",
                        "该角色本次远征 ATK+3（恶魔额外 +1）。"),
                    Choice("B", "全队各献祭 15% 当前 HP",
                        "获得 1 个随机普通遗物。"),
                    Choice("C", "离开",
                        "不献祭，安全离开。")),
                Shrine(
                    ExpeditionShrineIds.Knowledge,
                    "知识祭坛",
                    "古老符文要求以卡牌作为献祭。",
                    Choice("A", "献祭 2 张卡牌（永久移出牌堆）",
                        "从 3 张随机蓝色卡牌中选 1 张加入（占位）。"),
                    Choice("B", "献祭 1 张蓝色以上卡牌",
                        "获得 1 张随机紫色卡牌（占位）。"),
                    Choice("C", "离开",
                        "不献祭，安全离开。")),
                Shrine(
                    ExpeditionShrineIds.Soul,
                    "灵魂祭坛",
                    "灵魂之火跳动，可交换遗物或等级。",
                    Choice("A", "献祭 1 个遗物",
                        "获得 1 个更高稀有度的遗物。"),
                    Choice("B", "献祭 1 名角色全部局内经验（等级重置为 1）",
                        "该角色永久 ATK+3、DEF+2、HP+15（占位）。"),
                    Choice("C", "离开",
                        "不献祭，安全离开。")),
                Shrine(
                    ExpeditionShrineIds.Chaos,
                    "混沌祭坛",
                    "混沌能量无序涌动，代价与回报皆不可预知。",
                    Choice("A", "进行混沌仪式",
                        "随机代价：20% HP / 移除 1 张牌 / 失去 20 金币。\n随机奖励：遗物 / 蓝紫卡牌 / 全队 +1 属性 / 空（10%）。"),
                    Choice("B", "离开",
                        "拒绝仪式，安全离开。"))
            };
        }

        static ExpeditionShrineDefinition Shrine(
            string id,
            string name,
            string scene,
            params ExpeditionShrineChoiceDefinition[] choices)
        {
            var shrine = new ExpeditionShrineDefinition
            {
                Id = id,
                DisplayName = name,
                SceneText = scene
            };
            shrine.Choices.AddRange(choices);
            return shrine;
        }

        static ExpeditionShrineChoiceDefinition Choice(string label, string title, string description) =>
            new()
            {
                Label = label,
                Description = string.IsNullOrEmpty(description) ? title : $"{title}\n{description}"
            };
    }
}
