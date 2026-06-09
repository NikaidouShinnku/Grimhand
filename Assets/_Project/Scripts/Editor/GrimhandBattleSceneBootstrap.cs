#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Content;
using Grimhand.Content.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Grimhand.Editor
{
    /// <summary>一键准备可 Play 的战斗测试场景（SO + UI + 立绘 + Input System）。</summary>
    public static class GrimhandBattleSceneBootstrap
    {
        public const string ScenePath = "Assets/_Project/Scenes/BattleSandbox.unity";
        const string CharCatalogPath = "Assets/_Project/Data/CharacterVisualCatalog_Demo.asset";

        [MenuItem("Grimhand/Open Battle Test Scene", priority = 0)]
        public static void OpenBattleTestScene()
        {
            BootstrapBattleTestScene(showDialog: true);
        }

        /// <summary>供 Unity -executeMethod 调用，无对话框。</summary>
        public static void BootstrapBattleTestSceneBatch()
        {
            BootstrapBattleTestScene(showDialog: false);
            EditorApplication.Exit(0);
        }

        static void BootstrapBattleTestScene(bool showDialog)
        {
            EnsureDemoData();
            EnsureCharacterVisualCatalog();
            GrimhandUiVisualBootstrap.EnsureUiIconCatalog();
            GrimhandUiVisualBootstrap.EnsureCardVisualCatalog();
            GrimhandUiVisualBootstrap.AssignDemoCardRarities();

            if (!System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            BattleUISetup.SetupBattleUIInternal(saveScene: true);

            if (!showDialog)
                return;

            EditorUtility.DisplayDialog(
                "战斗测试场景已就绪",
                "已打开：\nAssets/_Project/Scenes/BattleSandbox.unity\n\n" +
                "直接点击 Unity 顶部的 ▶ Play 即可开始游戏。\n\n" +
                "左右大立绘对峙布局：玩家左、敌人右（镜像），手牌在下方居中。\n" +
                "若看不到立绘，请再次执行本菜单以刷新 UI 与美术目录。",
                "好的");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        public static void EnsureDemoData()
        {
            if (!AssetDatabase.LoadAssetAtPath<BattleSetupSO>(
                    "Assets/_Project/Data/Setups/BattleSetup_Demo.asset"))
            {
                GrimhandContentMenu.GenerateDemoAssetsSilent();
            }
        }

        [MenuItem("Grimhand/Content/Refresh Character Visual Catalog")]
        public static void RefreshCharacterVisualCatalogMenu()
        {
            EnsureCharacterVisualCatalog();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "角色立绘目录已刷新",
                "已更新 CharacterVisualCatalog_Demo.asset。\n若 Play 仍看不到立绘，请重新 Play。",
                "好的");
        }

        public static void EnsureCharacterVisualCatalog()
        {
            EnsureFolder("Assets/_Project/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(CharCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CharCatalogPath);
            }

            catalog.Entries.Clear();
            IdleAnimePivotFixer.FixAll();
            AddCharacterVisuals(catalog, "char_knight", "characters/warrior", "warrior", hitPortraitFacesRight: true);
            AddCharacterVisuals(catalog, "char_mage", "characters/pharoah", "pharoah", hitPortraitFacesRight: false);
            AddCharacterVisuals(catalog, "char_ranger", "characters/devil", "devil", hitPortraitFacesRight: false);
            AddIdleOnlyVisual(catalog, "char_goblin_brute", "monsters/goblin_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_goblin_shaman", "monsters/skeleton_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_goblin_archer", "monsters/wraith_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_goblin", "monsters/goblin_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_slime", "monsters/slime_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_skeleton", "monsters/skeleton_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_skeleton_elite", "monsters/skeleton2_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_wraith", "monsters/wraith_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_wraith_elite", "monsters/wraith2_idle_1024.png");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static void AddCharacterVisuals(
            CharacterVisualCatalogSO catalog,
            string characterId,
            string folder,
            string prefix,
            bool hitPortraitFacesRight)
        {
            var entry = new CharacterVisualEntry
            {
                CharacterId = characterId,
                IdlePortrait = LoadPortraitSprite($"{folder}/{prefix}_idle_1024.png")
            };

            entry.CardProfilePortrait = LoadPortraitSprite($"card/card_profile_{prefix}.png");
            entry.AttackPortrait = LoadPortraitSprite($"{folder}/{prefix}_attack_1024.png");
            entry.DefensePortrait = LoadPortraitSprite($"{folder}/{prefix}_defend_1024.png");
            entry.HitPortrait = LoadPortraitSprite($"{folder}/{prefix}_hit_1024.png");
            entry.DeathPortrait = LoadPortraitSprite($"{folder}/{prefix}_defeat_1024.png");
            entry.HitPortraitFacesRight = hitPortraitFacesRight;
            entry.IdleAnimationGifPath = $"The Grimhands Asset/{folder}/{prefix}_idle_anime.gif";

            if (entry.IdlePortrait == null)
                Debug.LogWarning($"[Grimhand] 未找到立绘：{characterId}（{folder}/{prefix}）");

            catalog.Entries.Add(entry);
        }

        static void AddIdleOnlyVisual(CharacterVisualCatalogSO catalog, string characterId, string relativePath)
        {
            var sprite = LoadPortraitSprite(relativePath);
            if (sprite == null)
                Debug.LogWarning($"[Grimhand] 未找到立绘：{characterId}（{relativePath}）");

            catalog.Entries.Add(new CharacterVisualEntry
            {
                CharacterId = characterId,
                IdlePortrait = sprite,
                AttackPortrait = sprite,
                DefensePortrait = sprite,
                HitPortrait = sprite,
                DeathPortrait = sprite,
                HitPortraitFacesRight = true
            });
        }

        static List<Sprite> LoadSpriteSequence(string relativePath)
        {
            const string root = "Assets/The Grimhands Asset/";
            var path = root + relativePath;
            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return CharacterVisualCatalogSO.FilterAnimationFrames(sprites);
        }

        static void AddPortrait(CharacterVisualCatalogSO catalog, string characterId, string relativePath)
        {
            var sprite = LoadPortraitSprite(relativePath);
            if (sprite == null)
                Debug.LogWarning($"[Grimhand] 未找到立绘：{characterId}（{relativePath}）");

            catalog.Entries.Add(new CharacterVisualEntry
            {
                CharacterId = characterId,
                IdlePortrait = sprite
            });
        }

        public static Sprite LoadPortraitSprite(string relativePath)
        {
            const string root = "Assets/The Grimhands Asset/";
            var folder = System.IO.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
            var file = System.IO.Path.GetFileName(relativePath);
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(file))
                return LoadFirstSprite(root + relativePath);

            var candidates = new[]
            {
                $"{root}{folder}/{file}",
                $"{root}{folder} 1/{file}"
            };

            foreach (var path in candidates)
            {
                var sprite = LoadFirstSprite(path);
                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        public static Sprite LoadFirstSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (direct != null)
                return direct;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
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
