using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public sealed class ExpeditionEventChoiceDefinition
    {
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class ExpeditionEventDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SceneText { get; set; } = "";
        public List<ExpeditionEventChoiceDefinition> Choices { get; } = new();
        public string PrerequisiteFlag { get; set; } = "";
        public string RequiredRelicId { get; set; } = "";
        public bool RequiresDemonInParty { get; set; }
        public int MinGold { get; set; }
    }

    public static class ExpeditionEventCatalog
    {
        public static IReadOnlyList<ExpeditionEventDefinition> All { get; } = BuildAll();

        public static bool TryGet(string id, out ExpeditionEventDefinition definition)
        {
            foreach (var evt in All)
            {
                if (evt.Id == id)
                {
                    definition = evt;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        static List<ExpeditionEventDefinition> BuildAll()
        {
            return new List<ExpeditionEventDefinition>
            {
                Evt(ExpeditionEventIds.MysteriousTraveler, "神秘旅者",
                    "戴兜帽的旅者摊开手掌，展示发光的物品。\n「交易，还是离开？」",
                    Choice("A", "用 30 金币购买", "获得随机卡牌奖励"),
                    Choice("B", "接受礼物", "获得普通遗物，牌堆加入 1 张诅咒牌"),
                    Choice("C", "拒绝离开", "无事发生")),
                Evt(ExpeditionEventIds.AncientTemple, "古老神殿",
                    "残破神殿中祭台火焰仍在燃烧，神像似乎在注视着你。",
                    Choice("A", "虔诚祈祷", "全队失去 10% HP，远征期间 ATK+1"),
                    Choice("B", "亵渎圣堂", "获得 50 金币，下场战斗敌人 ATK+20%"),
                    Choice("C", "静默离开", "无事发生")),
                Evt(ExpeditionEventIds.InjuredAdventurer, "受伤的冒险者",
                    "倒地的冒险者仍在流血：「求你…帮帮我…」",
                    Choice("A", "救治", "全队失去 15% HP，获得随机遗物"),
                    Choice("B", "搜刮", "获得 20 金币与随机卡牌"),
                    Choice("C", "无视", "无事发生")),
                Evt(ExpeditionEventIds.MagicSpring, "魔法泉水",
                    "荧光泉水映照出奇异影像。",
                    Choice("A", "饮用泉水", "随机：全队回复 25% HP / 1 人 ATK+2 / 全队失去 15% HP"),
                    Choice("B", "装瓶带走", "获得 2 个「泉水瓶」消耗品"),
                    Choice("C", "不碰", "无事发生")),
                Evt(ExpeditionEventIds.GamblerDice, "赌徒的骰子",
                    "矮人转着发光骰子：「来玩一把？」",
                    Choice("A", "小赌（20 金币）", "50% 获得 50 金币"),
                    Choice("B", "大赌（全部金币）", "40% 翻倍 / 30% 清零 / 30% 稀有遗物"),
                    Choice("C", "不赌", "无事发生"),
                    minGold: 20),
                Evt(ExpeditionEventIds.MirrorPhantom, "镜中幻影",
                    "魔法镜中映出会动的队伍影子。",
                    Choice("A", "进入镜中挑战", "镜像战斗，胜利获得蓝色卡牌"),
                    Choice("B", "打碎镜子", "获得「镜之碎片」消耗品"),
                    Choice("C", "离开", "无事发生")),
                Evt(ExpeditionEventIds.CursedBookshelf, "被诅咒的书架",
                    "一本书在自行翻页，文字不断变化。",
                    Choice("A", "阅读", "随机 1 人失去 10 HP，获得随机蓝色卡牌"),
                    Choice("B", "撕页带走", "获得「古卷残页」消耗品"),
                    Choice("C", "合上书", "无事发生")),
                Evt(ExpeditionEventIds.AdventurerRevenge, "冒险者的复仇",
                    "被你搜刮过的冒险者带着同伴出现了。",
                    Choice("A", "道歉赔偿（40 金币）", "和解，下 3 层节点类型全部可见"),
                    Choice("B", "应战", "战斗：2 名骷髅兵级敌人，胜利 +30 金币"),
                    Choice("C", "逃跑", "全队失去 5% HP"),
                    prerequisite: "looted_adventurer"),
                Evt(ExpeditionEventIds.TrainingDummy, "训练人偶",
                    "破旧训练人偶仍可用于练习。",
                    Choice("A", "全队训练", "全队失去 10% HP，远征 DEF+1"),
                    Choice("B", "单人特训", "1 名角色失去 20% HP，ATK+2"),
                    Choice("C", "休息", "全队回复 10% HP")),
                Evt(ExpeditionEventIds.SoulRift, "灵魂裂隙",
                    "紫色能量从空间裂缝中涌出。",
                    Choice("A", "吸收能量", "能量上限 +1，每场战斗开始随机 1 人 -5 HP"),
                    Choice("B", "封印裂隙", "移除 1 张卡牌，获得稀有遗物"),
                    Choice("C", "绕行", "无事发生")),
                Evt(ExpeditionEventIds.WanderingSmith, "流浪铁匠",
                    "驼背铁匠的炉火仍在燃烧。",
                    Choice("A", "强化卡牌（15 金币）", "1 张卡牌效果 +20%"),
                    Choice("B", "融合卡牌", "销毁 2 张同类型牌，获得更高品质牌"),
                    Choice("C", "离开", "无事发生")),
                Evt(ExpeditionEventIds.TiredCamp, "疲惫营地",
                    "废弃营地余烬未熄，可以休整。",
                    Choice("A", "深度休息", "跳过下一层选择，全队回复 30% HP"),
                    Choice("B", "简单休息", "全队回复 15% HP"),
                    Choice("C", "搜索营地", "获得 10-25 金币")),
                Evt(ExpeditionEventIds.JadeWorkshop, "玉匠工坊",
                    "老工匠看到你的翡翠原石，眼睛一亮。",
                    Choice("A", "打磨为戒指", "翡翠原石 → 翡翠戒指"),
                    Choice("B", "雕刻为短刀", "翡翠原石 → 翡翠短刀"),
                    requiredRelic: RelicIds.JadeStone),
                Evt(ExpeditionEventIds.AncientFurnace, "古老熔炉",
                    "远古熔炉仍在燃烧，靴子似乎有所回应。",
                    Choice("A", "以血淬火", "全队 -10% HP，燃烬之靴 → 赤红烈焰靴"),
                    Choice("B", "保留原样", "无事发生"),
                    requiredRelic: RelicIds.BurningBoots),
                Evt(ExpeditionEventIds.AbyssWhisper, "深渊低语",
                    "黑暗中的呢喃让你感到诱惑。",
                    Choice("A", "倾听低语", "恶魔 -20% HP，恶魔获得专属紫卡"),
                    Choice("B", "献出记忆", "移除 1 张卡牌，全队 ATK+1"),
                    Choice("C", "离开", "无事发生"),
                    requiresDemon: true)
            };
        }

        static ExpeditionEventDefinition Evt(
            string id,
            string name,
            string scene,
            ExpeditionEventChoiceDefinition c1,
            ExpeditionEventChoiceDefinition c2,
            ExpeditionEventChoiceDefinition c3 = null,
            string prerequisite = "",
            string requiredRelic = "",
            bool requiresDemon = false,
            int minGold = 0)
        {
            var evt = new ExpeditionEventDefinition
            {
                Id = id,
                DisplayName = name,
                SceneText = scene,
                PrerequisiteFlag = prerequisite,
                RequiredRelicId = requiredRelic,
                RequiresDemonInParty = requiresDemon,
                MinGold = minGold
            };
            evt.Choices.Add(c1);
            evt.Choices.Add(c2);
            if (c3 != null)
                evt.Choices.Add(c3);
            return evt;
        }

        static ExpeditionEventChoiceDefinition Choice(string label, string title, string desc) =>
            new() { Label = label, Description = $"{title}：{desc}" };
    }
}
