#if UNITY_EDITOR
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    public static class GrimhandUiVisualBootstrap
    {
        public const string IconCatalogPath = "Assets/_Project/Data/BattleUiIconCatalog_Demo.asset";
        public const string CardCatalogPath = "Assets/_Project/Data/CardVisualCatalog_Demo.asset";
        public const string UnknownPathFullSpriteName = "unknown_path_1";
        const string IconRoot = "Assets/The Grimhands Asset/icon/";
        const string CardRoot = "Assets/The Grimhands Asset/card/";
        const string CampArtRoot = "Assets/The Grimhands Asset/path and background/";

        [MenuItem("Grimhand/Content/Refresh UI Visual Catalogs")]
        public static void RefreshUiVisualCatalogsMenu()
        {
            EnsureUiIconCatalog();
            EnsureCardVisualCatalog();
            AssignDemoCardRarities();
            Grimhand.Content.Editor.BattleEffectArtBinder.BindBattleEffectsSilent();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "UI 美术目录已刷新",
                "已更新：\n" +
                "• BattleUiIconCatalog_Demo.asset\n" +
                "• CardVisualCatalog_Demo.asset（卡框按稀有度 + 类型）",
                "好的");
        }

        public static void EnsureUiIconCatalog()
        {
            EnsureFolder("Assets/_Project/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(IconCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BattleUiIconCatalogSO>();
                AssetDatabase.CreateAsset(catalog, IconCatalogPath);
            }

            catalog.HpIcon = LoadFirstSprite(IconRoot + "HP.png");
            catalog.ArmorIcon = LoadFirstSprite(IconRoot + "ARM.png");
            catalog.AttackIcon = LoadFirstSprite(IconRoot + "ATK.png");
            catalog.DefenseIcon = LoadFirstSprite(IconRoot + "DEF.png");
            catalog.SpeedIcon = LoadFirstSprite(IconRoot + "SPD.png");
            catalog.EnergyIcon = LoadFirstSprite(IconRoot + "ERG.png");
            catalog.GoldIcon = LoadFirstSprite(IconRoot + "coin.png");
            catalog.XpIcon = LoadFirstSprite(IconRoot + "XP.png");
            catalog.InventoryIcon = LoadFirstSprite(IconRoot + "inventory.png");
            catalog.ConfirmPlayIcon = LoadFirstSprite(IconRoot + "check.png");
            catalog.SkipIcon = LoadFirstSprite(IconRoot + "pass.png");
            catalog.NoteIcon = LoadFirstSprite(IconRoot + "note.png");
            catalog.MapIcon = LoadFirstSprite(IconRoot + "map.png");
            catalog.UnknownPathIcon = LoadNamedSprite(
                "Assets/The Grimhands Asset/path and background/unknown_path.png",
                UnknownPathFullSpriteName);
            catalog.CaveBackground = LoadFirstSprite("Assets/The Grimhands Asset/path and background/cave_background.png");
            catalog.ShopBackground = LoadFirstSprite(CampArtRoot + "shop_background.png");
            catalog.CampSiteBackground = LoadFirstSprite(CampArtRoot + "campsite_background.png");
            catalog.ChampionCampBuilding = LoadNamedSprite(CampArtRoot + "champion_camp.png", "champion_camp_0");
            catalog.MerchantCampBuilding = LoadNamedSprite(CampArtRoot + "merchant_camp.png", "merchant_camp_0");
            catalog.PortalBuilding = LoadNamedSprite(CampArtRoot + "portal.png", "portal_0");
            catalog.TalentAltarBuilding = LoadNamedSprite(CampArtRoot + "talent_alter.png", "talent_alter_0");
            catalog.TalentRunePlate = LoadNamedSprite(IconRoot + "talent_rune_plate.png", "talent_rune_plate_0");
            catalog.TreasureChestClosed = LoadFirstSprite("Assets/The Grimhands Asset/icon/treasure_chest_closed.png");
            catalog.TreasureChestOpen = LoadFirstSprite("Assets/The Grimhands Asset/icon/treasure_chest_open.png");

            var paths = new System.Collections.Generic.List<Sprite>();
            for (var i = 1; i <= 5; i++)
            {
                var sprite = LoadFirstSprite($"Assets/The Grimhands Asset/path and background/cave_path{i}.png");
                if (sprite != null)
                    paths.Add(sprite);
            }

            catalog.CavePathVariants = paths.ToArray();

            var dungeonPaths = new System.Collections.Generic.List<Sprite>();
            for (var i = 1; i <= 3; i++)
            {
                var sprite = LoadFirstSprite($"Assets/The Grimhands Asset/path and background/dungeon_path{i}.png");
                if (sprite != null)
                    dungeonPaths.Add(sprite);
            }

            catalog.DungeonPathVariants = dungeonPaths.ToArray();
            catalog.DungeonBackground = LoadFirstSprite(CampArtRoot + "dungeon_background.png");

            var abyssPaths = new System.Collections.Generic.List<Sprite>();
            for (var i = 1; i <= 3; i++)
            {
                var sprite = LoadFirstSprite(
                    $"Assets/The Grimhands Asset/path and background/underwaterruin_path{i}.png");
                if (sprite != null)
                    abyssPaths.Add(sprite);
            }

            catalog.AbyssPathVariants = abyssPaths.ToArray();
            catalog.AbyssBackground = LoadFirstSprite(
                "Assets/The Grimhands Asset/path and background/underwaterruin_background.png");

            catalog.StatusDamageUp = LoadFirstSprite(IconRoot + "damage_up.png");
            catalog.StatusDamageDown = LoadFirstSprite(IconRoot + "damage_down.png");
            catalog.StatusDefenseUp = LoadFirstSprite(IconRoot + "defense_up.png");
            catalog.StatusDefenseDown = LoadFirstSprite(IconRoot + "defense_down.png");
            catalog.StatusArmorAcqUp = LoadFirstSprite(IconRoot + "armoracq_up.png");
            catalog.StatusArmorAcqDown = LoadFirstSprite(IconRoot + "armoracq_down.png");
            catalog.StatusSpdDown = LoadFirstSprite(IconRoot + "spd_down.png");
            catalog.StatusSpdUp = LoadFirstSprite(IconRoot + "spd_up.png");
            catalog.StatusPoisoning = LoadNamedSprite(
                "Assets/The Grimhands Asset/effects/poisoning_effect.png", "poisoning_effect_0");
            catalog.StatusBurning = LoadNamedSprite(
                "Assets/The Grimhands Asset/effects/burning_effect.png", "burning_effect_1");

            EditorUtility.SetDirty(catalog);
        }

        public static void EnsureCardVisualCatalog()
        {
            EnsureFolder("Assets/_Project/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogSO>(CardCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CardCatalogPath);
            }

            catalog.FrameSets.Clear();
            AddFrameSet(catalog, CardRarity.Common, "common");
            AddFrameSet(catalog, CardRarity.Rare, "rare");
            AddFrameSet(catalog, CardRarity.Epic, "epic");
            AddFrameSet(catalog, CardRarity.SuperRare, "superrare");
            AddFrameSet(catalog, CardRarity.Legendary, "legendary");

            var common = catalog.FrameSets.Count > 0 ? catalog.FrameSets[0] : null;
            if (common != null)
            {
                catalog.DefaultFrameAttack = common.AttackFrame;
                catalog.DefaultFrameDefense = common.DefenseFrame;
                catalog.DefaultFrameStatus = common.StatusFrame;
            }

            EditorUtility.SetDirty(catalog);
        }

        public static void AssignDemoCardRarities()
        {
            SetCardRarity("Card_w_basic_slash", CardRarity.Common);
            SetCardRarity("Card_w_power_cleave", CardRarity.Rare);
            SetCardRarity("Card_w_iron_parry", CardRarity.SuperRare);
            SetCardRarity("Card_w_shield_block", CardRarity.Common);
            SetCardRarity("Card_w_fatal_strike", CardRarity.Rare);
            SetCardRarity("Card_p_sand_ray", CardRarity.Common);
            SetCardRarity("Card_p_bless", CardRarity.Rare);
            SetCardRarity("Card_p_solar_wrath", CardRarity.Epic);
            SetCardRarity("Card_d_shadow_claw", CardRarity.Common);
            SetCardRarity("Card_d_blood_flame", CardRarity.Rare);
        }

        static void SetCardRarity(string assetName, CardRarity rarity)
        {
            var path = $"Assets/_Project/Data/Cards/{assetName}.asset";
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
            if (card == null)
                return;

            card.Rarity = rarity;
            EditorUtility.SetDirty(card);
        }

        static void AddFrameSet(CardVisualCatalogSO catalog, CardRarity rarity, string prefix)
        {
            catalog.FrameSets.Add(new CardFrameRaritySet
            {
                Rarity = rarity,
                AttackFrame = LoadFirstSprite($"{CardRoot}{prefix}_card_atk.png"),
                DefenseFrame = LoadFirstSprite($"{CardRoot}{prefix}_card_def.png"),
                StatusFrame = LoadFirstSprite($"{CardRoot}{prefix}_card_spc.png")
            });
        }

        static Sprite LoadFirstSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

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
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(spriteName))
                return LoadFirstSprite(assetPath);

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Sprite fallback = null;
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
