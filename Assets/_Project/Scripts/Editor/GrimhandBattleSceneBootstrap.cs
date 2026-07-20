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
            CampUISetup.SetupCampUIInternal(saveScene: true);

            if (!showDialog)
                return;

            EditorUtility.DisplayDialog(
                "战斗测试场景已就绪",
                "已打开：\nAssets/_Project/Scenes/BattleSandbox.unity\n\n" +
                "直接点击 Unity 顶部的 ▶ Play 即可开始游戏。\n\n" +
                "进入后先显示营地界面：军营配队 → 传送门开始 Demo 远征。\n" +
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

        [MenuItem("Grimhand/Content/Bind Card Profiles")]
        public static void BindCardProfilesMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(CharCatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("卡面绑定", "未找到 CharacterVisualCatalog_Demo.asset", "好的");
                return;
            }

            AssetDatabase.ImportAsset(
                CardProfileArt.ProfileFolder,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            CardProfileArt.BindAllProfiles(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "卡面绑定",
                "已将 card/card_profile 下全部卡面按角色绑定到视觉目录。",
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
            AddCharacterVisuals(catalog, "char_snake_queen", "characters/snake queen", "snakequeen", hitPortraitFacesRight: false);
            AddCharacterVisuals(catalog, "char_lich_queen", "characters/lich queen", "lichqueen", hitPortraitFacesRight: false);
            FixLichQueenDeathPortrait(catalog);
            FixLichQueenPosePortraits(catalog);
            AddIdleOnlyVisual(catalog, "char_goblin", "monsters/goblin_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_slime", "monsters/slime_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_skeleton", "monsters/skeleton_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_skeleton_elite", "monsters/skeleton2_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_wraith", "monsters/wraith_idle_1024.png");
            AddIdleOnlyVisual(catalog, "char_wraith_elite", "monsters/wraith2_idle_1024.png");

            // Boss / 特殊敌人（含 idle GIF 动画）；Upsert 不会覆盖已有玩家条目。
            MonsterContentGenerator.UpdateVisualCatalog(catalog);
            CardProfileArt.BindAllProfiles(catalog);

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

            entry.CardProfilePortrait = CardProfileArt.LoadSprite(characterId);
            entry.AttackPortrait = LoadPortraitSprite($"{folder}/{prefix}_attack_1024.png");
            entry.DefensePortrait = LoadPortraitSprite($"{folder}/{prefix}_defend_1024.png");
            entry.HitPortrait = LoadPortraitSprite($"{folder}/{prefix}_hit_1024.png");
            entry.DeathPortrait = LoadPortraitSprite($"{folder}/{prefix}_defeat_1024.png");
            entry.HitPortraitFacesRight = hitPortraitFacesRight;
            entry.PreserveOriginalFacing = true;
            entry.IdleAnimationGifPath = $"The Grimhands Asset/{folder}/{prefix}_idle_anime.gif";

            if (entry.IdlePortrait == null)
                Debug.LogWarning($"[Grimhand] 未找到立绘：{characterId}（{folder}/{prefix}）");

            catalog.Entries.Add(entry);
        }

        static void FixLichQueenDeathPortrait(CharacterVisualCatalogSO catalog)
        {
            // 巫妖女王的死亡立绘文件名为 lichqueen_death_1024.png（非 _defeat_），单独补上。
            var entry = catalog.Entries.Find(e => e.CharacterId == "char_lich_queen");
            if (entry != null && entry.DeathPortrait == null)
                entry.DeathPortrait = LoadPortraitSprite("characters/lich queen/lichqueen_death_1024.png");
        }

        static void FixLichQueenPosePortraits(CharacterVisualCatalogSO catalog)
        {
            var entry = catalog.Entries.Find(e => e.CharacterId == "char_lich_queen");
            if (entry == null)
                return;

            // 图集含多个子图；必须绑定 _0 主立绘，否则会显示碎片（绿块）。
            entry.HitPortrait = LoadPortraitSprite("characters/lich queen/lichqueen_hit_1024.png");
            entry.DefensePortrait = LoadPortraitSprite("characters/lich queen/lichqueen_defend_1024.png");
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
                HitPortraitFacesRight = false,
                PreserveOriginalFacing = true
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

            var sprites = new List<Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
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

            foreach (var sprite in sprites)
            {
                if (sprite.name.EndsWith("_0"))
                    return sprite;
            }

            Sprite best = null;
            var bestArea = 0f;
            foreach (var sprite in sprites)
            {
                if (!CharacterVisualCatalogSO.IsValidAnimationFrame(sprite))
                    continue;

                var area = sprite.rect.width * sprite.rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = sprite;
                }
            }

            return best ?? sprites[0];
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
