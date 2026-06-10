using System.Collections.Generic;
using Grimhand.Core;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Map
{
    public static class ExpeditionMapGenerator
    {
        struct GenState
        {
            public bool MerchantPlaced;
            public bool ElitePlaced;
            public bool TreasureOrShrinePlaced;
            public int ConsecutiveCombatLayers;
        }

        public static ExpeditionMapState Generate(ExpeditionConfig config, ExpeditionRunState run, BattleRng rng)
        {
            var layerCount = config.ChapterLayerCount > 0
                ? config.ChapterLayerCount
                : ExpeditionMapState.DefaultChapterLayerCount;

            var map = new ExpeditionMapState { ChapterLayerCount = layerCount };
            var state = new GenState();

            for (var layer = 1; layer <= layerCount; layer++)
            {
                var row = new ExpeditionMapLayer { LayerNumber = layer, IsBoss = layer == layerCount };
                if (row.IsBoss)
                {
                    row.Options.Add(CreateBossOption());
                    map.Layers.Add(row);
                    continue;
                }

                if (layer == 1)
                {
                    row.Options.Add(CreateOption(ExpeditionNodeType.Combat, layer, 0, config, run, rng));
                    UpdateCombatStreak(new List<ExpeditionNodeType> { ExpeditionNodeType.Combat }, ref state);
                    map.Layers.Add(row);
                    continue;
                }

                var optionCount = RollOptionCount(layer, rng);
                var types = RollNodeTypes(layer, optionCount, state, rng);
                ApplyGuarantees(layer, layerCount, types, state, rng);
                EnsureCombatOptionIfNeeded(types, rng);

                for (var i = 0; i < types.Count; i++)
                    row.Options.Add(CreateOption(types[i], layer, i, config, run, rng));

                UpdateCombatStreak(types, ref state);
                map.Layers.Add(row);
            }

            ApplyGuaranteeBackfill(map, state, rng);
            foreach (var row in map.Layers)
            {
                if (row.IsBoss || row.Options.Count < 3)
                    continue;

                var types = new List<ExpeditionNodeType>();
                foreach (var option in row.Options)
                    types.Add(option.NodeType);

                EnsureCombatOptionIfNeeded(types, rng);
                for (var i = 0; i < types.Count; i++)
                {
                    if (row.Options[i].NodeType == types[i])
                        continue;

                    row.Options[i].NodeType = types[i];
                    row.Options[i].IsElite = types[i] == ExpeditionNodeType.Elite;
                    FillOptionMeta(row.Options[i], row.LayerNumber, i, config, run, rng);
                }
            }

            return map;
        }

        static int RollOptionCount(int layer, BattleRng rng)
        {
            if (layer == 1)
                return 1;

            return rng.NextInt(2, 5);
        }

        static void EnsureCombatOptionIfNeeded(List<ExpeditionNodeType> types, BattleRng rng)
        {
            if (types == null || types.Count < 3)
                return;

            foreach (var type in types)
            {
                if (type is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite)
                    return;
            }

            types[rng.NextIndex(types.Count)] = ExpeditionNodeType.Combat;
        }

        static List<ExpeditionNodeType> RollNodeTypes(int layer, int count, GenState state, BattleRng rng)
        {
            var result = new List<ExpeditionNodeType>();
            var pool = BuildPool(layer, state);

            while (result.Count < count && pool.Count > 0)
            {
                var pick = pool[rng.NextIndex(pool.Count)];
                pool.Remove(pick);
                if (result.Contains(pick))
                    continue;

                result.Add(pick);
            }

            while (result.Count < count)
                result.Add(ExpeditionNodeType.Combat);

            return result;
        }

        static List<ExpeditionNodeType> BuildPool(int layer, GenState state)
        {
            var pool = new List<ExpeditionNodeType>();
            var stage = layer <= 3 ? 0 : layer <= 6 ? 1 : 2;

            AddWeighted(pool, ExpeditionNodeType.Combat, stage == 0 ? 45 : stage == 1 ? 35 : 25);
            if (layer >= 4)
                AddWeighted(pool, ExpeditionNodeType.Elite, stage == 1 ? 12 : 18);

            AddWeighted(pool, ExpeditionNodeType.Event, 20);
            AddWeighted(pool, ExpeditionNodeType.Treasure, 8);
            AddWeighted(pool, ExpeditionNodeType.Shrine, 5);
            AddWeighted(pool, ExpeditionNodeType.Shop, stage == 0 ? 15 : stage == 1 ? 15 : 12);

            if (state.ConsecutiveCombatLayers >= 2)
            {
                pool.RemoveAll(t => t == ExpeditionNodeType.Combat || t == ExpeditionNodeType.Elite);
                if (pool.Count == 0)
                    pool.Add(ExpeditionNodeType.Event);
            }

            return pool;
        }

        static void AddWeighted(List<ExpeditionNodeType> pool, ExpeditionNodeType type, int weight)
        {
            for (var i = 0; i < weight; i++)
                pool.Add(type);
        }

        static void ApplyGuarantees(int layer, int layerCount, List<ExpeditionNodeType> types, GenState state, BattleRng rng)
        {
            if (layer is >= 3 and <= 7 && !state.MerchantPlaced && !types.Contains(ExpeditionNodeType.Shop))
            {
                types[rng.NextIndex(types.Count)] = ExpeditionNodeType.Shop;
                state.MerchantPlaced = true;
            }

            if (layer is >= 4 and <= 9 && !state.ElitePlaced && !types.Contains(ExpeditionNodeType.Elite))
            {
                types[rng.NextIndex(types.Count)] = ExpeditionNodeType.Elite;
                state.ElitePlaced = true;
            }

            if (layer <= layerCount - 1 && !state.TreasureOrShrinePlaced &&
                !types.Contains(ExpeditionNodeType.Treasure) && !types.Contains(ExpeditionNodeType.Shrine))
            {
                types[rng.NextIndex(types.Count)] =
                    rng.NextInt(0, 2) == 0 ? ExpeditionNodeType.Treasure : ExpeditionNodeType.Shrine;
                state.TreasureOrShrinePlaced = true;
            }
        }

        static void ApplyGuaranteeBackfill(ExpeditionMapState map, GenState state, BattleRng rng)
        {
            for (var layer = 1; layer < map.ChapterLayerCount; layer++)
            {
                var row = map.GetLayer(layer);
                if (row == null || row.Options.Count == 0)
                    continue;

                if (!state.MerchantPlaced && layer is >= 3 and <= 7)
                {
                    row.Options[0].NodeType = ExpeditionNodeType.Shop;
                    FillOptionMeta(row.Options[0], layer, 0, null, null, rng);
                    state.MerchantPlaced = true;
                }

                if (!state.ElitePlaced && layer is >= 4 and <= 9)
                {
                    var idx = System.Math.Min(1, row.Options.Count - 1);
                    row.Options[idx].NodeType = ExpeditionNodeType.Elite;
                    row.Options[idx].IsElite = true;
                    FillOptionMeta(row.Options[idx], layer, idx, null, null, rng);
                    state.ElitePlaced = true;
                }
            }
        }

        static void UpdateCombatStreak(List<ExpeditionNodeType> types, ref GenState state)
        {
            var hasCombat = false;
            foreach (var type in types)
            {
                if (type is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite)
                    hasCombat = true;
            }

            state.ConsecutiveCombatLayers = hasCombat ? state.ConsecutiveCombatLayers + 1 : 0;
        }

        static ExpeditionMapOption CreateOption(
            ExpeditionNodeType type,
            int layer,
            int index,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            var option = new ExpeditionMapOption
            {
                NodeType = type,
                PathSpriteIndex = ExpeditionRewardRoller.RollPathSpriteIndex(rng),
                EncounterIndex = config.CombatEncounters.Count > 0
                    ? rng.NextIndex(config.CombatEncounters.Count)
                    : 0,
                IsElite = type == ExpeditionNodeType.Elite
            };
            FillOptionMeta(option, layer, index, config, run, rng);
            return option;
        }

        static void FillOptionMeta(
            ExpeditionMapOption option,
            int layer,
            int index,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            switch (option.NodeType)
            {
                case ExpeditionNodeType.Combat:
                    option.DisplayName = Pick(ExpeditionNodeNames.Combat, layer, index);
                    option.Description = "普通战斗：获得经验与金币。";
                    break;
                case ExpeditionNodeType.Elite:
                    option.DisplayName = Pick(ExpeditionNodeNames.Elite, layer, index);
                    option.Description = "精英战斗：更高难度，遗物奖励。";
                    option.IsElite = true;
                    break;
                case ExpeditionNodeType.Treasure:
                    option.TreasureTier = RollTreasureTier(rng);
                    option.DisplayName = option.TreasureTier switch
                    {
                        ExpeditionTreasureTiers.Fancy => "华丽宝箱",
                        ExpeditionTreasureTiers.Cursed => "诅咒宝箱",
                        _ => "普通木箱"
                    };
                    option.Description = "开箱获得卡牌、金币或消耗品。";
                    break;
                case ExpeditionNodeType.Event:
                    option.EventId = PickEventId(run, rng);
                    option.DisplayName = Pick(ExpeditionNodeNames.MysteryPath, layer, index);
                    option.Description = ExpeditionRouteCopy.MysteryPathDescription;
                    break;
                case ExpeditionNodeType.Shop:
                    option.DisplayName = Pick(ExpeditionNodeNames.Shop, layer, index);
                    option.Description = "购买卡牌、删牌或治疗。";
                    break;
                case ExpeditionNodeType.Shrine:
                    option.ShrineId = PickShrineId(rng);
                    option.DisplayName = option.ShrineId switch
                    {
                        ExpeditionShrineIds.Knowledge => "知识祭坛",
                        ExpeditionShrineIds.Soul => "灵魂祭坛",
                        ExpeditionShrineIds.Chaos => "混沌祭坛",
                        _ => "血之祭坛"
                    };
                    option.Description = "献祭换取奖励，也可安全离开。";
                    break;
            }
        }

        static ExpeditionMapOption CreateBossOption() =>
            new()
            {
                NodeType = ExpeditionNodeType.Boss,
                DisplayName = "守关 Boss",
                Description = "终层守关者：骷髅王或幽灵女王。",
                PathSpriteIndex = 0,
                EncounterIndex = 0
            };

        static string PickEventId(ExpeditionRunState run, BattleRng rng)
        {
            var pool = new List<string>();
            foreach (var evt in ExpeditionEventCatalog.All)
            {
                if (run.UsedEventIds.Contains(evt.Id))
                    continue;

                if (!string.IsNullOrEmpty(evt.PrerequisiteFlag) &&
                    !run.EventFlags.Contains(evt.PrerequisiteFlag))
                    continue;

                if (!string.IsNullOrEmpty(evt.RequiredRelicId) &&
                    !run.Relics.Contains(evt.RequiredRelicId))
                    continue;

                if (evt.RequiresDemonInParty && !PartyHasCharacter(run, "char_ranger"))
                    continue;

                if (evt.MinGold > 0 && run.Gold < evt.MinGold)
                    continue;

                pool.Add(evt.Id);
            }

            if (pool.Count == 0)
                return ExpeditionEventIds.MysteriousTraveler;

            return pool[rng.NextIndex(pool.Count)];
        }

        static bool PartyHasCharacter(ExpeditionRunState run, string charId)
        {
            foreach (var member in run.Party)
            {
                if (member.CharacterDefinitionId == charId)
                    return true;
            }

            return false;
        }

        static string PickShrineId(BattleRng rng)
        {
            var roll = rng.NextIndex(100);
            if (roll < 35) return ExpeditionShrineIds.Blood;
            if (roll < 60) return ExpeditionShrineIds.Knowledge;
            if (roll < 80) return ExpeditionShrineIds.Soul;
            return ExpeditionShrineIds.Chaos;
        }

        static string RollTreasureTier(BattleRng rng)
        {
            var roll = rng.NextIndex(100);
            if (roll < 50) return ExpeditionTreasureTiers.Common;
            if (roll < 85) return ExpeditionTreasureTiers.Fancy;
            return ExpeditionTreasureTiers.Cursed;
        }

        static string Pick(string[] pool, int layer, int index) =>
            pool[(layer + index) % pool.Length];
    }

    static class ExpeditionNodeNames
    {
        public static readonly string[] Combat =
        {
            "哥布林哨站", "暗影通道", "断裂石桥", "低语深坑", "蛮兵营地", "腐化洞窟"
        };

        public static readonly string[] Elite =
        {
            "精英战阵", "骸骨要塞", "幽魂祭坛", "堕落骑士厅"
        };

        public static readonly string[] Shop =
        {
            "流浪商人", "黑市帐篷", "旅途驿站"
        };

        /// <summary>选路时不揭示节点类型（事件等）用的洞窟名。</summary>
        public static readonly string[] MysteryPath =
        {
            "暗影通道", "断裂石桥", "低语深坑", "腐化洞窟", "迷雾岔路", "坍塌矿道"
        };
    }
}
