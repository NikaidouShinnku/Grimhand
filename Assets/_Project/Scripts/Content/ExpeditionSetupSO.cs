using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "ExpeditionSetup", menuName = "Grimhand/Expedition Setup")]
    public class ExpeditionSetupSO : ScriptableObject
    {
        [Tooltip("远征随机种子；0 表示每次开局随机。每场战斗仍单独随机种子。")]
        public int RunSeed = 0;

        [Tooltip("大关层数（顶层为 Boss）。")]
        public int ChapterLayerCount = 10;

        [Tooltip("通关所需节点数（不含起始选路；默认等于层数-1）。")]
        public int TargetBattleCount = 9;

        [Tooltip("每场胜利后提供的路线数量。")]
        public int RoutesPerVictory = 3;

        [Tooltip("普通战斗胜利金币下限（含）。")]
        public int GoldMinPerVictory = 15;

        [Tooltip("普通战斗胜利金币上限（含）。")]
        public int GoldMaxPerVictory = 25;

        [Tooltip("普通战斗胜利每场 XP（每名存活角色）。")]
        public int XpPerVictory = 16;

        [Header("战后奖励")]
        [Range(0, 100)] public int RelicDropChancePercent = 25;
        [Range(0, 100)] public int CardDropChancePercent = 25;

        [Header("路线权重（战斗 / 宝箱）")]
        public int CombatRouteWeight = 55;
        public int TreasureRouteWeight = 45;

        [Header("宝箱房间")]
        public int TreasureGoldMin = 20;
        public int TreasureGoldMax = 35;
        [Range(0, 100)] public int TreasureRelicChancePercent = 15;
        [Tooltip("宝箱在「卡牌 / 遗物」二选一 roll 中，出卡牌的概率（默认 60%）。")]
        [Range(0, 100)] public int TreasureCardChancePercent = 60;
        [Range(0, 100)] public int TreasureConsumableChancePercent = 33;

        [Tooltip("普通战斗遭遇；Demo 可只填一张并复用。")]
        public List<BattleSetupSO> CombatEncounters = new();

        [Tooltip("Boss 遭遇（第 10 层等）；留空则 Boss 路线回退为 CombatEncounters[0]。")]
        public List<BattleSetupSO> BossEncounters = new();

        [Tooltip("商店/奖励可 roll 的全部玩家卡牌（含非初始牌组）；留空则回退为遭遇配置中的初始牌。")]
        public List<CardDefinitionSO> PlayerCardCatalog = new();

        public ExpeditionConfig ToExpeditionConfig()
        {
            var runSeed = RunSeed;
            if (runSeed <= 0)
                runSeed = Random.Range(1, int.MaxValue);

            var config = new ExpeditionConfig
            {
                RunSeed = runSeed,
                ChapterLayerCount = ChapterLayerCount,
                TargetBattleCount = TargetBattleCount,
                RoutesPerVictory = RoutesPerVictory,
                GoldMinPerVictory = GoldMinPerVictory,
                GoldMaxPerVictory = GoldMaxPerVictory,
                XpPerVictory = XpPerVictory,
                RelicDropChancePercent = RelicDropChancePercent,
                CardDropChancePercent = CardDropChancePercent,
                CombatRouteWeight = CombatRouteWeight,
                TreasureRouteWeight = TreasureRouteWeight,
                TreasureGoldMin = TreasureGoldMin,
                TreasureGoldMax = TreasureGoldMax,
                TreasureRelicChancePercent = TreasureRelicChancePercent,
                TreasureCardChancePercent = TreasureCardChancePercent,
                TreasureConsumableChancePercent = TreasureConsumableChancePercent
            };

            foreach (var encounter in CombatEncounters)
            {
                if (encounter == null)
                    continue;

                config.CombatEncounters.Add(encounter.ToBattleConfig());
            }

            foreach (var encounter in BossEncounters)
            {
                if (encounter == null)
                    continue;

                config.BossEncounters.Add(encounter.ToBattleConfig());
            }

            foreach (var card in PlayerCardCatalog)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                config.PlayerCardCatalog.Add(card.ToTemplate());
            }

            return config;
        }
    }
}
