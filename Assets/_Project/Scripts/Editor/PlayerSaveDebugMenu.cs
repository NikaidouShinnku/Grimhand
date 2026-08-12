using System;
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
        /// <summary>与 <c>MetaProgressionRules.MaxOutOfRunLevel</c> 保持一致（Editor 不直接引用 Expedition 程序集）。</summary>
        const int MaxOutOfRunLevel = 10;

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
                    $"将删除整个存档目录：\n{path}\n\n下次 Play 将创建新局（含新手教程）。",
                    "删除",
                    "取消"))
            {
                return;
            }

            Directory.Delete(path, recursive: true);
            Debug.Log("[Save] 存档已删除。");
        }

        [MenuItem(MenuRoot + "Max All Character Levels (Out-of-Run Lv.10)")]
        public static void MaxAllCharacterLevels()
        {
            var dir = SaveService.DefaultSaveDirectory;
            var path = Path.Combine(dir, "profile.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Save] 未找到存档: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            var dto = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            if (dto?.characters == null || dto.characters.Length == 0)
            {
                Debug.LogWarning("[Save] 存档无角色数据。");
                return;
            }

            foreach (var c in dto.characters)
            {
                if (c == null)
                    continue;
                c.outOfRunLevel = MaxOutOfRunLevel;
                c.outOfRunXp = 0;
            }

            dto.lastSavedUtc = DateTime.UtcNow.ToString("o");
            SaveIntegrity.ApplyHash(dto);
            File.WriteAllText(path, JsonUtility.ToJson(dto, prettyPrint: true));
            Debug.Log($"[Save] 已将 {dto.characters.Length} 名角色调至局外满级 "
                + $"Lv.{MaxOutOfRunLevel}：{path}");
        }

        [MenuItem(MenuRoot + "Save Now (Play Mode)")]
        public static void SaveNowPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Save] 请在 Play Mode 下使用。");
                return;
            }

            var controller = UnityEngine.Object.FindAnyObjectByType<Presentation.Camp.GameFlowController>();
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
