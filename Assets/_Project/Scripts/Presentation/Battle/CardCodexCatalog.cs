using System.Collections.Generic;
using System.Linq;
using Grimhand.Content;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Grimhand.Presentation.Battle
{
    /// <summary>测试用图鉴：加载项目中全部 CardDefinitionSO 并按策划分类。</summary>
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

        static readonly string[] CategoryOrder =
        {
            "战士",
            "法老",
            "恶魔",
            "毒蛇女王",
            "巫妖女王",
            "Boss",
            "敌人",
            "哥布林",
            "其他",
            "未分类"
        };

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

                buckets[label].Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
                groups.Add(new CategoryGroup(label, buckets[label]));
            }

            return groups;
        }

        public static string ResolveCategory(CardDefinitionSO card)
        {
            var id = card?.CardId ?? "";
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
            if (id.StartsWith("m_king_") || id.StartsWith("m_queen_"))
                return "Boss";
            if (id.StartsWith("m_"))
                return "敌人";
            if (id.StartsWith("g_"))
                return "哥布林";
            if (id.StartsWith("r_") || id.StartsWith("k_"))
                return "其他";
            return "未分类";
        }

        public static List<CardDefinitionSO> LoadAllCardDefinitions()
        {
            var list = new List<CardDefinitionSO>();
#if UNITY_EDITOR
            foreach (var guid in AssetDatabase.FindAssets("t:CardDefinitionSO", new[] { "Assets/_Project/Data/Cards" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
                if (card != null)
                    list.Add(card);
            }
#else
            // 非 Editor 构建：Resources 未挂载全量卡库时，图鉴仅展示运行时已知定义。
#endif
            return list
                .GroupBy(c => c.CardId)
                .Select(g => g.First())
                .OrderBy(c => c.CardId)
                .ToList();
        }
    }
}
