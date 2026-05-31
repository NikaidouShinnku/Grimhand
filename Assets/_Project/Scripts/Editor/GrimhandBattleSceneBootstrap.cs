#if UNITY_EDITOR
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
                "上方战场应显示 6 个角色立绘槽位（战士/法老/恶魔 + 怪物）。\n" +
                "若看不到，请再次执行本菜单以刷新 UI 布局。",
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
            AddPortrait(catalog, "char_knight",
                "Assets/The Grimhands Asset/warrior/warrior_idle_1024.png");
            AddPortrait(catalog, "char_mage",
                "Assets/The Grimhands Asset/pharoah/pharoah_idle_1024.png");
            AddPortrait(catalog, "char_ranger",
                "Assets/The Grimhands Asset/devil/devil_idle_1024.png");
            AddPortrait(catalog, "char_goblin_brute",
                "Assets/The Grimhands Asset/monsters/goblin_idle_1024.png");
            AddPortrait(catalog, "char_goblin_shaman",
                "Assets/The Grimhands Asset/monsters/skeleton_idle_1024.png");
            AddPortrait(catalog, "char_goblin_archer",
                "Assets/The Grimhands Asset/monsters/wraith_idle_1024.png");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static void AddPortrait(CharacterVisualCatalogSO catalog, string characterId, string texturePath)
        {
            catalog.Entries.Add(new CharacterVisualEntry
            {
                CharacterId = characterId,
                IdlePortrait = LoadFirstSprite(texturePath)
            });
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

            return null;
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
