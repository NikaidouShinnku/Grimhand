using Grimhand.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Grimhand.Editor
{
    public static class BattleSandboxSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/BattleSandbox.unity";

        [MenuItem("Grimhand/Setup Battle Sandbox Scene")]
        public static void SetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var existing = GameObject.Find("BattleDemo");
            if (existing == null)
            {
                var go = new GameObject("BattleDemo");
                go.AddComponent<BattleDemoController>();
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log($"Battle sandbox saved to {ScenePath}. Press Play to test.");
        }
    }
}
