using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    [Serializable]
    public sealed class RelicVisualEntry
    {
        public string RelicId = "";
        public Sprite Icon;
    }

    [CreateAssetMenu(fileName = "RelicVisualCatalog", menuName = "Grimhand/Relic Visual Catalog")]
    public class RelicVisualCatalogSO : ScriptableObject
    {
        public List<RelicVisualEntry> Entries = new();

        public Sprite GetIcon(string relicId)
        {
            if (string.IsNullOrEmpty(relicId))
                return null;

            foreach (var entry in Entries)
            {
                if (entry != null && entry.RelicId == relicId && entry.Icon != null)
                    return entry.Icon;
            }

#if UNITY_EDITOR
            return LoadEditorSprite(relicId);
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        static Sprite LoadEditorSprite(string relicId)
        {
            var path = $"Assets/The Grimhands Asset/relics/{relicId}.png";
            return RelicSpriteResolver.PickBest(
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path),
                relicId);
        }
#endif
    }
}
