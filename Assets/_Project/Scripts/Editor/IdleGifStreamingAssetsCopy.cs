#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Grimhand.Content;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    /// <summary>
    /// 把 catalog 引用的 idle GIF 拷到 StreamingAssets，供正式包运行时读取。
    /// Build 时由 GrimhandBuildContentSync 调用。
    /// 注意：不要 AssetDatabase.Refresh 这些 GIF，否则编辑器会当纹理导入导致卡死。
    /// </summary>
    public static class IdleGifStreamingAssetsCopy
    {
        [MenuItem("Grimhand/Content/Copy Idle GIFs to StreamingAssets")]
        public static void CopyIdleGifsToStreamingAssetsMenu()
        {
            var count = CopyIdleGifsToStreamingAssets();
            EditorUtility.DisplayDialog(
                "Idle GIF",
                $"已同步 {count} 个 GIF 到 StreamingAssets（未触发导入刷新）。\n正式包动画依赖此步骤（Build 时也会自动执行）。",
                "OK");
        }

        public static int CopyIdleGifsToStreamingAssets()
        {
            var paths = CollectGifRelativePaths();
            var copied = 0;
            foreach (var relative in paths)
            {
                if (string.IsNullOrEmpty(relative))
                    continue;

                var src = Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(src))
                {
                    Debug.LogWarning($"[IdleGifCopy] 源文件不存在: {src}");
                    continue;
                }

                var dst = Path.Combine(Application.streamingAssetsPath, relative.Replace('/', Path.DirectorySeparatorChar));
                var dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir))
                    Directory.CreateDirectory(dstDir);

                File.Copy(src, dst, overwrite: true);
                EnsureDefaultImporterMeta(dst);
                copied++;
            }

            // 刻意不调用 AssetDatabase.Refresh：StreamingAssets 只需原始文件可读
            Debug.Log($"[IdleGifCopy] 已拷贝 {copied}/{paths.Count} 个 idle GIF → StreamingAssets（跳过 Refresh）");
            return copied;
        }

        static void EnsureDefaultImporterMeta(string gifPath)
        {
            var metaPath = gifPath + ".meta";
            if (File.Exists(metaPath))
                return;

            var guid = GUID.Generate().ToString();
            var meta =
                "fileFormatVersion: 2\n" +
                $"guid: {guid}\n" +
                "DefaultImporter:\n" +
                "  externalObjects: {}\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n";
            File.WriteAllText(metaPath, meta);
        }

        static HashSet<string> CollectGifRelativePaths()
        {
            var result = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:CharacterVisualCatalogSO");
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(assetPath);
                if (catalog?.Entries == null)
                    continue;

                foreach (var entry in catalog.Entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.IdleAnimationGifPath))
                        continue;
                    result.Add(entry.IdleAnimationGifPath.Replace('\\', '/').TrimStart('/'));
                }
            }

            return result;
        }
    }
}
#endif
