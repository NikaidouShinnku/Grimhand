#if UNITY_EDITOR
using Grimhand.Content;
using Grimhand.Content.Editor;
using Grimhand.Presentation;
using Grimhand.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Grimhand.Editor
{
    public static class GrimhandBossTestSceneBootstrap
    {
        public const string ScenePath = "Assets/_Project/Scenes/BossTestSandbox.unity";
        const string ExpeditionSetupPath = "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset";
        const string GhostQueenSetupPath = "Assets/_Project/Data/Setups/BattleSetup_Encounter_GhostQueenBoss.asset";

        [MenuItem("Grimhand/Open Ghost Queen Boss Test Scene", priority = 1)]
        public static void OpenGhostQueenBossTestScene()
        {
            GrimhandBattleSceneBootstrap.EnsureDemoData();
            GrimhandBattleSceneBootstrap.EnsureCharacterVisualCatalog();
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

            BattleUISetup.SetupBattleUIInternal(saveScene: false);
            WireBossTestController();

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                ScenePath);

            EditorUtility.DisplayDialog(
                "幽灵女王 Boss 测试场景",
                "已打开：\nAssets/_Project/Scenes/BossTestSandbox.unity\n\n" +
                "开局：全队 Lv.7 · 随机 3 遗物 · 每人随机 3 张职业卡 · 直战幽灵女王。\n\n" +
                "常规远征仍用 BattleSandbox；本场景不影响正式 Demo 牌库。",
                "好的");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        static void WireBossTestController()
        {
            var demoGo = GameObject.Find("BattleDemo");
            if (demoGo == null)
                demoGo = new GameObject("BattleDemo");

            foreach (var legacy in demoGo.GetComponents<BattleScreenController>())
                Object.DestroyImmediate(legacy);

            var legacyDemo = demoGo.GetComponent<BattleDemoController>();
            if (legacyDemo != null)
                legacyDemo.enabled = false;

            var controller = demoGo.GetComponent<GhostQueenBossTestController>();
            if (controller == null)
                controller = demoGo.AddComponent<GhostQueenBossTestController>();

            var view = Object.FindAnyObjectByType<BattleScreenView>();
            var expedition = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(ExpeditionSetupPath);
            var ghostQueen = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(GhostQueenSetupPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogSO>(
                GrimhandUiVisualBootstrap.CardCatalogPath);
            var charCatalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(
                "Assets/_Project/Data/CharacterVisualCatalog_Demo.asset");
            var iconCatalog = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(
                GrimhandUiVisualBootstrap.IconCatalogPath);
            var relicCatalog = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(
                "Assets/_Project/Data/RelicVisualCatalog_Demo.asset");
            var effectCatalog = AssetDatabase.LoadAssetAtPath<BattleActionEffectCatalogSO>(
                BattleEffectArtBinder.CatalogPath);

            var so = new SerializedObject(controller);
            so.FindProperty("expeditionSetup").objectReferenceValue = expedition;
            so.FindProperty("ghostQueenBossSetup").objectReferenceValue = ghostQueen;
            so.FindProperty("cardVisualCatalog").objectReferenceValue = catalog;
            so.FindProperty("characterVisualCatalog").objectReferenceValue = charCatalog;
            so.FindProperty("uiIconCatalog").objectReferenceValue = iconCatalog;
            so.FindProperty("relicVisualCatalog").objectReferenceValue = relicCatalog;
            so.FindProperty("actionEffectCatalog").objectReferenceValue = effectCatalog;
            so.FindProperty("screenView").objectReferenceValue = view;
            so.FindProperty("disableLegacyImGui").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
