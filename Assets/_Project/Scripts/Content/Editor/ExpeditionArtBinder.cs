#if UNITY_EDITOR
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static class ExpeditionArtBinder
    {
        const string IconCatalogPath = "Assets/_Project/Data/BattleUiIconCatalog_Demo.asset";
        const string CaveBackgroundPath = "Assets/The Grimhands Asset/path and background/cave_background.png";
        const string ShopBackgroundPath = "Assets/The Grimhands Asset/path and background/shop_background.png";
        const string NoteIconPath = "Assets/The Grimhands Asset/icon/note.png";

        static readonly string[] CavePathAssetPaths =
        {
            "Assets/The Grimhands Asset/path and background/cave_path1.png",
            "Assets/The Grimhands Asset/path and background/cave_path2.png",
            "Assets/The Grimhands Asset/path and background/cave_path3.png",
            "Assets/The Grimhands Asset/path and background/cave_path4.png",
            "Assets/The Grimhands Asset/path and background/cave_path5.png"
        };

        [MenuItem("Grimhand/Content/Bind Expedition Art")]
        public static void BindExpeditionArt()
        {
            if (BindExpeditionArtSilent())
                Debug.Log("远征美术资源已绑定到 BattleUiIconCatalog_Demo。");
        }

        public static bool BindExpeditionArtSilent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(IconCatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"未找到 {IconCatalogPath}，请先执行 Generate Demo ScriptableObjects。");
                return false;
            }

            catalog.CaveBackground = LoadSprite(CaveBackgroundPath);
            catalog.ShopBackground = LoadSprite(ShopBackgroundPath);
            catalog.NoteIcon = LoadSprite(NoteIconPath);
            catalog.TreasureChestClosed = LoadSprite("Assets/The Grimhands Asset/icon/treasure_chest_closed.png");
            catalog.TreasureChestOpen = LoadSprite("Assets/The Grimhands Asset/icon/treasure_chest_open.png");

            var paths = new Sprite[CavePathAssetPaths.Length];
            for (var i = 0; i < CavePathAssetPaths.Length; i++)
                paths[i] = LoadSprite(CavePathAssetPaths[i]);

            catalog.CavePathVariants = paths;
            catalog.DungeonBackground = LoadSprite("Assets/The Grimhands Asset/path and background/dungeon_background.png");

            var dungeonPaths = new Sprite[]
            {
                LoadSprite("Assets/The Grimhands Asset/path and background/dungeon_path1.png"),
                LoadSprite("Assets/The Grimhands Asset/path and background/dungeon_path2.png"),
                LoadSprite("Assets/The Grimhands Asset/path and background/dungeon_path3.png")
            };
            catalog.DungeonPathVariants = dungeonPaths;

            catalog.AbyssBackground = LoadSprite(
                "Assets/The Grimhands Asset/path and background/underwaterruin_background.png");
            var abyssPaths = new Sprite[]
            {
                LoadSprite("Assets/The Grimhands Asset/path and background/underwaterruin_path1.png"),
                LoadSprite("Assets/The Grimhands Asset/path and background/underwaterruin_path2.png"),
                LoadSprite("Assets/The Grimhands Asset/path and background/underwaterruin_path3.png")
            };
            catalog.AbyssPathVariants = abyssPaths;
            catalog.StatusDamageUp = LoadSprite("Assets/The Grimhands Asset/icon/damage_up.png");
            catalog.StatusDamageDown = LoadSprite("Assets/The Grimhands Asset/icon/damage_down.png");
            catalog.StatusDefenseUp = LoadSprite("Assets/The Grimhands Asset/icon/defense_up.png");
            catalog.StatusDefenseDown = LoadSprite("Assets/The Grimhands Asset/icon/defense_down.png");
            catalog.StatusArmorAcqUp = LoadSprite("Assets/The Grimhands Asset/icon/armoracq_up.png");
            catalog.StatusArmorAcqDown = LoadSprite("Assets/The Grimhands Asset/icon/armoracq_down.png");
            catalog.StatusSpdDown = LoadSprite("Assets/The Grimhands Asset/icon/spd_down.png");
            catalog.StatusSpdUp = LoadSprite("Assets/The Grimhands Asset/icon/spd_up.png");
            catalog.StatusPoisoning = LoadSprite("Assets/The Grimhands Asset/effects/poisoning_effect.png");
            catalog.StatusBurning = LoadSprite("Assets/The Grimhands Asset/effects/burning_effect.png");
            EditorUtility.SetDirty(catalog);
            return true;
        }

        static Sprite LoadSprite(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
#endif
