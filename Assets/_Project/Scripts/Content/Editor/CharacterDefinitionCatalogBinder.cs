#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    /// <summary>
    /// 把 Data/Characters 写入 Resources，供正式包训练场怪物列表等使用。
    /// Build 时由 GrimhandBuildContentSync 调用。
    /// </summary>
    public static class CharacterDefinitionCatalogBinder
    {
        public const string CatalogAssetPath =
            "Assets/_Project/Resources/CharacterDefinitionCatalog_Demo.asset";
        const string CharactersRoot = "Assets/_Project/Data/Characters";

        [MenuItem("Grimhand/Content/Bind Character Definition Catalog")]
        public static void BindMenu()
        {
            var count = BindSilent();
            EditorUtility.DisplayDialog(
                "Character Catalog",
                $"已同步 {count} 个角色到 Resources/CharacterDefinitionCatalog_Demo。\n正式包训练场怪物列表依赖此资产（Build 时也会自动执行）。",
                "OK");
        }

        public static int BindSilent()
        {
            EnsureResourcesFolder();

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalogSO>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterDefinitionCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.Characters ??= new List<CharacterDefinitionSO>();
            catalog.Characters.Clear();

            var seen = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:CharacterDefinitionSO", new[] { CharactersRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var character = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
                if (character == null || string.IsNullOrEmpty(character.CharacterId))
                    continue;
                if (!seen.Add(character.CharacterId))
                    continue;
                catalog.Characters.Add(character);
            }

            catalog.Characters.Sort((a, b) => string.CompareOrdinal(a.CharacterId, b.CharacterId));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterCatalog] 已写入 {catalog.Characters.Count} 个角色 → {CatalogAssetPath}");
            return catalog.Characters.Count;
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
