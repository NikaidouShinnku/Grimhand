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
        const string NewUiRoot = "Assets/The Grimhands Asset/new_UI_and_Layout/";

        [MenuItem("Grimhand/Content/Refresh UI Visual Catalogs")]
        public static void RefreshUiVisualCatalogsMenu()
        {
            EnsureUiIconCatalog();
            EnsureCardVisualCatalog();
            AssignDemoCardRarities();
            Grimhand.Content.Editor.BattleEffectArtBinder.BindBattleEffectsSilent();
            AudioCatalogBootstrap.EnsureAudioCatalog();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "UI 美术目录已刷新",
                "已更新：\n" +
                "• BattleUiIconCatalog_Demo.asset\n" +
                "• CardVisualCatalog_Demo.asset（卡框按稀有度 + 类型）\n" +
                "• AudioCatalog_Demo.asset",
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
            catalog.CampGoldIcon = LoadNamedSprite(IconRoot + "camp_gold.png", "camp_gold_0");
            catalog.XpIcon = LoadFirstSprite(IconRoot + "XP.png");
            catalog.InventoryIcon = LoadFirstSprite(IconRoot + "inventory.png");
            catalog.ConfirmPlayIcon = LoadFirstSprite(IconRoot + "check.png");
            catalog.SkipIcon = LoadFirstSprite(IconRoot + "pass.png");
            catalog.NoteIcon = LoadFirstSprite(IconRoot + "note.png");
            catalog.MapIcon = LoadFirstSprite(IconRoot + "map.png");
            catalog.UnknownPathIcon = LoadNamedSprite(
                "Assets/The Grimhands Asset/path and background/unknown_path.png",
                UnknownPathFullSpriteName);
            catalog.CaveBackground = LoadNamedSprite(NewUiRoot + "new_cave_background.png", "new_cave_background_0")
                ?? LoadFirstSprite(NewUiRoot + "new_cave_background.png")
                ?? LoadFirstSprite("Assets/The Grimhands Asset/path and background/cave_background.png");
            catalog.ShopBackground = LoadFirstSprite(CampArtRoot + "shop_background.png");
            catalog.CampSiteBackground = LoadFirstSprite(NewUiRoot + "camp_background.png")
                ?? LoadFirstSprite(CampArtRoot + "campsite_background.png");
            catalog.MainMenuBackground = LoadFirstSprite(NewUiRoot + "mainmenu_background.png")
                ?? LoadNamedSprite(NewUiRoot + "mainmenu_background.png", "mainmenu_background_0");
            catalog.ChampionCampBuilding = LoadNamedSprite(CampArtRoot + "champion_camp.png", "champion_camp_0");
            catalog.ChampionCampHubBackground = LoadFirstSprite(
                NewUiRoot + "templates/远征军军营一级界面概念图-模板.png");
            catalog.ChampionCampButton1 = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/champion_camp_button1.png", "champion_camp_button1_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/champion_camp_button1.png");
            catalog.ChampionCampButton2 = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/champion_camp_button2.png", "champion_camp_button2_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/champion_camp_button2.png");
            catalog.UiButton3 = LoadNamedSprite(NewUiRoot + "interactables_and_ui/button3.png", "button3_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/button3.png");
            catalog.UiButton1 = LoadNamedSprite(NewUiRoot + "interactables_and_ui/button1.png", "button1_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/button1.png");
            catalog.UiButton2 = LoadNamedSprite(NewUiRoot + "interactables_and_ui/button2.png", "button2_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/button2.png");
            catalog.UiButton4 = LoadNamedSprite(NewUiRoot + "interactables_and_ui/button4.png", "button4_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/button4.png");
            catalog.UiButton6 = LoadNamedSprite(NewUiRoot + "interactables_and_ui/button6.png", "button6_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/button6.png");
            catalog.UiButton5 = LoadNamedSprite(NewUiRoot + "interactables_and_ui/button5.png", "button5_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/button5.png");
            catalog.UiSettingButton = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/setting_button.png", "setting_button_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/setting_button.png");
            catalog.UiChangeGamespeedButton = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/change_gamespeed_button.png", "change_gamespeed_button_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/change_gamespeed_button.png");
            catalog.ChampionCampTeamBackground = LoadFirstSprite(
                NewUiRoot + "templates/配置队伍概念图-模板.png");
            catalog.ChampionCampCollectionBackground = LoadFirstSprite(
                NewUiRoot + "templates/军营收藏概念图-模板.png");
            catalog.ChampionCampCharacterSelectBackground = LoadFirstSprite(
                NewUiRoot + "templates/军营角色选择概念图-模板.png");
            catalog.UiCharacterPlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/character_plate.png", "character_plate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/character_plate.png");
            catalog.UiEventPlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/event_plate.png", "event_plate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/event_plate.png");
            catalog.UiSliderBar = LoadNamedSprite(NewUiRoot + "interactables_and_ui/sliderbar.png", "sliderbar_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/sliderbar.png");
            catalog.UiSlider = LoadNamedSprite(NewUiRoot + "interactables_and_ui/slider.png", "slider_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/slider.png");
            catalog.UiCardDetailBackground = LoadFirstSprite(
                NewUiRoot + "templates/卡牌遗物角色详情概念图-模板.png");
            catalog.UiCampShopBackground = LoadFirstSprite(
                NewUiRoot + "templates/营地商店概念图-模板.png");
            catalog.UiExpeditionShopBackground = LoadFirstSprite(
                NewUiRoot + "templates/局内流浪商人概念图-模板.png");
            catalog.UiMerchantGoodsPlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/merchant_camp_goodsplate.png", "merchant_camp_goodsplate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/merchant_camp_goodsplate.png");
            catalog.UiMerchantGoodsImagePlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/merchant_camp_goodsimageplate.png",
                "merchant_camp_goodsimageplate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/merchant_camp_goodsimageplate.png");
            catalog.UiPromptPlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/prompt_plate.png", "prompt_plate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/prompt_plate.png");
            catalog.UiInformationPlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/information_plate.png", "information_plate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/information_plate.png");
            catalog.MerchantCampBuilding = LoadNamedSprite(CampArtRoot + "merchant_camp.png", "merchant_camp_0");
            catalog.PortalBuilding = LoadNamedSprite(CampArtRoot + "portal.png", "portal_0");
            catalog.TrainingGroundBuilding = LoadNamedSprite(CampArtRoot + "training_ground.png", "training_ground_0")
                ?? LoadFirstSprite(CampArtRoot + "training_ground.png");
            catalog.LibraryBuilding = LoadNamedSprite(CampArtRoot + "library.png", "library_0")
                ?? LoadFirstSprite(CampArtRoot + "library.png");
            catalog.TrainingGroundBackground = LoadNamedSprite(
                NewUiRoot + "new_training_ground_background.png", "new_training_ground_background_0")
                ?? LoadFirstSprite(NewUiRoot + "new_training_ground_background.png")
                ?? LoadFirstSprite(CampArtRoot + "training_ground_background.png");
            catalog.GameMenuButtons = new[]
            {
                LoadNamedSprite(IconRoot + "gamemenu.png", "gamemenu_0"),
                LoadNamedSprite(IconRoot + "gamemenu.png", "gamemenu_1"),
                LoadNamedSprite(IconRoot + "gamemenu.png", "gamemenu_2"),
                LoadNamedSprite(IconRoot + "gamemenu.png", "gamemenu_3")
            };
            catalog.EscMenuButtons = new[]
            {
                LoadNamedSprite(IconRoot + "escmenu.png", "escmenu_0"),
                LoadNamedSprite(IconRoot + "escmenu.png", "escmenu_1"),
                LoadNamedSprite(IconRoot + "escmenu.png", "escmenu_2"),
                LoadNamedSprite(IconRoot + "escmenu.png", "escmenu_3")
            };
            catalog.TalentAltarBuilding = LoadNamedSprite(CampArtRoot + "talent_alter.png", "talent_alter_0");
            catalog.UiTalentAltarBackground = LoadFirstSprite(
                NewUiRoot + "templates/天赋祭坛概念图-模板.png");
            catalog.UiLibraryCodexBackground = LoadFirstSprite(
                NewUiRoot + "templates/图鉴概念图-模板.png");
            catalog.UiExpeditionStartBackground = LoadFirstSprite(
                NewUiRoot + "templates/远征开启概念图-模板.png");
            catalog.UiChoosingPathLocationPlate = LoadNamedSprite(
                NewUiRoot + "interactables_and_ui/choosingpath_location_plate.png",
                "choosingpath_location_plate_0")
                ?? LoadFirstSprite(NewUiRoot + "interactables_and_ui/choosingpath_location_plate.png");
            catalog.CaveRouteSelectBackground = LoadNamedSprite(
                NewUiRoot + "new_cave_background2.png", "new_cave_background2_0")
                ?? LoadFirstSprite(NewUiRoot + "new_cave_background2.png");
            catalog.DungeonRouteSelectBackground = LoadNamedSprite(
                NewUiRoot + "new_dungeon_background2.png", "new_dungeon_background2_0")
                ?? LoadFirstSprite(NewUiRoot + "new_dungeon_background2.png");
            catalog.AbyssRouteSelectBackground = LoadNamedSprite(
                NewUiRoot + "new_underwaterruin_background2.png", "new_underwaterruin_background2_0")
                ?? LoadFirstSprite(NewUiRoot + "new_underwaterruin_background2.png");
            catalog.PathFrameCombat = LoadNamedSprite(NewUiRoot + "path/path_combat.png", "path_combat_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_combat.png");
            catalog.PathFrameElite = LoadNamedSprite(NewUiRoot + "path/path_combatelite.png", "path_combatelite_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_combatelite.png");
            catalog.PathFrameTreasure = LoadNamedSprite(
                NewUiRoot + "path/path_treasurechest.png", "path_treasurechest_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_treasurechest.png");
            catalog.PathFrameEvent = LoadNamedSprite(NewUiRoot + "path/path_event.png", "path_event_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_event.png");
            catalog.PathFrameShop = LoadNamedSprite(NewUiRoot + "path/path_shop.png", "path_shop_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_shop.png");
            catalog.PathFrameAltar = LoadNamedSprite(NewUiRoot + "path/path_alter.png", "path_alter_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_alter.png");
            catalog.PathFrameBoss = LoadNamedSprite(NewUiRoot + "path/path_boss.png", "path_boss_0")
                ?? LoadFirstSprite(NewUiRoot + "path/path_boss.png");
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
            catalog.DungeonBackground = LoadNamedSprite(
                NewUiRoot + "new_dungeon_background.png", "new_dungeon_background_0")
                ?? LoadFirstSprite(NewUiRoot + "new_dungeon_background.png")
                ?? LoadFirstSprite(CampArtRoot + "dungeon_background.png");

            var abyssPaths = new System.Collections.Generic.List<Sprite>();
            for (var i = 1; i <= 3; i++)
            {
                var sprite = LoadFirstSprite(
                    $"Assets/The Grimhands Asset/path and background/underwaterruin_path{i}.png");
                if (sprite != null)
                    abyssPaths.Add(sprite);
            }

            catalog.AbyssPathVariants = abyssPaths.ToArray();
            catalog.AbyssBackground = LoadNamedSprite(
                NewUiRoot + "new_underwaterruin_background.png", "new_underwaterruin_background_0")
                ?? LoadFirstSprite(NewUiRoot + "new_underwaterruin_background.png")
                ?? LoadFirstSprite(
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
            catalog.StatusBrandMark = LoadFirstSprite(IconRoot + "warden_brand.png");

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
            SetCardRarity("Card_p_solar_wrath", CardRarity.Rare);
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
