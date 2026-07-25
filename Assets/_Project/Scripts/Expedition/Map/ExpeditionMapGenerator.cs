using System.Collections.Generic;
using Grimhand.Core;
using Grimhand.Expedition;
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
                var isBoss = layer == layerCount || ExpeditionRegionRules.IsMandatoryBossLayer(layer);
                var row = new ExpeditionMapLayer { LayerNumber = layer, IsBoss = isBoss };
                if (row.IsBoss)
                {
                    row.Options.Add(CreateBossOption(layer));
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
                EnsureUniqueNodeTypes(types, layer, state, rng);

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
                EnsureUniqueNodeTypes(types, row.LayerNumber, state, rng);
                for (var i = 0; i < types.Count; i++)
                {
                    if (row.Options[i].NodeType == types[i])
                        continue;

                    row.Options[i].NodeType = types[i];
                    row.Options[i].IsElite = types[i] == ExpeditionNodeType.Elite;
                    if (types[i] is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite)
                        AssignMonsterEncounter(row.Options[i], row.LayerNumber, rng);
                    FillOptionMeta(row.Options[i], row.LayerNumber, i, config, run, rng);
                }
            }

            return map;
        }

        public static void ForceBossLayer(ExpeditionMapState map, int layerNumber)
        {
            var layer = map?.GetLayer(layerNumber);
            if (layer == null)
                return;

            layer.IsBoss = true;
            layer.Options.Clear();
            layer.Options.Add(CreateBossOption(layerNumber));
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
            {
                if (!TryPickUniqueNodeType(result, layer, state, rng, out var filler))
                    break;

                result.Add(filler);
            }

            return result;
        }

        static bool TryPickUniqueNodeType(
            List<ExpeditionNodeType> existing,
            int layer,
            GenState state,
            BattleRng rng,
            out ExpeditionNodeType type)
        {
            var pool = BuildPool(layer, state);
            for (var attempt = 0; attempt < pool.Count; attempt++)
            {
                var pick = pool[rng.NextIndex(pool.Count)];
                if (existing.Contains(pick))
                    continue;

                type = pick;
                return true;
            }

            foreach (ExpeditionNodeType candidate in System.Enum.GetValues(typeof(ExpeditionNodeType)))
            {
                if (candidate == ExpeditionNodeType.Boss)
                    continue;
                if (candidate == ExpeditionNodeType.Elite && layer < 4)
                    continue;
                if (existing.Contains(candidate))
                    continue;

                type = candidate;
                return true;
            }

            type = ExpeditionNodeType.Combat;
            return !existing.Contains(type);
        }

        static void EnsureUniqueNodeTypes(
            List<ExpeditionNodeType> types,
            int layer,
            GenState state,
            BattleRng rng)
        {
            if (types == null || types.Count <= 1)
                return;

            for (var i = 0; i < types.Count; i++)
            {
                for (var j = i + 1; j < types.Count; j++)
                {
                    if (types[i] != types[j])
                        continue;

                    if (!TryPickUniqueNodeType(types, layer, state, rng, out var replacement))
                        replacement = ExpeditionNodeType.Event;

                    types[j] = replacement;
                }
            }
        }

        static bool RowContainsType(ExpeditionMapLayer row, ExpeditionNodeType type)
        {
            foreach (var option in row.Options)
            {
                if (option.NodeType == type)
                    return true;
            }

            return false;
        }

        static List<ExpeditionNodeType> BuildPool(int layer, GenState state)
        {
            var pool = new List<ExpeditionNodeType>();
            var stage = layer <= 7 ? 0 : layer <= 14 ? 1 : 2;

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
            if (layer is >= 3 and <= 10 && !state.MerchantPlaced && !types.Contains(ExpeditionNodeType.Shop))
            {
                types[rng.NextIndex(types.Count)] = ExpeditionNodeType.Shop;
                state.MerchantPlaced = true;
            }

            if (layer >= 4 && layer <= layerCount - 2 && !state.ElitePlaced && !types.Contains(ExpeditionNodeType.Elite))
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
                if (row == null || row.IsBoss || row.Options.Count == 0)
                    continue;

                if (!state.MerchantPlaced && layer is >= 3 and <= 10)
                {
                    if (!RowContainsType(row, ExpeditionNodeType.Shop))
                    {
                        var idx = rng.NextIndex(row.Options.Count);
                        row.Options[idx].NodeType = ExpeditionNodeType.Shop;
                        FillOptionMeta(row.Options[idx], layer, idx, null, null, rng);
                    }

                    state.MerchantPlaced = true;
                }

                if (!state.ElitePlaced && layer >= 4 && layer <= map.ChapterLayerCount - 2)
                {
                    if (!RowContainsType(row, ExpeditionNodeType.Elite))
                    {
                        var idx = rng.NextIndex(row.Options.Count);
                        row.Options[idx].NodeType = ExpeditionNodeType.Elite;
                        row.Options[idx].IsElite = true;
                        AssignMonsterEncounter(row.Options[idx], layer, rng);
                        FillOptionMeta(row.Options[idx], layer, idx, null, null, rng);
                    }

                    state.ElitePlaced = true;
                }

                if (row.Options.Count >= 2)
                {
                    var types = new List<ExpeditionNodeType>();
                    foreach (var option in row.Options)
                        types.Add(option.NodeType);

                    EnsureUniqueNodeTypes(types, layer, state, rng);
                    for (var i = 0; i < types.Count; i++)
                    {
                        if (row.Options[i].NodeType == types[i])
                            continue;

                        row.Options[i].NodeType = types[i];
                        row.Options[i].IsElite = types[i] == ExpeditionNodeType.Elite;
                        if (types[i] is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite)
                            AssignMonsterEncounter(row.Options[i], layer, rng);
                        FillOptionMeta(row.Options[i], layer, i, null, null, rng);
                    }
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

            if (type is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite)
                AssignMonsterEncounter(option, layer, rng);

            FillOptionMeta(option, layer, index, config, run, rng);
            return option;
        }

        static void AssignMonsterEncounter(ExpeditionMapOption option, int layer, BattleRng rng)
        {
            if (option == null || rng == null)
                return;

            option.IsElite = option.NodeType == ExpeditionNodeType.Elite;
            option.MonsterEncounterId = MonsterEncounterCatalog.Roll(layer, option.IsElite, rng);
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
                case ExpeditionNodeType.Boss:
                    option.DisplayName = "守关 Boss";
                    option.Description = ExpeditionRegionRules.IsMandatoryBossLayer(layer)
                        ? $"第 {layer} 层守关 Boss：唯一通路。"
                        : $"第 {layer} 层终局守关。";
                    break;
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
                    option.EventId = ExpeditionEventRoller.PickEventId(run, rng);
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

        static ExpeditionMapOption CreateBossOption(int layer) =>
            new()
            {
                NodeType = ExpeditionNodeType.Boss,
                DisplayName = "守关 Boss",
                Description = ExpeditionRegionRules.IsMandatoryBossLayer(layer)
                    ? $"第 {layer} 层守关 Boss：唯一通路。"
                    : $"第 {layer} 层终局守关。",
                PathSpriteIndex = 0,
                EncounterIndex = 0
            };

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
