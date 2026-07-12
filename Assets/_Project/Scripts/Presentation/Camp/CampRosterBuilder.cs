using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Camp
{
    public static class CampRosterBuilder
    {
        public static readonly IReadOnlyList<string> PlayableCharacterIds = new[]
        {
            "char_knight", "char_mage", "char_ranger",
            "char_snake_queen", "char_lich_queen"
        };

        public static CampRosterState CreateDefault(
            BattleSetupSO battleSetup,
            ExpeditionSetupSO expeditionSetup)
        {
            var roster = new CampRosterState();
            if (battleSetup == null)
                return roster;

            foreach (var character in battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                if (roster.Members.Count >= CampRosterState.PartySize)
                    break;

                roster.Members.Add(CreateEmptyMember(character));
            }

            while (roster.Members.Count < CampRosterState.PartySize)
            {
                var empty = new CampMemberLoadout();
                CampRosterLoadoutRules.EnsureDeckStructure(empty);
                roster.Members.Add(empty);
            }

            return roster;
        }

        public static CampMemberLoadout CreateEmptyMember(CharacterDefinitionSO character)
        {
            var loadout = new CampMemberLoadout
            {
                CharacterDefinitionId = character?.CharacterId ?? "",
                DisplayName = character?.DisplayName ?? ""
            };
            CampRosterLoadoutRules.EnsureDeckStructure(loadout);
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

        public static Dictionary<string, string> BuildCardOwnerLookup(
            IReadOnlyDictionary<string, CardDefinitionSO> definitions)
        {
            var lookup = new Dictionary<string, string>();
            if (definitions == null)
                return lookup;

            foreach (var pair in definitions)
            {
                if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                    continue;

                lookup[pair.Key] = pair.Value.OwnerCharacterId ?? "";
            }

            return lookup;
        }
    }
}
