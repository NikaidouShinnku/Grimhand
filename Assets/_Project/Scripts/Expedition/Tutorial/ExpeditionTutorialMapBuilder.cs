using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Tutorial
{
    /// <summary>新手教程固定 6 节点迷你地图。</summary>
    public static class ExpeditionTutorialMapBuilder
    {
        public const int LayerCount = 6;

        public static ExpeditionMapState Build()
        {
            var map = new ExpeditionMapState { ChapterLayerCount = LayerCount };
            map.Layers.Add(Layer(1, CombatOption()));
            map.Layers.Add(Layer(2, TreasureOption()));
            map.Layers.Add(Layer(3, EventOption()));
            map.Layers.Add(Layer(4, ShopOption()));
            map.Layers.Add(Layer(5, EliteOption()));
            map.Layers.Add(Layer(6, AltarOption()));
            return map;
        }

        static ExpeditionMapLayer Layer(int number, ExpeditionMapOption option)
        {
            var layer = new ExpeditionMapLayer
            {
                LayerNumber = number,
                IsBoss = false,
                IsRevealed = true
            };
            layer.Options.Add(option);
            return layer;
        }

        static ExpeditionMapOption CombatOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Combat,
                DisplayName = "教学遭遇",
                Description = "哥布林与史莱姆。学习能量、出牌与选敌。",
                PathSpriteIndex = 0,
                MonsterEncounterId = MonsterEncounterCatalog.TutorialGoblinSlime,
                IsElite = false
            };

        static ExpeditionMapOption TreasureOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Treasure,
                DisplayName = "教学宝箱",
                Description = "打开宝箱，获得遗物与消耗品。",
                PathSpriteIndex = 1,
                TreasureTier = "common"
            };

        static ExpeditionMapOption EventOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Event,
                DisplayName = "魔法泉水",
                Description = "固定事件：魔法泉水。",
                PathSpriteIndex = 2,
                EventId = ExpeditionEventIds.MagicSpring
            };

        static ExpeditionMapOption ShopOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Shop,
                DisplayName = "旅行商人",
                Description = "花费金币购买卡包、遗物或消耗品。",
                PathSpriteIndex = 3
            };

        static ExpeditionMapOption EliteOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Elite,
                DisplayName = "骷髅精英",
                Description = "精英战：学习应对攻击与使用消耗品。",
                PathSpriteIndex = 4,
                MonsterEncounterId = MonsterEncounterCatalog.TutorialSkeletonElite,
                IsElite = true
            };

        static ExpeditionMapOption AltarOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Shrine,
                DisplayName = "祭坛",
                Description = "学习刻印：把卡牌带入收藏。",
                PathSpriteIndex = 0
            };
    }
}
