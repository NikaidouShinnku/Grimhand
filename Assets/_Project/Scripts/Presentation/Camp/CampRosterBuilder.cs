using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Camp
{
    public static class CampRosterBuilder
    {
        public static readonly IReadOnlyList<string> PlayableCharacterIds = new[]
        {
            "char_knight", "char_mage", "char_ranger"
        };

        public static CampRosterState CreateDefault(
            BattleSetupSO battleSetup,
            ExpeditionSetupSO expeditionSetup)
        {
            var roster = new CampRosterState();
            if (battleSetup == null)
                return roster;

            var catalog = BuildCardCatalog(expeditionSetup);
            foreach (var character in battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                if (roster.Members.Count >= CampRosterState.PartySize)
                    break;

                roster.Members.Add(CreateDefaultMember(character, catalog));
            }

            while (roster.Members.Count < CampRosterState.PartySize)
                roster.Members.Add(new CampMemberLoadout());

            return roster;
        }

        public static CampMemberLoadout CreateDefaultMember(
            CharacterDefinitionSO character,
            IReadOnlyList<CardDefinitionSO> catalog)
        {
            var loadout = new CampMemberLoadout
            {
                CharacterDefinitionId = character.CharacterId,
                DisplayName = character.DisplayName
            };

            foreach (var card in character.Deck)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                loadout.DeckCardIds.Add(card.CardId);
                if (loadout.DeckCardIds.Count >= CampRosterState.DeckSize)
                    break;
            }

            while (loadout.DeckCardIds.Count < CampRosterState.DeckSize)
                loadout.DeckCardIds.Add("");

            return loadout;
        }

        public static List<CardDefinitionSO> BuildCardCatalog(ExpeditionSetupSO expeditionSetup)
        {
            var list = new List<CardDefinitionSO>();
            if (expeditionSetup?.PlayerCardCatalog == null)
                return list;

            var seen = new HashSet<string>();
            foreach (var card in expeditionSetup.PlayerCardCatalog)
            {
                if (card == null || string.IsNullOrEmpty(card.CardId))
                    continue;

                if (seen.Add(card.CardId))
                    list.Add(card);
            }

            return list;
        }

        public static bool IsCardOwnedByCharacter(CardDefinitionSO card, string characterDefinitionId)
        {
            if (card == null || string.IsNullOrEmpty(characterDefinitionId))
                return false;

            return card.OwnerCharacterId == characterDefinitionId;
        }
    }
}
