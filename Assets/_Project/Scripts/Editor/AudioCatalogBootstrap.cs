#if UNITY_EDITOR
using System.IO;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    public static class AudioCatalogBootstrap
    {
        public const string CatalogPath = "Assets/_Project/Resources/AudioCatalog_Demo.asset";
        const string SoundRoot = "Assets/The Grimhands Asset/sound/";

        [InitializeOnLoadMethod]
        static void AutoEnsureOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                EnsureAudioCatalog();
            };
        }

        [MenuItem("Grimhand/Content/Refresh Audio Catalog")]
        public static void RefreshAudioCatalogMenu()
        {
            var catalog = EnsureAudioCatalog();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "音频目录已刷新",
                catalog != null
                    ? "已更新 Resources/AudioCatalog_Demo.asset，并从 sound 文件夹绑定全部 clip（含 mp3/ogg/wav）。"
                    : "刷新失败。",
                "好的");
        }

        public static AudioCatalogSO EnsureAudioCatalog()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Resources");

            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                // Migrate old Data path if present.
                var legacy = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(
                    "Assets/_Project/Data/AudioCatalog_Demo.asset");
                if (legacy != null)
                {
                    AssetDatabase.MoveAsset(
                        "Assets/_Project/Data/AudioCatalog_Demo.asset",
                        CatalogPath);
                    catalog = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(CatalogPath);
                }
            }

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.BgmCave = LoadClip("bgm_cave");
            catalog.BgmDungeon = LoadClip("bgm_dungeon");
            catalog.BgmOceanRuin = LoadClip("bgm_oceanruin");
            catalog.BgmCamp = LoadClip("bgm_camp");
            catalog.BgmBattle = LoadClip("bgm_battle");
            catalog.BgmBattle2 = LoadClip("bgm_battle2");
            catalog.BgmBattle3 = LoadClip("bgm_battle3");

            catalog.UiMenuButtonPress = LoadClip("ui_menubutton_press");
            catalog.UiButtonHover = LoadClip("ui_button_hover");
            catalog.UiButtonPress = LoadClip("ui_button_press");
            catalog.UiButtonPress2 = LoadClip("ui_button_press2");
            catalog.UiButtonUpgradeCard = LoadClip("ui_button_upgradecard");
            catalog.UiButtonUpgradeCard2 = LoadClip("ui_button_upgradecard2");
            catalog.UiButtonUpgradePower = LoadClip("ui_button_upgradepower");
            catalog.UiChestOpen = LoadClip("ui_chest_open");
            catalog.UiShopEnter = LoadClip("ui_shop_enter");
            catalog.UiGoldAcquire = LoadClip("ui_gold_acquire");
            catalog.UiRelicsAcquire = LoadClip("ui_relics_acquire");
            catalog.UiCardAcquire = LoadClip("ui_card_acquire");
            catalog.UiCardPackOpen = LoadClip("ui_cardpack_open");
            catalog.UiInventoryOpen = LoadClip("ui_inventory_open");
            catalog.UiInventoryClose = LoadClip("ui_inventory_close");
            catalog.UiOpenMap = LoadClipFirst("ui_open_map", "open_map");
            catalog.UiCloseMap = LoadClipFirst("ui_close_map", "close_map");

            catalog.BattleUsePotion = LoadClip("battle_use_potion");
            catalog.BattleUseConsumable = LoadClip("battle_use_consumable");
            catalog.BattleAttackEnemy = LoadClip("battle_attack_enemy");
            catalog.BattleAttackSnakeQueen = LoadClip("battle_attack_snakequeen");
            catalog.BattleAttackWarrior = LoadClip("battle_attack_warrior");
            catalog.BattleAttackPharaoh = LoadClip("battle_attack_pharoah");
            catalog.BattleAttackDevil = LoadClip("battle_attack_devil");
            catalog.BattleAttackLichQueen = LoadClip("battle_attack_lichqueen");
            catalog.BattleCast = LoadClip("battle_cast");
            catalog.BattleBlocking = LoadClip("battle_blocking");
            catalog.BattleCardHover = LoadClip("battle_card_hover");
            catalog.BattleCardDraw = LoadClip("battle_card_draw");
            catalog.BattleCardSelect = LoadClip("battle_card_select");
            catalog.BattleEffectPoison = LoadClip("battle_effect_poison");
            catalog.BattleEffectBurn = LoadClip("battle_effect_burn");
            catalog.BattleGainArmor = LoadClip("battle_gain_armor");
            catalog.BattleHealing = LoadClip("battle_healing");
            catalog.BattleHitArmor = LoadClip("battle_hit_armor");
            catalog.BattleHit = LoadClip("battle_hit");

            ConfigureBgmStreaming(catalog.BgmCave);
            ConfigureBgmStreaming(catalog.BgmDungeon);
            ConfigureBgmStreaming(catalog.BgmOceanRuin);
            ConfigureBgmStreaming(catalog.BgmCamp);
            ConfigureBgmStreaming(catalog.BgmBattle);
            ConfigureBgmStreaming(catalog.BgmBattle2);
            ConfigureBgmStreaming(catalog.BgmBattle3);

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static AudioClip LoadClipFirst(params string[] idsWithoutExt)
        {
            foreach (var id in idsWithoutExt)
            {
                var clip = LoadClipQuiet(id);
                if (clip != null)
                    return clip;
            }

            if (idsWithoutExt.Length > 0)
                Debug.LogWarning($"[AudioCatalog] Missing clip: {string.Join(" / ", idsWithoutExt)}");
            return null;
        }

        static AudioClip LoadClipQuiet(string idWithoutExt)
        {
            foreach (var ext in new[] { ".ogg", ".mp3", ".wav" })
            {
                var path = SoundRoot + idWithoutExt + ext;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                    return clip;
            }

            return null;
        }

        static AudioClip LoadClip(string idWithoutExt)
        {
            var clip = LoadClipQuiet(idWithoutExt);
            if (clip == null)
                Debug.LogWarning($"[AudioCatalog] Missing clip: {idWithoutExt}");
            return clip;
        }

        static void ConfigureBgmStreaming(AudioClip clip)
        {
            if (clip == null)
                return;

            var path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path))
                return;

            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                return;

            var settings = importer.defaultSampleSettings;
            var dirty = false;
            if (settings.loadType != AudioClipLoadType.Streaming)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                dirty = true;
            }

            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                dirty = true;
            }

            if (!dirty)
                return;

            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
