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
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
#endif
    }
}
