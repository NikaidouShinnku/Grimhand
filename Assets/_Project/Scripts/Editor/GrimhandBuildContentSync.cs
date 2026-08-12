#if UNITY_EDITOR
using Grimhand.Content;
using Grimhand.Content.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Grimhand.Editor
{
    /// <summary>
    /// Build 前统一同步正式包所需目录。刻意避免 DeleteAsset/Refresh 等易卡死操作。
    /// </summary>
    public sealed class GrimhandBuildContentSync : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report) => SyncAll(showDialog: false);

        [MenuItem("Grimhand/Content/Sync All Build Content")]
        public static void SyncAllMenu()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Grimhand Sync", "同步 Build 内容…", 0.1f);
                SyncAll(showDialog: true);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void SyncAll(bool showDialog)
        {
            var cards = 0;
            var characters = 0;
            var gifs = 0;

            try
            {
                EditorUtility.DisplayProgressBar("Grimhand Sync", "卡牌目录…", 0.15f);
                cards = CardDefinitionCatalogBinder.BindSilent();

                EditorUtility.DisplayProgressBar("Grimhand Sync", "角色目录…", 0.3f);
                characters = CharacterDefinitionCatalogBinder.BindSilent();

                EditorUtility.DisplayProgressBar("Grimhand Sync", "遗物图标…", 0.45f);
                RelicArtBinder.BindRelicArtSilent();

                EditorUtility.DisplayProgressBar("Grimhand Sync", "消耗品图标…", 0.55f);
                ConsumableArtBinder.BindConsumableArtSilent();

                EditorUtility.DisplayProgressBar("Grimhand Sync", "玩家卡池…", 0.65f);
                PlayerCardCatalogBinder.BindPlayerCardCatalogSilent();

                EditorUtility.DisplayProgressBar("Grimhand Sync", "卡面立绘…", 0.75f);
                var charVisual = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(
                    "Assets/_Project/Data/CharacterVisualCatalog_Demo.asset");
                if (charVisual != null)
                {
                    CardProfileArt.BindAllProfiles(charVisual);
                    EditorUtility.SetDirty(charVisual);
                }

                // 消耗品：覆盖写入 Resources 副本，避免 DeleteAsset 触发大范围重导入
                EditorUtility.DisplayProgressBar("Grimhand Sync", "消耗品 Resources 副本…", 0.85f);
                SyncConsumableCatalogToResources();

                EditorUtility.DisplayProgressBar("Grimhand Sync", "Idle GIF → StreamingAssets…", 0.92f);
                gifs = IdleGifStreamingAssetsCopy.CopyIdleGifsToStreamingAssets();

                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var summary =
                $"Build 内容同步完成：\n" +
                $"· 卡牌目录 {cards}\n" +
                $"· 角色目录 {characters}\n" +
                $"· 遗物 / 消耗品图标 / 玩家卡池 / 卡面立绘\n" +
                $"· Idle GIF → StreamingAssets {gifs}";

            Debug.Log($"[GrimhandBuildContentSync] {summary.Replace("\n", " | ")}");
            if (showDialog)
                EditorUtility.DisplayDialog("Grimhand Build Sync", summary, "OK");
        }

        static void SyncConsumableCatalogToResources()
        {
            const string src = "Assets/_Project/Data/ConsumableVisualCatalog_Demo.asset";
            const string dst = "Assets/_Project/Resources/ConsumableVisualCatalog_Demo.asset";
            var source = AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(src);
            if (source == null)
                return;

            var dest = AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(dst);
            if (dest == null)
            {
                // 仅首次缺失时复制；之后用序列化覆盖，避免反复 DeleteAsset
                AssetDatabase.CopyAsset(src, dst);
                dest = AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(dst);
            }

            if (dest == null)
                return;

            dest.Entries.Clear();
            foreach (var entry in source.Entries)
            {
                if (entry == null)
                    continue;
                dest.Entries.Add(new ConsumableVisualEntry
                {
                    ConsumableId = entry.ConsumableId,
                    Icon = entry.Icon
                });
            }

            EditorUtility.SetDirty(dest);
        }
    }
}
#endif
