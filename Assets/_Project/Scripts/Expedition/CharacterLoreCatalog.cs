using System.Collections.Generic;

namespace Grimhand.Expedition
{
    /// <summary>玩家角色图鉴背景故事（图书馆「玩家角色」页）。</summary>
    public static class CharacterLoreCatalog
    {
        public readonly struct Entry
        {
            public readonly string TitleLine;
            public readonly string Story;

            public Entry(string titleLine, string story)
            {
                TitleLine = titleLine ?? "";
                Story = story ?? "";
            }
        }

        static readonly Dictionary<string, Entry> ById = new()
        {
            [TalentCatalog.KnightId] = new(
                "战士 —— 艾德里克，最后的城墙骑士",
                "她曾是王国王卫队的末席骑士。在大崩落之夜，她奉命保护图书馆，却只能眼睁睁看着《创世叙事》被撕裂。她捡起了一块嵌着“守护”二字的卡牌碎片，以此活了下来。她从不谈及过去，只会在每次远征前擦拭盾牌，低声说一句话：“这一页，我不会再让你撕掉。”"),
            [TalentCatalog.MageId] = new(
                "法老 —— 塞赫美特，窃取日光的祭司",
                "她是沙漠王朝最后一位太阳祭司。王朝覆灭时，她将神殿穹顶的最后一缕日光封入权杖，以此换得逃亡的机会。她的权杖既是武器，也是一座移动的日晷——她相信，只要还能测量时间，世界就还没有完全死去。她的言语总是充满预言般的晦涩，因为她早已习惯在“光明”与“灰烬”的夹缝中行走。"),
            [TalentCatalog.RangerId] = new(
                "恶魔 —— 摩洛克，自焚的复仇者",
                "她曾是王国边境的一名小领主，因拒绝向“黑暗之手”献出领地上的灰烬之核，被剥夺真名，放逐到了地底深处。她的身体是一具燃烧血肉才能驱动的空壳。她话很少，笑的时候会从喉咙里溢出火星。她并非为了荣耀或救赎而战——她只想找到那个夺走她名字的东西，然后问它：你还记得我叫什么吗？"),
            [TalentCatalog.SnakeQueenId] = new(
                "毒蛇女王 —— 希尔德，剧毒花园的末裔",
                "她是蛇人族最后一位女王。她的族群在灰烬侵蚀下逐渐石化，临终前，所有族人将残留的毒液注入她的心脏，用古老的祈福让她活了下来，却也让她成为了一座活着的毒泉。她行走时衣摆会腐蚀地面，触碰过的物体会慢慢枯萎。她把每一次战斗称为“投毒”，因为在她看来，敌人从遇到她的那一刻起，就已经开始死亡了。"),
            [TalentCatalog.LichQueenId] = new(
                "巫妖女王 —— 伊索尔德，撕书之人",
                "她曾是封印图书馆的首席学者，也是百年前亲手撕下《创世叙事》最后一页的那个人。那一刻，她看到了所有的因果——包括自己的结局。为了赎罪，她在死亡前将自己转化为巫妖，把灵魂拆解成无数碎片，封入每一张她亲手制作的卡牌中。她早已记不清自己的名字，却清晰记得那场实验的每一个细节——因为那是她想要抹去的唯一一件事。"),
        };

        public static bool TryGet(string characterId, out Entry entry)
        {
            if (!string.IsNullOrEmpty(characterId) && ById.TryGetValue(characterId, out entry))
                return true;

            entry = default;
            return false;
        }

        public static string GetTooltipTitle(string characterId, string fallbackName)
        {
            if (TryGet(characterId, out var entry) && !string.IsNullOrEmpty(entry.TitleLine))
                return entry.TitleLine;
            return fallbackName ?? "";
        }

        public static string GetTooltipBody(string characterId, string lockedFallback = "尚未拥有该角色。")
        {
            if (TryGet(characterId, out var entry) && !string.IsNullOrEmpty(entry.Story))
                return entry.Story;
            return lockedFallback;
        }
    }
}
