#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    /// <summary>
    /// 把 Data/Cards 下全部 CardDefinitionSO 写入 Resources，供正式包图鉴加载。
    /// Build 时由 GrimhandBuildContentSync 调用。
    /// </summary>
    public static class CardDefinitionCatalogBinder
    {
        public const string CatalogAssetPath =
            "Assets/_Project/Resources/CardDefinitionCatalog_Demo.asset";
        const string CardsRoot = "Assets/_Project/Data/Cards";

        [MenuItem("Grimhand/Content/Bind Card Definition Catalog")]
        public static void BindMenu()
        {
            var count = BindSilent();
            EditorUtility.DisplayDialog(
                "Card Catalog",
                $"已同步 {count} 张卡牌到 Resources/CardDefinitionCatalog_Demo。\n正式包测试图鉴依赖此资产（Build 时也会自动执行）。",
                "OK");
        }

        public static int BindSilent()
        {
            EnsureResourcesFolder();

            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogSO>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardDefinitionCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.Cards ??= new List<CardDefinitionSO>();
            catalog.Cards.Clear();

            var seen = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:CardDefinitionSO", new[] { CardsRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;
                if (!seen.Add(card.CardId))
                    continue;
                catalog.Cards.Add(card);
            }

            catalog.Cards.Sort((a, b) => string.CompareOrdinal(a.CardId, b.CardId));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardCatalog] 已写入 {catalog.Cards.Count} 张卡牌 → {CatalogAssetPath}");
            return catalog.Cards.Count;
        }

        static void EnsureResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                return;
            AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        }
    }
}
#endif
