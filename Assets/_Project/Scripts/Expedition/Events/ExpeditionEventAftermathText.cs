namespace Grimhand.Expedition.Events
{
    /// <summary>事件选项「选择后文本」：概率分支只展示实际命中的一句。</summary>
    public static class ExpeditionEventAftermathText
    {
        public static bool NeedsStochasticRoll(string eventId, int choiceIndex) =>
            (eventId, choiceIndex) switch
            {
                (ExpeditionEventIds.MagicSpring, 0) => true,
                (ExpeditionEventIds.GamblerDice, 0) => true,
                (ExpeditionEventIds.GamblerDice, 1) => true,
                (ExpeditionEventIds.AncientFurnace, 2) => true,
                _ => false
            };

        public static string Resolve(string eventId, int choiceIndex, int roll100)
        {
            roll100 = System.Math.Clamp(roll100, 0, 99);

            if (eventId == ExpeditionEventIds.MagicSpring && choiceIndex == 0)
            {
                if (roll100 < 60)
                    return "泉水如温暖的光流过全身，全队的伤势迅速愈合。";

                if (roll100 < 85)
                    return "泉水迸发出奇异的力量注入肌肉，一名角色感到自己变得更强。";

                return "泉水突然变得灼热刺骨，全队被烫伤后退。";
            }

            if (eventId == ExpeditionEventIds.GamblerDice && choiceIndex == 0)
            {
                return roll100 < 50
                    ? "骰子定格在幸运数字上，矮人笑着退还更多金币。"
                    : "骰子滚落到角落，矮人摇头收走了你的赌注。";
            }

            if (eventId == ExpeditionEventIds.GamblerDice && choiceIndex == 1)
            {
                if (roll100 < 40)
                    return "骰子连续翻转后稳稳立住，矮人惊讶地将双倍金币推还给你。";

                if (roll100 < 70)
                    return "骰子滚出桌面消失不见，矮人耸肩收走了你所有的金币。";

                return "你输了，矮人收走了你的金币，却从怀里掏出一件神秘物品作为补偿。";
            }

            if (eventId == ExpeditionEventIds.AncientFurnace && choiceIndex == 2)
            {
                if (roll100 < 40)
                    return "一只石傀儡从熔渣中苏醒并发起攻击。";

                if (roll100 < 70)
                    return "你在炉灰中翻找出一件遗物。";

                return "你仔细搜索了一番，但什么也没找到。";
            }

            return "";
        }
    }
}
