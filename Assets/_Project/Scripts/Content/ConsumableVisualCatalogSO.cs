using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    [Serializable]
    public sealed class ConsumableVisualEntry
    {
        public string ConsumableId = "";
        public Sprite Icon;
    }

    [CreateAssetMenu(fileName = "ConsumableVisualCatalog", menuName = "Grimhand/Consumable Visual Catalog")]
    public class ConsumableVisualCatalogSO : ScriptableObject
    {
        public List<ConsumableVisualEntry> Entries = new();

        static readonly Dictionary<string, string> AssetNameOverrides = new()
        {
            ["spring_bottle"] = "spring_water",
            ["scroll_page"] = "ancient_scroll"
        };

        public Sprite GetIcon(string consumableId)
        {
            if (string.IsNullOrEmpty(consumableId))
                return null;

            foreach (var entry in Entries)
            {
                if (entry != null && entry.ConsumableId == consumableId && entry.Icon != null)
                    return entry.Icon;
            }

            var fallback = ResolveFallbackCatalog();
            if (fallback != null && !ReferenceEquals(fallback, this))
            {
                var fromFallback = fallback.GetIconFromEntriesOnly(consumableId);
                if (fromFallback != null)
                    return fromFallback;
            }

#if UNITY_EDITOR
            return LoadEditorSprite(consumableId);
#else
            return null;
#endif
        }

        Sprite GetIconFromEntriesOnly(string consumableId)
        {
            foreach (var entry in Entries)
            {
                if (entry != null && entry.ConsumableId == consumableId && entry.Icon != null)
                    return entry.Icon;
            }

            return null;
        }

        static ConsumableVisualCatalogSO _fallbackCatalog;

        static ConsumableVisualCatalogSO ResolveFallbackCatalog()
        {
            if (_fallbackCatalog != null)
                return _fallbackCatalog;

            _fallbackCatalog = Resources.Load<ConsumableVisualCatalogSO>("ConsumableVisualCatalog_Demo");
#if UNITY_EDITOR
            if (_fallbackCatalog == null)
            {
                _fallbackCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ConsumableVisualCatalogSO>(
                    "Assets/_Project/Data/ConsumableVisualCatalog_Demo.asset");
            }
#endif
            return _fallbackCatalog;
        }

#if UNITY_EDITOR
        static Sprite LoadEditorSprite(string consumableId)
        {
            var fileName = AssetNameOverrides.TryGetValue(consumableId, out var mapped)
                ? mapped
                : consumableId;
            var path = $"Assets/The Grimhands Asset/consumables/{fileName}.png";
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
