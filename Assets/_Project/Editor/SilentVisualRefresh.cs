#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    public static class SilentVisualRefresh
    {
        [MenuItem("Grimhand/Content/_Silent Refresh All Visual Catalogs")]
        public static void RefreshAllSilent()
        {
            GrimhandBattleSceneBootstrap.EnsureCharacterVisualCatalog();
            GrimhandUiVisualBootstrap.EnsureUiIconCatalog();
            GrimhandUiVisualBootstrap.EnsureCardVisualCatalog();
            GrimhandUiVisualBootstrap.AssignDemoCardRarities();
            AssetDatabase.SaveAssets();
            Debug.Log("[SilentVisualRefresh] 角色立绘目录 + 卡牌视觉目录已刷新并保存。");
        }
    }
}
#endif
