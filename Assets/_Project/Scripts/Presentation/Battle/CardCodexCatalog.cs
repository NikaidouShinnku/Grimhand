using System.Collections.Generic;
using System.Linq;
using Grimhand.Content;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Grimhand.Presentation.Battle
{
    /// <summary>测试图鉴 / 假人出牌：按总览表角色分类展示全部卡牌。</summary>
    public static class CardCodexCatalog
    {
        public readonly struct CategoryGroup
        {
            public CategoryGroup(string label, IReadOnlyList<CardDefinitionSO> cards)
            {
                Label = label;
                Cards = cards;
            }

            public string Label { get; }
            public IReadOnlyList<CardDefinitionSO> Cards { get; }
        }

        /// <summary>与总览表「卡牌 / Boss设计 / 小怪设计」一致的分类顺序。</summary>
        static readonly string[] CategoryOrder =
        {
            "战士",
            "法老",
            "恶魔",
            "毒蛇女王",
            "巫妖女王",
            "Boss·骷髅王",
            "Boss·易爆骷髅头",
            "Boss·幽灵女王",
            "Boss·典狱长",
            "Boss·囚笼",
            "Boss·黑暗骑士",
            "Boss·腐化海洋女神",
            "小怪·哥布林",
            "小怪·史莱姆",
            "小怪·骷髅兵",
            "小怪·骷髅精英",
            "小怪·幽灵",
            "小怪·幽灵精英",
            "小怪·绿皮巨魔",
            "小怪·巨翼蝙蝠",
            "小怪·鼠人",
            "小怪·锁链怨灵",
            "小怪·石像鬼",
            "小怪·蜘蛛贵妇",
            "小怪·石傀儡",
            "小怪·踏潮守卫",
            "小怪·水母海巫",
            "小怪·人鱼战士",
            "小怪·深渊怪物",
            "小怪·腐蚀蟹",
            "小怪·鬼灵海盗船长",
            "其他",
            "未分类"
        };

        static readonly Dictionary<string, string> OwnerToCategory = new()
        {
            ["char_knight"] = "战士",
            ["char_mage"] = "法老",
            ["char_ranger"] = "恶魔",
            ["char_snake_queen"] = "毒蛇女王",
            ["char_lich_queen"] = "巫妖女王",
            ["char_skeleton_king"] = "Boss·骷髅王",
            ["char_explosive_skull"] = "Boss·易爆骷髅头",
            ["char_ghost_queen"] = "Boss·幽灵女王",
            ["char_warden"] = "Boss·典狱长",
            ["char_prison_cage"] = "Boss·囚笼",
            ["char_dark_knight"] = "Boss·黑暗骑士",
            ["char_corrupted_ocean_goddess"] = "Boss·腐化海洋女神",
            ["char_goblin"] = "小怪·哥布林",
            ["char_slime"] = "小怪·史莱姆",
            ["char_skeleton"] = "小怪·骷髅兵",
            ["char_skeleton_elite"] = "小怪·骷髅精英",
            ["char_wraith"] = "小怪·幽灵",
            ["char_wraith_elite"] = "小怪·幽灵精英",
            ["char_ogre"] = "小怪·绿皮巨魔",
            ["char_bat"] = "小怪·巨翼蝙蝠",
            ["char_rat"] = "小怪·鼠人",
            ["char_chain_wraith"] = "小怪·锁链怨灵",
            ["char_gargoyle"] = "小怪·石像鬼",
            ["char_spider_lady"] = "小怪·蜘蛛贵妇",
            ["char_stone_golem"] = "小怪·石傀儡",
            ["char_seahorse_guard"] = "小怪·踏潮守卫",
            ["char_jellyfish_caster"] = "小怪·水母海巫",
            ["char_mermaid_warrior"] = "小怪·人鱼战士",
            ["char_abyss_creature"] = "小怪·深渊怪物",
            ["char_corrupted_crab"] = "小怪·腐蚀蟹",
            ["char_phantom_captain"] = "小怪·鬼灵海盗船长",
            ["char_dummy"] = "其他",
        };

        public static string ResolveOwnerCategory(string ownerCharacterId) =>
            !string.IsNullOrEmpty(ownerCharacterId)
            && OwnerToCategory.TryGetValue(ownerCharacterId, out var label)
                ? label
                : "其他";

        public static IReadOnlyList<CategoryGroup> BuildGroupedCatalog()
        {
            var buckets = new Dictionary<string, List<CardDefinitionSO>>();
            foreach (var label in CategoryOrder)
                buckets[label] = new List<CardDefinitionSO>();

            foreach (var card in LoadAllCardDefinitions())
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                var category = ResolveCategory(card);
                if (!buckets.ContainsKey(category))
                    buckets[category] = new List<CardDefinitionSO>();

                buckets[category].Add(card);
            }

            var groups = new List<CategoryGroup>();
            foreach (var label in CategoryOrder)
            {
                if (buckets[label].Count == 0)
                    continue;

                buckets[label].Sort(CompareByRarityThenName);
                groups.Add(new CategoryGroup(label, buckets[label]));
            }

            return groups;
        }

        static int CompareByRarityThenName(CardDefinitionSO a, CardDefinitionSO b)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            var rarityCmp = a.Rarity.CompareTo(b.Rarity);
            if (rarityCmp != 0)
                return rarityCmp;

            var nameCmp = string.CompareOrdinal(a.DisplayName, b.DisplayName);
            return nameCmp != 0 ? nameCmp : string.CompareOrdinal(a.CardId, b.CardId);
        }

        public static string ResolveCategory(CardDefinitionSO card)
        {
            if (card == null)
                return "未分类";

            if (!string.IsNullOrEmpty(card.OwnerCharacterId)
                && OwnerToCategory.TryGetValue(card.OwnerCharacterId, out var byOwner))
                return byOwner;

            // 兜底：旧前缀（Owner 缺失时）
            var id = card.CardId ?? "";
            if (id.StartsWith("w_"))
                return "战士";
            if (id.StartsWith("p_"))
                return "法老";
            if (id.StartsWith("d_"))
                return "恶魔";
            if (id.StartsWith("v_"))
                return "毒蛇女王";
            if (id.StartsWith("l_"))
                return "巫妖女王";
            if (id.StartsWith("m_king_") || id.StartsWith("m_skull_"))
                return id.StartsWith("m_skull_") ? "Boss·易爆骷髅头" : "Boss·骷髅王";
            if (id.StartsWith("m_queen_"))
                return "Boss·幽灵女王";
            if (id.StartsWith("g_") || id.StartsWith("m_") || id.StartsWith("r_") || id.StartsWith("k_"))
                return "其他";
            if (id.StartsWith("curse"))
                return "其他";
            return "未分类";
        }

        public static List<CardDefinitionSO> LoadAllCardDefinitions()
        {
            var list = new List<CardDefinitionSO>();
            var seen = new HashSet<string>();

            void Add(CardDefinitionSO card)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    return;
                if (!seen.Add(card.CardId))
                    return;
                list.Add(card);
            }

            // 正式包：Resources 目录（由 CardDefinitionCatalogBinder 在编辑器/Build 前同步）
            var runtimeCatalog = Resources.Load<CardDefinitionCatalogSO>("CardDefinitionCatalog_Demo");
            if (runtimeCatalog?.Cards != null)
            {
                foreach (var card in runtimeCatalog.Cards)
                    Add(card);
            }

#if UNITY_EDITOR
            // 编辑器：再扫一遍 Data/Cards，避免 catalog 过期时图鉴缺卡
            foreach (var guid in AssetDatabase.FindAssets("t:CardDefinitionSO", new[] { "Assets/_Project/Data/Cards" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Add(AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path));
            }
#endif

            return list
                .OrderBy(c => c.CardId)
                .ToList();
        }
    }
}
