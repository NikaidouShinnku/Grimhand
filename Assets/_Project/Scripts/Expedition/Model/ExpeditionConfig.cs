using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Model
{
    public sealed class ExpeditionConfig
    {
        public int RunSeed { get; set; } = 42;
        public int ChapterLayerCount { get; set; } = 10;
        public int TargetBattleCount { get; set; } = 9;
        public int RoutesPerVictory { get; set; } = 3;

        public int GoldMinPerVictory { get; set; } = 15;

        public int GoldMaxPerVictory { get; set; } = 25;

        public int XpPerVictory { get; set; } = 16;

        public int RelicDropChancePercent { get; set; } = 25;
        public int CardDropChancePercent { get; set; } = 25;

        public int TreasureGoldMin { get; set; } = 20;
        public int TreasureGoldMax { get; set; } = 35;
        public int TreasureRelicChancePercent { get; set; } = 15;
        public int TreasureCardChancePercent { get; set; } = 60;
        public int TreasureConsumableChancePercent { get; set; } = 33;

        public int CombatRouteWeight { get; set; } = 55;
        public int TreasureRouteWeight { get; set; } = 45;

        public List<BattleConfig> CombatEncounters { get; } = new();
        public List<CardTemplate> PlayerCardCatalog { get; } = new();
    }
}
