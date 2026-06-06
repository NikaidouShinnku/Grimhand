using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle
{
    public static class CardRarityTable
    {
        static readonly Dictionary<string, CardRarity> ById = new();

        public static void Register(string definitionId, CardRarity rarity)
        {
            if (string.IsNullOrEmpty(definitionId))
                return;

            ById[definitionId] = rarity;
        }

        public static CardRarity GetOrDefault(string definitionId) =>
            !string.IsNullOrEmpty(definitionId) && ById.TryGetValue(definitionId, out var rarity)
                ? rarity
                : CardRarity.Common;

        public static void Clear() => ById.Clear();
    }
}
