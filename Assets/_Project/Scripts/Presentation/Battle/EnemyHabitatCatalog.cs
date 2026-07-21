using System.Collections.Generic;

namespace Grimhand.Presentation.Battle
{
    /// <summary>
    /// 图鉴：敌人出没地点（对照总览表「小怪设计 / Boss设计 / 怪物组合」分区）。
    /// </summary>
    public static class EnemyHabitatCatalog
    {
        public const string Cave = "洞穴";
        public const string Dungeon = "地牢";
        public const string Abyss = "海渊";

        static readonly Dictionary<string, string> ByCharacterId = new()
        {
            // 洞穴层
            ["char_goblin"] = Cave,
            ["char_slime"] = Cave,
            ["char_skeleton"] = Cave,
            ["char_skeleton_elite"] = Cave,
            ["char_wraith"] = Cave,
            ["char_wraith_elite"] = Cave,
            ["char_ogre"] = Cave,
            ["char_bat"] = Cave,
            ["char_skeleton_king"] = Cave,
            ["char_explosive_skull"] = Cave,
            ["char_ghost_queen"] = Cave,

            // 地牢层
            ["char_rat"] = Dungeon,
            ["char_chain_wraith"] = Dungeon,
            ["char_gargoyle"] = Dungeon,
            ["char_spider_lady"] = Dungeon,
            ["char_stone_golem"] = Dungeon,
            ["char_warden"] = Dungeon,
            ["char_prison_cage"] = Dungeon,
            ["char_dark_knight"] = Dungeon,

            // 海渊层
            ["char_seahorse_guard"] = Abyss,
            ["char_jellyfish_caster"] = Abyss,
            ["char_mermaid_warrior"] = Abyss,
            ["char_abyss_creature"] = Abyss,
            ["char_corrupted_crab"] = Abyss,
            ["char_phantom_captain"] = Abyss,
            ["char_corrupted_ocean_goddess"] = Abyss,
        };

        public static string GetHabitat(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return "";

            return ByCharacterId.TryGetValue(characterId, out var habitat) ? habitat : "";
        }
    }
}
