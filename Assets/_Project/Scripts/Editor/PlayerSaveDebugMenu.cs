using System.IO;
using Grimhand.Persistence;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Grimhand.Editor
{
    public static class PlayerSaveDebugMenu
    {
        const string MenuRoot = "Grimhand/Save/";

        [MenuItem(MenuRoot + "Log Save Path")]
        public static void LogSavePath()
        {
            var path = SaveService.DefaultSaveDirectory;
            Debug.Log($"[Save] 存档目录: {path}");
            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log("[Save] 路径已复制到剪贴板。");
        }

        [MenuItem(MenuRoot + "Open Save Folder")]
        public static void OpenSaveFolder()
        {
            var path = SaveService.DefaultSaveDirectory;
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Delete Save (New Game)")]
        public static void DeleteSave()
        {
            var path = SaveService.DefaultSaveDirectory;
            if (!Directory.Exists(path))
            {
                Debug.Log("[Save] 无存档目录，无需删除。");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "删除存档",
                    $"将删除整个存档目录：\n{path}\n\n下次 Play 将创建新局（等级 0）。",
                    "删除",
                    "取消"))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
            Debug.Log("[Save] 存档已删除。");
        }

        [MenuItem(MenuRoot + "Save Now (Play Mode)")]
        public static void SaveNowPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Save] 请在 Play Mode 下使用。");
                return;
            }

            var controller = Object.FindAnyObjectByType<Presentation.Camp.GameFlowController>();
            if (controller == null)
            {
                Debug.LogWarning("[Save] 场景中未找到 GameFlowController。");
                return;
            }

            controller.SaveProfile();
            Debug.Log("[Save] 已手动写盘。");
        }
    }
}
