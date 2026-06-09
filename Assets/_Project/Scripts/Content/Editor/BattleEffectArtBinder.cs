#if UNITY_EDITOR
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static class BattleEffectArtBinder
    {
        public const string CatalogPath = "Assets/_Project/Data/BattleActionEffectCatalog_Demo.asset";
        const string EffectRoot = "Assets/The Grimhands Asset/effects/";

        [MenuItem("Grimhand/Content/Bind Battle Action Effects")]
        public static void BindBattleActionEffects()
        {
            if (BindBattleEffectsSilent())
                Debug.Log("行动特效已绑定到 BattleActionEffectCatalog_Demo。");
        }

        public static bool BindBattleEffectsSilent()
        {
            EnsureFolder("Assets/_Project/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<BattleActionEffectCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BattleActionEffectCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.WarriorDamage = LoadNamedSprite(EffectRoot + "warrior_damage_effect.png", "warrior_damage_effect_0");
            catalog.PharaohDamage = LoadFirstSprite(EffectRoot + "pharoah_damage_effect.png");
            catalog.DevilDamage = LoadFirstSprite(EffectRoot + "devil_damage_effect.png");
            catalog.Blocking = LoadFirstSprite(EffectRoot + "blocking_effect.png");
            catalog.Healing = LoadFirstSprite(EffectRoot + "healing_effect.png");
            catalog.Poisoning = LoadNamedSprite(EffectRoot + "poisoning_effect.png", "poisoning_effect_0");
            catalog.Burning = LoadNamedSprite(EffectRoot + "burning_effect.png", "burning_effect_1");
            catalog.SacrificeBurst = LoadNamedSprite(EffectRoot + "sacrifice_effect.png", "sacrifice_effect_0");

            EditorUtility.SetDirty(catalog);
            return true;
        }

        static Sprite LoadFirstSprite(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        static Sprite LoadNamedSprite(string assetPath, string spriteName)
        {
            Sprite fallback = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is not Sprite sprite)
                    continue;

                fallback ??= sprite;
                if (sprite.name == spriteName)
                    return sprite;
            }

            return fallback;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
