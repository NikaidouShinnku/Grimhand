using System.Collections.Generic;
using Grimhand.Battle;
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

            var rarity = CardRarityTable.GetOrDefault(card.DefinitionId);
            if (definitionsById != null &&
                definitionsById.TryGetValue(card.DefinitionId, out var def) &&
                def != null)
            {
                rarity = def.Rarity;
                var art = def.CardArt;
                var frame = def.CardFrame;
                var icon = def.CardIcon;

                if (art == null && characterVisuals != null)
                    art = characterVisuals.GetCardPortrait(card.OwnerCharacterId);

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
                ? characterVisuals.GetCardPortrait(card.OwnerCharacterId)
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

        public static Dictionary<string, CardDefinitionSO> BuildDefinitionLookup(
            BattleSetupSO setup,
            ExpeditionSetupSO expeditionSetup = null)
        {
            var map = new Dictionary<string, CardDefinitionSO>();
            if (setup != null)
            {
                foreach (var character in setup.Combatants)
                {
                    if (character == null)
                        continue;

                    AddCards(map, character.Deck);
                    AddCards(map, character.SkillPool);
                }
            }

            if (expeditionSetup != null)
                AddCards(map, expeditionSetup.PlayerCardCatalog);

            return map;
        }

        static void AddCards(Dictionary<string, CardDefinitionSO> map, IReadOnlyList<CardDefinitionSO> cards)
        {
            if (cards == null)
                return;

            foreach (var card in cards)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                map[card.CardId] = card;
                CardRarityTable.Register(card.CardId, card.Rarity);
            }
        }

        /// <summary>奖励/远征 catalog 可能只有 DefinitionId；描述与悬停始终以 SO 为准。</summary>
        public static CardInstanceState ResolveForDescription(
            CardInstanceState card,
            IReadOnlyDictionary<string, CardDefinitionSO> definitionsById)
        {
            if (card == null)
                return null;

            if (definitionsById != null
                && definitionsById.TryGetValue(card.DefinitionId, out var def)
                && def != null)
                return CreatePreviewInstance(card.DefinitionId, card.OwnerCharacterId, card.DisplayName, def);

            return card;
        }

        public static CardInstanceState CreatePreviewInstance(
            string definitionId,
            string ownerCharacterId,
            string displayName,
            CardDefinitionSO definition)
        {
            var card = new CardInstanceState
            {
                InstanceId = 0,
                DefinitionId = definitionId ?? "",
                OwnerCharacterId = !string.IsNullOrEmpty(ownerCharacterId)
                    ? ownerCharacterId
                    : definition?.OwnerCharacterId ?? "",
                DisplayName = !string.IsNullOrEmpty(displayName)
                    ? displayName
                    : definition?.DisplayName ?? definitionId ?? "",
                Cost = definition?.Cost ?? 1,
                CardType = definition?.CardType ?? CardType.Attack,
                IsUsable = false
            };

            if (definition == null)
                return card;

            foreach (var keyword in definition.Keywords)
                card.Keywords.Add(keyword);

            foreach (var action in definition.Actions)
                card.Actions.Add(action.ToSpec());

            return card;
        }
    }
}
