using System.Collections.Generic;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    public sealed class MonsterEncounterDefinition
    {
        public string Id { get; set; } = "";
        public string[] EnemyCharacterIds { get; set; } = System.Array.Empty<string>();
        public int MinFloor { get; set; } = 1;
        public int MaxFloor { get; set; } = 19;
        public bool IsElite { get; set; }
        public int Weight { get; set; } = 100;
        /// <summary>第 10 层前使用的权重；&lt;=0 表示与普通权重相同。</summary>
        public int PreTenWeight { get; set; }
        /// <summary>第 25 层前使用的权重（地牢）。</summary>
        public int PreTwentyFiveWeight { get; set; }
        /// <summary>第 30 层前使用的权重（地牢）。</summary>
        public int PreThirtyWeight { get; set; }
    }

    public static class MonsterEncounterCatalog
    {
        public const string GoblinTriple = "enc_goblin_triple";
        public const string GoblinSlimeSkeleton = "enc_goblin_slime_skeleton";
        public const string SlimeTriple = "enc_slime_triple";
        public const string SkeletonSlimeGoblin = "enc_skeleton_slime_goblin";
        public const string SkeletonWraithWraith = "enc_skeleton_wraith_wraith";
        public const string SkeletonSkeletonWraith = "enc_skeleton_skeleton_wraith";
        public const string SkeletonSkeletonElite = "enc_skeleton_skeleton_elite";
        public const string SkeletonWraithEliteWraith = "enc_skeleton_wraith_elite_wraith";
        public const string EliteSkeletonSandwich = "enc_elite_skeleton_sandwich";
        public const string EliteWraithSandwich = "enc_elite_wraith_sandwich";
        public const string EliteSkeletonWraithDuo = "enc_elite_skeleton_wraith_duo";
        public const string EliteSkeletonMix = "enc_elite_skeleton_mix";
        public const string EliteOgreSolo = "enc_elite_ogre_solo";
        public const string EliteSkeletonBat = "enc_elite_skeleton_bat";

        public const string DunSkeletonChainWraith = "enc_dun_skeleton_chain_wraith";
        public const string DunSkeletonBatWraith = "enc_dun_skeleton_bat_wraith";
        public const string DunRatWraithWraith = "enc_dun_rat_wraith_wraith";
        public const string DunRatBat = "enc_dun_rat_bat";
        public const string DunRatChain = "enc_dun_rat_chain";
        public const string DunSlimeEliteMix = "enc_dun_slime_elite_mix";
        public const string DunEliteSkelChainSkeleton = "enc_dun_elite_skel_chain_skeleton";
        public const string DunEliteWraithBatWraith = "enc_dun_elite_wraith_bat_wraith";
        public const string DunEliteSkeletonTriple = "enc_dun_elite_skeleton_triple";
        public const string DunEliteWraithTriple = "enc_dun_elite_wraith_triple";
        public const string DunBatDouble = "enc_dun_bat_double";
        public const string DunEliteRatRatEliteSkel = "enc_dun_elite_rat_rat_elite_skel";
        public const string DunEliteRatRatEliteWraith = "enc_dun_elite_rat_rat_elite_wraith";
        public const string DunEliteGargoyleDuo = "enc_dun_elite_gargoyle_duo";
        public const string DunEliteGargoyleSkeletonSpider = "enc_dun_elite_gargoyle_skeleton_spider";
        public const string DunEliteRatTriple = "enc_dun_elite_rat_triple";
        public const string DunEliteGargoyleGolem = "enc_dun_elite_gargoyle_golem";
        public const string DunEliteGolemSpider = "enc_dun_elite_golem_spider";

        static readonly List<MonsterEncounterDefinition> All = BuildAll();

        public static IReadOnlyList<MonsterEncounterDefinition> Entries => All;

        public static MonsterEncounterDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var entry in All)
            {
                if (entry.Id == id)
                    return entry;
            }

            return null;
        }

        public static string Roll(int floor, bool isElite, BattleRng rng)
        {
            var pool = new List<MonsterEncounterDefinition>();
            foreach (var entry in All)
            {
                if (entry.IsElite != isElite)
                    continue;
                if (floor < entry.MinFloor || floor > entry.MaxFloor)
                    continue;

                pool.Add(entry);
            }

            if (pool.Count == 0)
                return isElite ? EliteSkeletonSandwich : GoblinTriple;

            var total = 0;
            var weights = new int[pool.Count];
            for (var i = 0; i < pool.Count; i++)
            {
                weights[i] = ResolveWeight(pool[i], floor);
                total += weights[i];
            }

            if (total <= 0)
                return pool[0].Id;

            var roll = rng.NextInt(0, total);
            for (var i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                    return pool[i].Id;
            }

            return pool[pool.Count - 1].Id;
        }

        static int ResolveWeight(MonsterEncounterDefinition entry, int floor)
        {
            var weight = entry.Weight;
            if (floor < 30 && entry.PreThirtyWeight > 0)
                weight = entry.PreThirtyWeight;
            else if (floor < 25 && entry.PreTwentyFiveWeight > 0)
                weight = entry.PreTwentyFiveWeight;
            else if (floor < 10 && entry.PreTenWeight > 0)
                weight = entry.PreTenWeight;
            return weight;
        }

        public static string GetDisplayName(MonsterEncounterDefinition encounter)
        {
            if (encounter?.EnemyCharacterIds == null || encounter.EnemyCharacterIds.Length == 0)
                return "未知组合";

            return string.Join(" + ", encounter.EnemyCharacterIds);
        }

        static List<MonsterEncounterDefinition> BuildAll() =>
            new()
            {
                Def(GoblinTriple, false, 1, 5,
                    "char_goblin", "char_goblin", "char_goblin"),
                Def(GoblinSlimeSkeleton, false, 1, 9,
                    "char_goblin", "char_slime", "char_skeleton"),
                Def(SlimeTriple, false, 1, 9,
                    "char_slime", "char_slime", "char_slime"),
                Def(SkeletonSlimeGoblin, false, 1, 9,
                    "char_skeleton", "char_slime", "char_goblin"),
                Def(SkeletonWraithWraith, false, 5, 9,
                    "char_skeleton", "char_wraith", "char_wraith"),
                Def(SkeletonSkeletonWraith, false, 5, 9,
                    "char_skeleton", "char_skeleton", "char_wraith"),
                Def(SkeletonSkeletonElite, false, 5, 19, 100, 35, 0, 0,
                    "char_skeleton", "char_skeleton", "char_skeleton_elite"),
                Def(SkeletonWraithEliteWraith, false, 5, 19, 100, 35, 0, 0,
                    "char_skeleton", "char_wraith", "char_wraith_elite"),
                Def(EliteSkeletonSandwich, true, 1, 5,
                    "char_skeleton", "char_skeleton_elite", "char_skeleton"),
                Def(EliteWraithSandwich, true, 1, 5,
                    "char_wraith", "char_wraith_elite", "char_wraith"),
                Def(EliteSkeletonWraithDuo, true, 5, 9,
                    "char_skeleton_elite", "char_wraith_elite"),
                Def(EliteSkeletonMix, true, 5, 9,
                    "char_skeleton", "char_skeleton_elite", "char_wraith_elite"),
                Def(EliteOgreSolo, true, 10, 19,
                    "char_ogre"),
                Def(EliteSkeletonBat, true, 10, 19,
                    "char_skeleton", "char_bat"),

                Def(DunSkeletonChainWraith, false, 21, 29,
                    "char_skeleton", "char_chain_wraith", "char_wraith"),
                Def(DunSkeletonBatWraith, false, 21, 29,
                    "char_skeleton", "char_bat", "char_wraith"),
                Def(DunRatWraithWraith, false, 21, 29,
                    "char_rat", "char_wraith", "char_wraith"),
                Def(DunRatBat, false, 21, 29,
                    "char_rat", "char_bat"),
                Def(DunRatChain, false, 21, 29,
                    "char_rat", "char_chain_wraith"),
                Def(DunSlimeEliteMix, false, 21, 29,
                    "char_slime", "char_skeleton_elite", "char_wraith_elite"),
                Def(DunEliteSkelChainSkeleton, false, 25, 34,
                    "char_skeleton_elite", "char_chain_wraith", "char_skeleton"),
                Def(DunEliteWraithBatWraith, false, 25, 34,
                    "char_wraith_elite", "char_bat", "char_wraith"),
                Def(DunEliteSkeletonTriple, false, 25, 39,
                    "char_skeleton_elite", "char_skeleton_elite", "char_skeleton_elite"),
                Def(DunEliteWraithTriple, false, 25, 39,
                    "char_wraith_elite", "char_wraith_elite", "char_wraith_elite"),
                Def(DunBatDouble, false, 25, 39,
                    "char_bat", "char_bat"),
                Def(DunEliteRatRatEliteSkel, true, 21, 24,
                    "char_rat", "char_rat", "char_skeleton_elite"),
                Def(DunEliteRatRatEliteWraith, true, 21, 24,
                    "char_rat", "char_rat", "char_wraith_elite"),
                Def(DunEliteGargoyleDuo, true, 21, 34, 100, 0, 35, 0,
                    "char_gargoyle", "char_gargoyle"),
                Def(DunEliteGargoyleSkeletonSpider, true, 21, 34, 100, 0, 35, 0,
                    "char_gargoyle", "char_skeleton", "char_spider_lady"),
                Def(DunEliteRatTriple, true, 25, 39, 100, 0, 0, 35,
                    "char_rat", "char_rat", "char_rat"),
                Def(DunEliteGargoyleGolem, true, 25, 39,
                    "char_gargoyle", "char_stone_golem"),
                Def(DunEliteGolemSpider, true, 25, 39,
                    "char_stone_golem", "char_spider_lady")
            };

        static MonsterEncounterDefinition Def(
            string id,
            bool elite,
            int minFloor,
            int maxFloor,
            params string[] enemies) =>
            Def(id, elite, minFloor, maxFloor, 100, 0, 0, 0, enemies);

        static MonsterEncounterDefinition Def(
            string id,
            bool elite,
            int minFloor,
            int maxFloor,
            int weight,
            int preTenWeight,
            int preTwentyFiveWeight,
            int preThirtyWeight,
            params string[] enemies) =>
            new()
            {
                Id = id,
                IsElite = elite,
                MinFloor = minFloor,
                MaxFloor = maxFloor,
                Weight = weight,
                PreTenWeight = preTenWeight,
                PreTwentyFiveWeight = preTwentyFiveWeight,
                PreThirtyWeight = preThirtyWeight,
                EnemyCharacterIds = enemies
            };
    }
}
