using System.Collections.Generic;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public sealed class ExpeditionEventChoiceDefinition
    {
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public string AfterChoiceText { get; set; } = "";
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
                    Choice("A", "用 30 金币购买", "随机卡牌奖励",
                        "你掏出金币换得一张卡牌，旅者满意地收下，转身离去。"),
                    Choice("B", "接受礼物", "随机遗物 + 牌组加入诅咒牌「混沌之触」",
                        "你伸手接过礼物的瞬间，一道阴影般的诅咒悄悄爬进了你的牌组。"),
                    Choice("C", "拒绝离开", "无事发生",
                        "旅者耸耸肩，转身消失在阴影中。")),
                Evt(ExpeditionEventIds.AncientTemple, "古老神殿",
                    "残破神殿中祭台圣火仍在燃烧，神像似乎在注视着你。",
                    Choice("A", "虔诚祈祷", "全队 -10% 最大生命值，当前卡组中所有状态牌升1级，同时全队获得+5经验",
                        "你跪地虔诚祈祷，神像微微发光，一股力量灌注全队，但也带来了刺痛。"),
                    Choice("B", "亵渎圣堂", "50 金币；下场战斗敌人攻击牌增加20%伤害",
                        "你撬开祭台取走金币，殿内传来低沉的怒吼，似乎惊动了潜伏的敌人。"),
                    Choice("C", "静默离开", "无事发生",
                        "你悄悄退出神殿，未引起任何注意。")),
                Evt(ExpeditionEventIds.InjuredAdventurer, "受伤的冒险者",
                    "倒地的冒险者仍在流血：「求你…帮帮我…」",
                    Choice("A", "救治", "全队 -15% HP，随机遗物",
                        "你为他包扎伤口，他用尽最后的力气将一件遗物塞进你手中，随后闭眼，不知是死了还是陷入沉睡。"),
                    Choice("B", "搜刮", "20 金币 + 随机卡牌",
                        "你翻找他的口袋，拿走了金币和一张卡牌，留他自生自灭。"),
                    Choice("C", "无视", "无事发生",
                        "你转身离开，他的呻吟声逐渐消失在身后。")),
                Evt(ExpeditionEventIds.MagicSpring, "魔法泉水",
                    "荧光泉水映照出奇异影像。",
                    Choice("A", "饮用泉水", "随机：60% 全队 +25% HP / 25% 1 人选择3张卡各升1级 / 15% 全队 -15% HP"),
                    Choice("B", "装瓶带走", "获得 2 个「泉水瓶」消耗品",
                        "你小心地将泉水装入两个空瓶，收好备用。"),
                    Choice("C", "不碰", "无事发生",
                        "你绕过泉水，继续向前走去。")),
                Evt(ExpeditionEventIds.GamblerDice, "赌徒的骰子",
                    "矮人转着发光骰子：「来玩一把？」",
                    Choice("A", "小赌（20 金币）", "50% 获得 50 金币"),
                    Choice("B", "大赌（全部金币）", "40% 翻倍 / 30% 清零 / 30% 稀有遗物"),
                    Choice("C", "不赌", "无事发生",
                        "你摆摆手谢绝，矮人遗憾地收起骰子。"),
                    minGold: 20),
                Evt(ExpeditionEventIds.MirrorPhantom, "镜中幻影",
                    "魔法镜中映出会动的队伍影子。",
                    Choice("A", "进入镜中挑战", "镜像战斗；胜利获得蓝色卡牌和全队 +5 经验",
                        "镜面波动，你好像看到了你的倒影们冲了出来..."),
                    Choice("B", "打碎镜子", "获得「镜之碎片」消耗品",
                        "你一拳击碎镜面，碎片散落一地，你捡起其中闪烁的一片放入背包。"),
                    Choice("C", "离开", "无事发生",
                        "镜中的影子模仿着你的转身动作，缓缓消失。")),
                Evt(ExpeditionEventIds.CursedBookshelf, "被诅咒的书架",
                    "一本书在自行翻页，文字不断变化。",
                    Choice("A", "阅读", "随机 1 人 -10 HP，随机蓝色卡牌，全队 +5 经验",
                        "书页自行翻动，一段文字钻入你的脑海，伴随刺痛，但你也学到了新的招式。"),
                    Choice("B", "撕页带走", "获得「古卷残页」消耗品",
                        "你迅速撕下一页塞进口袋，书架发出一阵不满的震动。"),
                    Choice("C", "合上书", "无事发生",
                        "你合上书本，书架重新归于沉寂。")),
                Evt(ExpeditionEventIds.AdventurerRevenge, "冒险者的复仇",
                    "被你搜刮过的冒险者带着同伴出现了，他们似乎想讨要一个说法...",
                    Choice("A", "道歉赔偿（40 金币）", "和解，下 3 层节点类型全部可见",
                        "你递上金币诚恳道歉，对方接过后冷哼一声，但还是放你通过并透露了前路的情报。"),
                    Choice("B", "应战", "2 骷髅兵战斗；胜利 +30 金和全队 +8 经验",
                        "冒险者和同伴缓缓摘下伪装，露出阴森白骨，冰冷的说:\"那就偿命吧...\""),
                    Choice("C", "逃跑", "全队 -5% HP",
                        "你转身就跑，对方的追击让全队受了些轻伤。"),
                    prerequisite: "looted_adventurer"),
                Evt(ExpeditionEventIds.TrainingDummy, "训练人偶",
                    "破旧训练人偶仍可用于练习。",
                    Choice("A", "全队训练", "全队 -10% HP，整场远征中所有角色DEF+1，全队+5经验",
                        "全队轮番对人偶展开特训，虽然消耗了不少体力，但每个人的防御技巧都有所提升。"),
                    Choice("B", "单人特训", "1 名角色 -20% HP，该角色选择1张卡升1级并获得+10经验",
                        "一名角色独自苦练到精疲力竭，但攻击力得到了显著提升。"),
                    Choice("C", "休息", "全队回复 10% HP",
                        "队伍围坐在人偶旁稍作休整，恢复了些体力。")),
                Evt(ExpeditionEventIds.SoulRift, "灵魂裂隙",
                    "紫色能量从空间裂缝中涌出。",
                    Choice("A", "吸收能量", "能量上限 +1；每场战斗开始随机 1 人 -5% HP",
                        "你将手伸入裂缝，紫色能量涌入体内，增强了你的能量上限，但狂暴的能量也留下了更深的隐患。"),
                    Choice("B", "封印裂隙", "移除 1 张卡牌，获得稀有遗物",
                        "你献上一张卡牌试图封住裂缝，过了一会卡牌消失，但裂隙中弹出了一件遗物。"),
                    Choice("C", "绕行", "无事发生",
                        "你绕开裂缝，紫光在你身后悄然闭合。")),
                Evt(ExpeditionEventIds.WanderingSmith, "流浪铁匠",
                    "驼背铁匠的炉火仍在燃烧。",
                    Choice("A", "强化卡牌（15 金币）", "1 张卡牌升 1 级",
                        "铁匠接过金币，用锤子在你选中的卡牌上敲打出新的纹路，使其威力增强。"),
                    Choice("B", "融合卡牌", "销毁 2 张同类型牌，随机获得更高品质牌",
                        "铁匠将两张卡牌投入炉火，熔炼出一张品质更高的新卡牌。"),
                    Choice("C", "无视离开", "无事发生",
                        "铁匠摆摆手，继续摆弄手中的工具。")),
                Evt(ExpeditionEventIds.TiredCamp, "疲惫营地",
                    "废弃营地余烬未熄，可以休整。",
                    Choice("A", "深度休息", "跳过下一层选择，全队回复 30% HP",
                        "队伍睡了一夜好觉，醒来时神清气爽，但隐隐感觉迷失了原本的方向。"),
                    Choice("B", "简单休息", "全队回复 15% HP",
                        "队伍稍作小憩，恢复了一些体力后继续上路。"),
                    Choice("C", "搜索营地", "10-25 金币",
                        "你在灰烬中翻找，发现了几枚遗留的金币。")),
                Evt(ExpeditionEventIds.JadeWorkshop, "玉匠工坊",
                    "老工匠看到你的翡翠原石，眼睛一亮。",
                    Choice("A", "打磨为戒指", "翡翠原石 → 翡翠戒指",
                        "老工匠仔细打磨原石，将其雕琢成一枚闪烁的戒指。"),
                    Choice("B", "雕刻为短刀", "翡翠原石 → 翡翠短刀",
                        "老工匠精心雕刻原石，制成了一把锋利的翡翠短刀。"),
                    Choice("C", "离开", "无事发生",
                        "你摇头谢绝，老工匠遗憾地叹了口气。"),
                    requiredRelic: RelicIds.JadeStone),
                Evt(ExpeditionEventIds.AncientFurnace, "古老熔炉",
                    "远古熔炉仍在燃烧，靴子似乎有所回应。",
                    Choice("A", "以血淬火", "全队 -10% HP，燃烬之靴 → 赤红烈焰靴",
                        "队伍将血液滴入熔炉，靴子在烈焰中重塑，散发出赤红的光芒。"),
                    Choice("B", "保留原样", "无事发生",
                        "你决定不冒险，靴子安静地留在原处。"),
                    Choice("C", "探索熔炉", "40% 石傀儡战斗（胜利 +10 经验）/ 30% 随机遗物 / 30% 无事"),
                    requiredRelic: RelicIds.BurningBoots),
                Evt(ExpeditionEventIds.AbyssWhisper, "深渊低语",
                    "黑暗中的呢喃让你感到诱惑，那是恶魔的低语...",
                    Choice("A", "倾听低语", "恶魔 -20% HP，恶魔获得紫卡「魔王降临」1 张",
                        "低语在恶魔耳边回响，伴随着剧痛，一张全新的禁忌卡牌悄然融入了它的牌组。"),
                    Choice("B", "献出记忆", "移除 1 张卡牌，恶魔随机 1 张牌升 2 级",
                        "你献祭一张卡牌作为代价，黑暗低语中暗藏的祝福让恶魔的力量显著增强。"),
                    Choice("C", "离开", "无事发生",
                        "你捂住耳朵快步离开，低语逐渐消失在黑暗中。"),
                    requiresDemon: true),
                Evt(ExpeditionEventIds.FelFlameAltar, "魔焰祭坛",
                    "这是邪恶的仪式，它的力量诱惑着你...",
                    Choice("A", "加入仪式", "玩家获得魔焰颅骨遗物",
                        "你在仪式中感到力量被抽走，两眼昏暗。当你再次睁眼，一颗诡异的颅骨出现在你手中。"),
                    Choice("B", "上前查看", "进入对应层数精英战斗",
                        "你上前查看仪式祭坛，突然几个影子从黑暗中蹦出，他们笑着靠近你..."),
                    Choice("C", "悄悄离开", "无事发生",
                        "你悄悄的离开，仪式的呼唤逐渐消失在黑暗中。"))
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

        static ExpeditionEventChoiceDefinition Choice(string label, string title, string desc, string afterChoiceText = "") =>
            new()
            {
                Label = label,
                Description = $"{title}：{desc}",
                AfterChoiceText = afterChoiceText ?? ""
            };
    }
}
