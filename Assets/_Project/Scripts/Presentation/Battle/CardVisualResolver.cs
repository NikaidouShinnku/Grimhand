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
            CharacterVisualCatalogSO characterVisuals,
            IReadOnlyDictionary<string, CardDefinitionSO> definitionsById)
        {
            if (card == null)
                return CardVisual.Empty;

            var rarity = CardRarity.Common;
            if (definitionsById != null &&
                definitionsById.TryGetValue(card.DefinitionId, out var def) &&
                def != null)
            {
                rarity = def.Rarity;
                var art = def.CardArt;
                var frame = def.CardFrame;
                var icon = def.CardIcon;

                if (art == null && characterVisuals != null)
                    art = characterVisuals.GetPortrait(card.OwnerCharacterId);

                if (catalog != null)
                {
                    if (art == null)
                        art = catalog.Resolve(card.DefinitionId, card.CardType, rarity).Art;
                    if (frame == null)
                        frame = catalog.GetFrame(card.CardType, rarity);
                }

                return new CardVisual(art, frame, icon);
            }

            var fallbackArt = characterVisuals != null
                ? characterVisuals.GetPortrait(card.OwnerCharacterId)
                : null;

            if (catalog != null)
            {
                var resolved = catalog.Resolve(card.DefinitionId, card.CardType, rarity);
                if (fallbackArt != null && resolved.Art == null)
                    return new CardVisual(fallbackArt, resolved.Frame, resolved.Icon);
                return resolved;
            }

            return new CardVisual(fallbackArt, null, null);
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
