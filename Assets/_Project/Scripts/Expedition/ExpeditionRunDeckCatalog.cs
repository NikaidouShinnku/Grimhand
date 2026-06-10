using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class ExpeditionRunDeckCatalog
    {
        public sealed class DeckEntry
        {
            public string Key { get; set; } = "";
            public CardTemplate Template { get; set; }
            public bool IsBonus { get; set; }
            public int BonusIndex { get; set; } = -1;
        }

        public static List<CardTemplate> CollectMemberDeck(ExpeditionConfig config, PartyMemberSnapshot member)
        {
            var entries = CollectMemberDeckEntries(config, member);
            var cards = new List<CardTemplate>(entries.Count);
            foreach (var entry in entries)
                cards.Add(ExpeditionBattleConfigBuilder.CloneTemplate(entry.Template));

            cards.Sort(CompareTemplates);
            return cards;
        }

        public static List<DeckEntry> CollectMemberDeckEntries(ExpeditionConfig config, PartyMemberSnapshot member)
        {
            var entries = new List<DeckEntry>();
            if (config?.CombatEncounters == null || config.CombatEncounters.Count == 0
                || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return entries;

            var removedLeft = new Dictionary<string, int>();
            foreach (var pair in member.RemovedCardCounts)
                removedLeft[pair.Key] = pair.Value;

            var baseDecks = BuildBaseDeckLookup(config.CombatEncounters[0]);
            if (baseDecks.TryGetValue(member.CharacterDefinitionId, out var baseDeck))
            {
                foreach (var template in baseDeck)
                {
                    if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                        continue;

                    if (removedLeft.TryGetValue(template.DefinitionId, out var left) && left > 0)
                    {
                        removedLeft[template.DefinitionId] = left - 1;
                        continue;
                    }

                    var copy = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                    ApplyCardPowerBonus(copy, member);
                    if (string.IsNullOrEmpty(copy.OwnerCharacterId))
                        copy.OwnerCharacterId = member.CharacterDefinitionId;

                    entries.Add(new DeckEntry
                    {
                        Key = ExpeditionDeckCardKey.Build(member.CharacterDefinitionId, template.DefinitionId, entries.Count),
                        Template = copy,
                        IsBonus = false
                    });
                }
            }

            for (var i = 0; i < member.BonusCards.Count; i++)
            {
                var bonus = member.BonusCards[i];
                if (bonus == null || string.IsNullOrEmpty(bonus.DefinitionId))
                    continue;

                var copy = ExpeditionBattleConfigBuilder.CloneTemplate(bonus);
                ApplyCardPowerBonus(copy, member);
                entries.Add(new DeckEntry
                {
                    Key = ExpeditionDeckCardKey.Build(member.CharacterDefinitionId, bonus.DefinitionId, entries.Count),
                    Template = copy,
                    IsBonus = true,
                    BonusIndex = i
                });
            }

            return entries;
        }

        public static void ApplyCardPowerBonus(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null)
                return;

            if (!member.CardPowerBonusPercent.TryGetValue(template.DefinitionId, out var bonus) || bonus <= 0)
                return;

            foreach (var action in template.Actions)
            {
                if (action.ScaleWithAttack || action.Type == EffectActionType.DealDamage)
                {
                    var basePct = action.AttackScalePercent > 0 ? action.AttackScalePercent : 100;
                    action.AttackScalePercent = basePct + bonus;
                }

                if (action.ScaleWithDefense || action.Type == EffectActionType.GainBlock)
                {
                    var basePct = action.DefenseScalePercent > 0 ? action.DefenseScalePercent : 100;
                    action.DefenseScalePercent = basePct + bonus;
                }

                if (action.Value > 0 && !action.ScaleWithAttack && !action.ScaleWithDefense)
                    action.Value = action.Value * (100 + bonus) / 100;
            }
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
