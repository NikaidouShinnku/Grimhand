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
        public string MonsterEncounterId { get; set; } = "";
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
        public const int DefaultChapterLayerCount = 20;

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

    public sealed class ExpeditionPendingEventAftermath
    {
        public string EventId { get; set; } = "";
        public int ChoiceIndex { get; set; }
        public int SourceLayer { get; set; }
        public string AfterChoiceText { get; set; } = "";
        /// <summary>概率事件：选选项时掷一次，确认时用同一结果结算。</summary>
        public int? FixedRoll100 { get; set; }
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
        public int HandLimitBonus { get; set; }
        /// <summary>祭坛「抽牌数量」升级档位（基础 5，+1×3 至 8）。</summary>
        public int DrawPerTurnBonus { get; set; }
        public int AltarHpPlus5Purchases { get; set; }
        public int AltarHpPlus10Purchases { get; set; }
        public bool NextCombatEnemyAttackBonus { get; set; }
        public int ForeseenLayerCount { get; set; }
        public bool SkipNextRouteSelect { get; set; }
        public bool LootedInjuredAdventurer { get; set; }
        public bool DivinePunishmentActive { get; set; }
        /// <summary>灵魂裂隙：每场战斗开始随机 1 名队员失去 MaxHp 的该百分比（至少 1）。</summary>
        public int SoulRiftBattleStartRandomHpLoss { get; set; }
    }

    public sealed class ConsumableStack
    {
        public string ConsumableId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Count { get; set; }
    }
}
