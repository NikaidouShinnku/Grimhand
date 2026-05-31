using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;

namespace Grimhand.Presentation.Battle
{
    public static class CardVisualResolver
    {
        public static CardVisual Resolve(
            CardInstanceState card,
            CardVisualCatalogSO catalog,
            IReadOnlyDictionary<string, CardDefinitionSO> definitionsById)
        {
            if (card == null)
                return CardVisual.Empty;

            if (definitionsById != null &&
                definitionsById.TryGetValue(card.DefinitionId, out var def) &&
                def != null)
            {
                var art = def.CardArt;
                var frame = def.CardFrame;
                var icon = def.CardIcon;

                if (catalog != null)
                {
                    if (art == null) art = catalog.Resolve(card.DefinitionId, card.CardType).Art;
                    if (frame == null) frame = catalog.GetDefaultFrame(card.CardType);
                }

                return new CardVisual(art, frame, icon);
            }

            return catalog != null
                ? catalog.Resolve(card.DefinitionId, card.CardType)
                : CardVisual.Empty;
        }

        public static Dictionary<string, CardDefinitionSO> BuildDefinitionLookup(BattleSetupSO setup)
        {
            var map = new Dictionary<string, CardDefinitionSO>();
            if (setup == null)
                return map;

            foreach (var character in setup.Combatants)
            {
                if (character == null)
                    continue;

                foreach (var card in character.Deck)
                {
                    if (card == null || string.IsNullOrEmpty(card.CardId))
                        continue;

                    map[card.CardId] = card;
                }
            }

            return map;
        }
    }
}
