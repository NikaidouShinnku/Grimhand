#if UNITY_EDITOR
using Grimhand.Content;
using Grimhand.Content.Editor;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    /// <summary>进入 Play 前若 v0.9 Boss 立绘 pose 绑定无效，则自动 Upsert 并保存。</summary>
    [InitializeOnLoad]
    static class BossVisualCatalogAutoSync
    {
        const string CatalogPath = "Assets/_Project/Data/CharacterVisualCatalog_Demo.asset";

        static readonly string[] BossCharacterIds =
        {
            BossCharacterRules.Warden,
            BossCharacterRules.DarkKnight,
            BossCharacterRules.CorruptedOceanGoddess,
            BossCharacterRules.PrisonCage
        };

        static BossVisualCatalogAutoSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            TrySyncMissingBossVisuals();
        }

        static void TrySyncMissingBossVisuals()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(CatalogPath);
            if (catalog == null)
                return;

            var needsRefresh = false;
            foreach (var characterId in BossCharacterIds)
            {
                if (!HasValidBossVisual(catalog, characterId))
                {
                    needsRefresh = true;
                    break;
                }
            }

            if (!needsRefresh)
                return;

            MonsterContentGenerator.UpdateBossVisualCatalog(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[BossVisualCatalogAutoSync] 已刷新 v0.9 Boss 立绘 pose 绑定。");
        }

        static bool HasValidBossVisual(CharacterVisualCatalogSO catalog, string characterId)
        {
            var entry = catalog.GetEntry(characterId);
            if (entry?.IdlePortrait == null)
                return false;

            return HasValidPoseSprite(entry.AttackPortrait, entry.IdlePortrait)
                && HasValidPoseSprite(entry.DefensePortrait, entry.IdlePortrait)
                && HasValidPoseSprite(entry.HitPortrait, entry.IdlePortrait)
                && HasValidPoseSprite(entry.DeathPortrait, entry.IdlePortrait);
        }

        static bool HasValidPoseSprite(Sprite sprite, Sprite idle)
        {
            if (sprite == null || idle == null)
                return false;

            var poseArea = sprite.rect.width * sprite.rect.height;
            var idleArea = idle.rect.width * idle.rect.height;
            return poseArea >= idleArea * 0.45f;
        }
    }
}
#endif
