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

            ExpeditionDeckInstanceRules.EnsureBaseDeckInstances(config, member);

            var removedLeft = new Dictionary<string, int>();
            foreach (var pair in member.RemovedCardCounts)
                removedLeft[pair.Key] = pair.Value;

            var baseDecks = BuildBaseDeckLookup(config.CombatEncounters[0]);
            if (baseDecks.TryGetValue(member.CharacterDefinitionId, out var baseDeck))
            {
                for (var slot = 0; slot < baseDeck.Count; slot++)
                {
                    var template = baseDeck[slot];
                    if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                        continue;

                    if (removedLeft.TryGetValue(template.DefinitionId, out var left) && left > 0)
                    {
                        removedLeft[template.DefinitionId] = left - 1;
                        continue;
                    }

                    var instanceId = ExpeditionDeckInstanceRules.ResolveBaseDeckInstanceId(member, slot);
                    if (string.IsNullOrEmpty(instanceId))
                        continue;

                    var copy = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                    copy.DeckInstanceId = instanceId;
                    if (string.IsNullOrEmpty(copy.OwnerCharacterId))
                        copy.OwnerCharacterId = member.CharacterDefinitionId;

                    ApplyCardUpgrades(copy, member);
                    entries.Add(new DeckEntry
                    {
                        Key = instanceId,
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

                ExpeditionDeckInstanceRules.PrepareNewDeckCard(member, bonus);
                var copy = ExpeditionBattleConfigBuilder.CloneTemplate(bonus);
                ApplyCardUpgrades(copy, member);
                entries.Add(new DeckEntry
                {
                    Key = copy.DeckInstanceId,
                    Template = copy,
                    IsBonus = true,
                    BonusIndex = i
                });
            }

            return entries;
        }

        public static CardTemplate TryResolveCampCollectionCard(
            ExpeditionConfig config,
            ExpeditionRunState run,
            PartyMemberSnapshot member,
            int collectionIndex)
        {
            var cardId = GetCampCollectionCardId(run, member, collectionIndex);
            if (string.IsNullOrEmpty(cardId))
                return null;

            return ResolveCampCollectionCard(config, cardId, member?.CharacterDefinitionId ?? "");
        }

        public static string GetCampCollectionCardId(
            ExpeditionRunState run,
            PartyMemberSnapshot member,
            int collectionIndex)
        {
            var ids = ExpeditionRunDeckRules.GetCampCollectionCardIds(run, member);
            if (ids == null || collectionIndex < 0 || collectionIndex >= ids.Count)
                return null;

            return ids[collectionIndex];
        }

        public static CardTemplate ResolveCampCollectionCard(
            ExpeditionConfig config,
            string cardId,
            string ownerId)
        {
            if (string.IsNullOrEmpty(cardId))
                return null;

            var template = ResolveCardTemplate(config, cardId, ownerId);
            if (template == null)
            {
                template = new CardTemplate
                {
                    DefinitionId = cardId,
                    DisplayName = cardId,
                    OwnerCharacterId = ownerId ?? ""
                };
            }
            else
            {
                template = ExpeditionBattleConfigBuilder.CloneTemplate(template);
            }

            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(template, config?.PlayerCardCatalog);
            if (string.IsNullOrEmpty(template.OwnerCharacterId) && !string.IsNullOrEmpty(ownerId))
                template.OwnerCharacterId = ownerId;

            return template;
        }

        static CardTemplate ResolveCardTemplate(ExpeditionConfig config, string cardId, string ownerId)
        {
            if (config == null || string.IsNullOrEmpty(cardId))
                return null;

            foreach (var template in config.PlayerCardCatalog)
            {
                if (template?.DefinitionId != cardId)
                    continue;

                var copy = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                if (string.IsNullOrEmpty(copy.OwnerCharacterId))
                    copy.OwnerCharacterId = ownerId;
                return copy;
            }

            if (TryFindInEncounterDecks(config.CombatEncounters, cardId, ownerId, out var fromCombat))
                return fromCombat;

            if (TryFindInEncounterDecks(config.BossEncounters, cardId, ownerId, out var fromBoss))
                return fromBoss;

            return null;
        }

        static bool TryFindInEncounterDecks(
            IReadOnlyList<BattleConfig> encounters,
            string cardId,
            string ownerId,
            out CardTemplate result)
        {
            result = null;
            if (encounters == null)
                return false;

            foreach (var encounter in encounters)
            {
                var lookup = BuildBaseDeckLookup(encounter);
                if (!string.IsNullOrEmpty(ownerId)
                    && lookup.TryGetValue(ownerId, out var ownerDeck))
                {
                    foreach (var template in ownerDeck)
                    {
                        if (template?.DefinitionId != cardId)
                            continue;

                        result = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                        return true;
                    }
                }

                foreach (var deck in lookup.Values)
                {
                    foreach (var template in deck)
                    {
                        if (template?.DefinitionId != cardId)
                            continue;

                        result = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                        return true;
                    }
                }
            }

            return false;
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

        public static void ApplyCardUpgrades(CardTemplate template, PartyMemberSnapshot member) =>
            CardUpgradeRules.ApplyToTemplate(template, member);

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
    }
}
