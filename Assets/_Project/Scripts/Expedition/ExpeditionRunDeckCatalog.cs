using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征非战斗界面：按角色顺序汇总牌组（基础牌 + 奖励牌）。</summary>
    public static class ExpeditionRunDeckCatalog
    {
        public static List<CardTemplate> CollectMemberDeck(ExpeditionConfig config, PartyMemberSnapshot member)
        {
            var cards = new List<CardTemplate>();
            if (config?.CombatEncounters == null || config.CombatEncounters.Count == 0
                || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return cards;

            var baseDecks = BuildBaseDeckLookup(config.CombatEncounters[0]);
            if (baseDecks.TryGetValue(member.CharacterDefinitionId, out var baseDeck))
            {
                foreach (var template in baseDeck)
                {
                    if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                        continue;

                    var copy = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                    if (string.IsNullOrEmpty(copy.OwnerCharacterId))
                        copy.OwnerCharacterId = member.CharacterDefinitionId;
                    cards.Add(copy);
                }
            }

            foreach (var bonus in member.BonusCards)
            {
                if (bonus == null || string.IsNullOrEmpty(bonus.DefinitionId))
                    continue;

                cards.Add(ExpeditionBattleConfigBuilder.CloneTemplate(bonus));
            }

            cards.Sort(CompareTemplates);
            return cards;
        }

        public static List<CardTemplate> CollectSortedDeck(ExpeditionConfig config, IReadOnlyList<PartyMemberSnapshot> party)
        {
            var result = new List<CardTemplate>();
            if (config?.CombatEncounters == null || config.CombatEncounters.Count == 0 || party == null)
                return result;

            foreach (var member in party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                result.AddRange(CollectMemberDeck(config, member));
            }

            return result;
        }

        static int CompareTemplates(CardTemplate a, CardTemplate b)
        {
            var idCmp = string.CompareOrdinal(a.DefinitionId, b.DefinitionId);
            if (idCmp != 0)
                return idCmp;

            return string.CompareOrdinal(a.DisplayName, b.DisplayName);
        }

        static Dictionary<string, List<CardTemplate>> BuildBaseDeckLookup(BattleConfig encounter)
        {
            var lookup = new Dictionary<string, List<CardTemplate>>();
            if (encounter?.Combatants == null)
                return lookup;

            foreach (var cc in encounter.Combatants)
            {
                if (cc.Team != TeamSide.Player || string.IsNullOrEmpty(cc.CharacterDefinitionId))
                    continue;

                if (!lookup.ContainsKey(cc.CharacterDefinitionId))
                    lookup[cc.CharacterDefinitionId] = cc.DeckTemplates;
            }

            return lookup;
        }
    }
}
