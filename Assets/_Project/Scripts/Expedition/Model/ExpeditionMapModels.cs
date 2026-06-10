using System.Collections.Generic;

namespace Grimhand.Expedition.Model
{
    public sealed class ExpeditionMapOption
    {
        public ExpeditionNodeType NodeType { get; set; }
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public int PathSpriteIndex { get; set; }
        public int EncounterIndex { get; set; }
        public string EventId { get; set; } = "";
        public string ShrineId { get; set; } = "";
        public string TreasureTier { get; set; } = "";
        public bool IsElite { get; set; }
    }

    public sealed class ExpeditionMapLayer
    {
        public int LayerNumber { get; set; }
        public bool IsBoss { get; set; }
        public bool IsRevealed { get; set; }
        public List<ExpeditionMapOption> Options { get; } = new();
        public int? ChosenOptionIndex { get; set; }
    }

    public sealed class ExpeditionMapState
    {
        public const int DefaultChapterLayerCount = 10;

        public int ChapterLayerCount { get; set; } = DefaultChapterLayerCount;
        public List<ExpeditionMapLayer> Layers { get; } = new();
        public int NodesCompleted { get; set; }

        public ExpeditionMapLayer GetLayer(int layerNumber)
        {
            foreach (var layer in Layers)
            {
                if (layer.LayerNumber == layerNumber)
                    return layer;
            }

            return null;
        }

        public ExpeditionMapLayer CurrentChoiceLayer
        {
            get
            {
                var layerNumber = NodesCompleted + 1;
                if (layerNumber > ChapterLayerCount)
                    return null;

                return GetLayer(layerNumber);
            }
        }
    }

    public sealed class ExpeditionPendingEvent
    {
        public string EventId { get; set; } = "";
        public int SourceLayer { get; set; }
    }

    public sealed class ExpeditionPendingShrine
    {
        public string ShrineId { get; set; } = "";
        public int SourceLayer { get; set; }
    }

    public sealed class ExpeditionRunModifiers
    {
        public int TeamAttackBonus { get; set; }
        public int TeamDefenseBonus { get; set; }
        public int EnergyCapBonus { get; set; }
        public bool NextCombatEnemyAttackBonus { get; set; }
        public int ForeseenLayerCount { get; set; }
        public bool SkipNextRouteSelect { get; set; }
        public bool LootedInjuredAdventurer { get; set; }
        public bool DivinePunishmentActive { get; set; }
        /// <summary>灵魂裂隙：每场战斗开始随机 1 名队员失去 HP。</summary>
        public int SoulRiftBattleStartRandomHpLoss { get; set; }
    }

    public sealed class ConsumableStack
    {
        public string ConsumableId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Count { get; set; }
    }
}
