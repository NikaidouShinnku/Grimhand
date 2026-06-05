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

#if UNITY_EDITOR
            return LoadEditorSprite(consumableId);
#else
            return null;
#endif
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
