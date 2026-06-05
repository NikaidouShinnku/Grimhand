#if UNITY_EDITOR
using Grimhand.Battle.Consumables;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static class ConsumableArtBinder
    {
        const string CatalogPath = "Assets/_Project/Data/ConsumableVisualCatalog_Demo.asset";
        const string ConsumableRoot = "Assets/The Grimhands Asset/consumables";

        static readonly (string Id, string File)[] Overrides =
        {
            (ConsumableIds.SpringBottle, "spring_water"),
            (ConsumableIds.ScrollPage, "ancient_scroll")
        };

        [MenuItem("Grimhand/Content/Bind Consumable Art")]
        public static void BindConsumableArt()
        {
            if (BindConsumableArtSilent())
                Debug.Log("消耗品图标已绑定到 ConsumableVisualCatalog_Demo。");
        }

        public static bool BindConsumableArtSilent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ConsumableVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Entries.Clear();

            foreach (var def in ConsumableDatabase.All)
            {
                var sprite = LoadSprite(def.Id);
                if (sprite == null)
                {
                    Debug.LogWarning($"[Grimhand] 未找到消耗品图标：{def.Id}");
                    continue;
                }

                catalog.Entries.Add(new ConsumableVisualEntry
                {
                    ConsumableId = def.Id,
                    Icon = sprite
                });
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return true;
        }

        static Sprite LoadSprite(string consumableId)
        {
            var fileName = consumableId;
            foreach (var pair in Overrides)
            {
                if (pair.Id == consumableId)
                {
                    fileName = pair.File;
                    break;
                }
            }

            var path = $"{ConsumableRoot}/{fileName}.png";
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
