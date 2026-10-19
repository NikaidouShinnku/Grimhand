using System.Collections.Generic;
using System.Linq;
namespace Grimhand.Expedition
{
    public sealed class AchievementDefinition
    {
        public AchievementDefinition(int number,string id,string name,string description){Number=number;Id=id;Name=name;Description=description;}
        public int Number {get;}
        public string Id {get;}
        public string Name {get;}
        public string Description {get;}
    }
    public static class AchievementCatalog
    {
        public static readonly IReadOnlyList<AchievementDefinition> All = new AchievementDefinition[]
        {
            new(1, "ACH_DEFEAT_SKELETON_KING", "王座倾覆", "击败第20层的骷髅王。"),
            new(2, "ACH_DEFEAT_GHOST_QUEEN", "幽魂落幕", "击败第20层的幽灵女王。"),
            new(3, "ACH_DEFEAT_WARDEN", "越狱时刻", "击败第40层的典狱长。"),
            new(4, "ACH_DEFEAT_DARK_KNIGHT", "折断黑刃", "击败第40层的黑暗骑士。"),
            new(5, "ACH_DEFEAT_OCEAN_GODDESS", "深海弑神", "击败第60层的腐化海洋女神。"),
            new(6, "ACH_FINISH_EXPEDITION", "一段旅途", "完成一次远征。"),
            new(7, "ACH_FIRST_VICTORY", "初露锋芒", "首次赢得一场远征战斗。"),
            new(8, "ACH_ELITE_VICTORY", "硬骨头", "赢得一场精英战。"),
            new(9, "ACH_LAST_SURVIVOR", "绝处逢生", "至少两名角色参战，仅剩一名角色存活时赢得Boss战。"),
            new(10, "ACH_BOSS_ALL_ALIVE", "一个都不能少", "三名角色全部存活，赢得一场Boss战。"),
            new(11, "ACH_THREE_RESPONSES", "算无遗策", "同一回合成功应对三张不同的敌方攻击牌。"),
            new(12, "ACH_BOSS_NO_CONSUMABLES", "留有余地", "携带至少三个消耗品，且不使用任何消耗品，赢得一场Boss战。"),
            new(13, "ACH_THREE_POISON_KILLS", "毒入骨髓", "同一场战斗中，让中毒伤害击败至少三名敌人。"),
            new(14, "ACH_POISON_AND_BURN", "内外交困", "击败一名同时拥有中毒和灼烧的敌人。"),
            new(15, "ACH_THREE_KILLS_ONE_CARD", "一击清场", "使用一张攻击牌击败至少三名敌人。"),
            new(16, "ACH_FRONT_BACK_KILLS", "破阵而入", "同一回合内，击败至少一名前排敌人和一名后排敌人。"),
            new(17, "ACH_ALL_RESPOND", "轮番上阵", "三名角色各成功应对至少一次敌方攻击，并赢得该场战斗。"),
            new(18, "ACH_RETAIN_THREE_TURNS", "好牌不怕晚", "让同一张继承牌连续跨过三个回合结束仍留在手中，随后使用它。"),
            new(19, "ACH_RELIC_EVOLUTION", "蜕变之物", "首次完成一次遗物进化。"),
            new(20, "ACH_ORANGE_ENGRAVING", "刻入命运", "首次成功刻印一张橙色卡牌。"),
            new(21, "ACH_COLLECTION_30", "收藏家", "卡牌收藏中拥有至少30种不同的卡牌。"),
            new(22, "ACH_COLLECTION_FIVE_ORANGE", "橙光闪耀", "卡牌收藏中拥有至少五种不同的橙色卡牌。"),
            new(23, "ACH_CARD_MAX_UPGRADE", "千锤百炼", "将一张可升级卡牌提升至其最高等级。"),
            new(24, "ACH_FIVE_ENGRAVINGS", "铭刻之路", "累计成功刻印五张卡牌。"),
            new(25, "ACH_ONE_CHARACTER_MAX", "炉火纯青", "将任意一名角色的局外等级提升至满级。"),
            new(26, "ACH_ALL_CHARACTERS_MAX", "群星齐耀", "将六名角色的局外等级全部提升至满级。"),
        };
        public static bool Contains(string id)=>All.Any(a=>a.Id==id);
    }
}
