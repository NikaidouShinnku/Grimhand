using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grimhand.Content
{
    [Serializable]
    public sealed class CharacterVisualEntry
    {
        public string CharacterId = "";
        public Sprite IdlePortrait;
    }

    [CreateAssetMenu(fileName = "CharacterVisualCatalog", menuName = "Grimhand/Character Visual Catalog")]
    public class CharacterVisualCatalogSO : ScriptableObject
    {
        public Sprite DefaultPortrait;
        public List<CharacterVisualEntry> Entries = new();

        public Sprite GetPortrait(string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(characterDefinitionId))
                return DefaultPortrait;

            foreach (var entry in Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.CharacterId))
                    continue;
                if (entry.CharacterId == characterDefinitionId && entry.IdlePortrait != null)
                    return entry.IdlePortrait;
            }

            return DefaultPortrait;
        }
    }
}
