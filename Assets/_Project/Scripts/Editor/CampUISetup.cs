#if UNITY_EDITOR
using Grimhand.Content;
using Grimhand.Presentation.Battle;
using Grimhand.Presentation.Camp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Editor
{
    public static class CampUISetup
    {
        const string CampBackgroundPath = "Assets/The Grimhands Asset/path and background/campsite_background.png";
        const string PortalBackgroundPath = "Assets/The Grimhands Asset/path and background/portal_background.png";

        [MenuItem("Grimhand/Setup Camp UI in Scene", priority = 11)]
        public static void SetupCampUIMenu()
        {
            SetupCampUIInternal(saveScene: false);
            EditorUtility.DisplayDialog(
                "营地 UI 已搭建",
                "已在当前场景创建 CampScreen。\n\n" +
                "Play 后先进入营地，点击「军营」配队，「传送门」开始 Demo 远征。\n\n" +
                "推荐：Grimhand → Open Battle Test Scene 一键准备。",
                "好的");
        }

        public static void SetupCampUIInternal(bool saveScene)
        {
            BattleUISetup.SetupBattleUIInternal(saveScene: false);
            GrimhandUiVisualBootstrap.EnsureUiIconCatalog();
            AssetDatabase.SaveAssets();

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[CampUISetup] 未找到 Canvas，请先执行 Setup Battle UI。");
                return;
            }

            var campRoot = canvas.transform.Find("CampScreen");
            GameObject campGo;
            if (campRoot == null)
            {
                campGo = new GameObject("CampScreen", typeof(RectTransform), typeof(CampScreenView));
                campGo.transform.SetParent(canvas.transform, false);
                var rt = campGo.GetComponent<RectTransform>();
                StretchFull(rt);
            }
            else
            {
                campGo = campRoot.gameObject;
            }

            var campView = campGo.GetComponent<CampScreenView>();
            if (campView == null)
                campView = campGo.AddComponent<CampScreenView>();

            var iconCatalog = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(
                GrimhandUiVisualBootstrap.IconCatalogPath);

            var soCamp = new SerializedObject(campView);
            soCamp.FindProperty("uiIcons").objectReferenceValue = iconCatalog;
            soCamp.ApplyModifiedPropertiesWithoutUndo();

            EnsureOverlayHost(canvas.transform, out var overlayHost);
            EnsureOverlayHostOnTop(canvas.transform, overlayHost);
            var champion = EnsureComponent<ChampionCampOverlayView>(overlayHost, "ChampionCampOverlay");
            var portal = EnsureComponent<PortalOverlayView>(overlayHost, "PortalOverlay");
            var soPortal = new SerializedObject(portal);
            soPortal.FindProperty("portalBackground").objectReferenceValue =
                GrimhandBattleSceneBootstrap.LoadFirstSprite(PortalBackgroundPath);
            soPortal.ApplyModifiedPropertiesWithoutUndo();

            WireGameFlowController(campView, champion, portal);

            campGo.transform.SetSiblingIndex(canvas.transform.childCount - 2);

            var battleScreen = canvas.transform.Find("BattleScreen");
            if (battleScreen != null)
                battleScreen.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if (saveScene)
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                    GrimhandBattleSceneBootstrap.ScenePath);

            Selection.activeGameObject = campGo;
        }

        static void WireGameFlowController(
            CampScreenView campView,
            ChampionCampOverlayView championCamp,
            PortalOverlayView portalOverlay)
        {
            var demoGo = GameObject.Find("BattleDemo");
            if (demoGo == null)
            {
                demoGo = new GameObject("BattleDemo");
                Undo.RegisterCreatedObjectUndo(demoGo, "Create BattleDemo");
            }

            var flow = demoGo.GetComponent<GameFlowController>();
            if (flow == null)
                flow = demoGo.AddComponent<GameFlowController>();

            var controller = demoGo.GetComponent<BattleScreenController>();
            if (controller == null)
                controller = demoGo.AddComponent<BattleScreenController>();

            var battleSetup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(
                "Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            var expeditionSetup = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(
                "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");

            BattleUISetup.SetupBattleUIInternal(saveScene: false);
            var view = Object.FindAnyObjectByType<BattleScreenView>();

            var soFlow = new SerializedObject(flow);
            soFlow.FindProperty("battleController").objectReferenceValue = controller;
            soFlow.FindProperty("campScreen").objectReferenceValue = campView;
            soFlow.FindProperty("championCamp").objectReferenceValue = championCamp;
            soFlow.FindProperty("portalOverlay").objectReferenceValue = portalOverlay;
            soFlow.FindProperty("battleSetup").objectReferenceValue = battleSetup;
            soFlow.FindProperty("expeditionSetup").objectReferenceValue = expeditionSetup;
            soFlow.FindProperty("startAtCamp").boolValue = true;
            soFlow.ApplyModifiedPropertiesWithoutUndo();

            var soController = new SerializedObject(controller);
            soController.FindProperty("battleSetup").objectReferenceValue = battleSetup;
            soController.FindProperty("expeditionSetup").objectReferenceValue = expeditionSetup;
            soController.FindProperty("screenView").objectReferenceValue = view;
            soController.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureOverlayHost(Transform canvas, out Transform host)
        {
            var existing = canvas.Find("CampOverlays");
            if (existing == null)
            {
                var go = new GameObject("CampOverlays", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                StretchFull(go.GetComponent<RectTransform>());
                host = go.transform;
            }
            else
            {
                host = existing;
            }
        }

        static void EnsureOverlayHostOnTop(Transform canvas, Transform overlayHost)
        {
            overlayHost.SetAsLastSibling();
        }

        static T EnsureComponent<T>(Transform parent, string name) where T : Component
        {
            var child = parent.Find(name);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                StretchFull(go.GetComponent<RectTransform>());
            }
            else
            {
                go = child.gameObject;
            }

            var comp = go.GetComponent<T>();
            if (comp == null)
                comp = go.AddComponent<T>();
            return comp;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
