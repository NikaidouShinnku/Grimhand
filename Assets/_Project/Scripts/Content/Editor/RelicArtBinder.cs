#if UNITY_EDITOR
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static class RelicArtBinder
    {
        const string CatalogPath = "Assets/_Project/Data/RelicVisualCatalog_Demo.asset";
        const string RelicRoot = "Assets/The Grimhands Asset/relics";

        [MenuItem("Grimhand/Content/Bind Relic Art")]
        public static void BindRelicArt()
        {
            if (BindRelicArtSilent())
                Debug.Log("遗物图标已绑定到 RelicVisualCatalog_Demo。");
        }

        public static bool BindRelicArtSilent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RelicVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Entries.Clear();

            foreach (var relic in RelicDatabase.All)
            {
                var sprite = LoadRelicSprite(relic.Id);
                if (sprite == null)
                {
                    Debug.LogWarning($"[Grimhand] 未找到遗物图标：{relic.Id}.png");
                    continue;
                }

                catalog.Entries.Add(new RelicVisualEntry
                {
                    RelicId = relic.Id,
                    Icon = sprite
                });
            }

            EditorUtility.SetDirty(catalog);
            return true;
        }

        static Sprite LoadRelicSprite(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
                return null;

            var path = $"{RelicRoot}/{relicId}.png";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            return RelicSpriteResolver.PickBest(assets, relicId);
        }
    }
}
#endif
