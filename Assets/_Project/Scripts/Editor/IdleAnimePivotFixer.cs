#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    /// <summary>将 idle_anime spritesheet 每帧 pivot 统一为 Center（直接写 .meta，兼容 Unity 6）。</summary>
    public static class IdleAnimePivotFixer
    {
        public static readonly string[] IdleAnimePaths =
        {
            "Assets/The Grimhands Asset/warrior/warrior_idle_anime.png",
            "Assets/The Grimhands Asset/pharoah/pharoah_idle_anime.png",
            "Assets/The Grimhands Asset/devil/devil_idle_anime.png"
        };

        static readonly Regex PivotBlockRegex = new(
            @"alignment: 0\s*\n\s*pivot: \{x: 0, y: 0\}",
            RegexOptions.Multiline);

        [MenuItem("Grimhand/Content/Fix Idle Animation Sprite Pivots")]
        public static void FixAllMenu()
        {
            FixAll();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(
                "Idle 动画 Pivot 已统一",
                "已将 warrior / pharoah / devil 的 idle_anime 每帧 pivot 设为 Center。\n" +
                "请重新 Play 验证 idle 是否在原地播放。",
                "好的");
        }

        public static void FixAll()
        {
            foreach (var path in IdleAnimePaths)
                FixAsset(path);
        }

        public static void FixAsset(string assetPath)
        {
            var metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                Debug.LogWarning($"[Grimhand] 未找到 meta：{metaPath}");
                return;
            }

            var text = File.ReadAllText(metaPath);
            var updated = PivotBlockRegex.Replace(
                text,
                "alignment: 9\n      pivot: {x: 0.5, y: 0.5}");

            if (text == updated)
                return;

            File.WriteAllText(metaPath, updated);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Grimhand] 已统一 idle pivot：{assetPath}");
        }
    }
}
#endif
