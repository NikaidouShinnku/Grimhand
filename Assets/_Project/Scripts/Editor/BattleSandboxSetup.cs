using Grimhand.Content;
using Grimhand.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Grimhand.Editor
{
    public static class BattleSandboxSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/BattleSandbox.unity";
        const string SetupPath = "Assets/_Project/Data/Setups/BattleSetup_Demo.asset";

        [MenuItem("Grimhand/Setup Battle Sandbox Scene")]
        public static void SetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            BattleDemoController controller;
            var existing = GameObject.Find("BattleDemo");
            if (existing == null)
            {
                var go = new GameObject("BattleDemo");
                controller = go.AddComponent<BattleDemoController>();
            }
            else
            {
                controller = existing.GetComponent<BattleDemoController>();
                if (controller == null)
                    controller = existing.AddComponent<BattleDemoController>();
            }

            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupSO>(SetupPath);
            if (setup != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("battleSetup").objectReferenceValue = setup;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath);

            if (setup == null)
            {
                Debug.LogWarning(
                    "场景已创建，但未找到 BattleSetup_Demo.asset。\n" +
                    "请先执行：Grimhand → Content → Generate Demo ScriptableObjects");
            }
            else
            {
                Debug.Log($"Battle sandbox saved. Battle Setup 已绑定。Press Play to test.");
            }
        }
    }
}
