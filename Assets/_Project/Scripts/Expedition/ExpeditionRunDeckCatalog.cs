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

            if (member.UsesCampDeckAsBattleBase)
            {
                AppendCampDeckEntries(config, member, entries, removedLeft);
                AppendBonusDeckEntries(config, member, entries);
                return entries;
            }

            var baseDeck = FindCharacterBaseDeck(config, member.CharacterDefinitionId);
            if (baseDeck != null)
                AppendEncounterBaseDeckEntries(member, baseDeck, entries, removedLeft);

            // 遭遇模板缺该角色基组时，回退军营携带牌，避免祭坛/背包只剩一名角色的牌。
            if (entries.Count == 0 && member.CampDeckCardIds.Count > 0)
            {
                ExpeditionDeckInstanceRules.EnsureBaseDeckInstances(config, member);
                AppendCampDeckEntries(config, member, entries, removedLeft);
            }

            AppendBonusDeckEntries(config, member, entries);
            return entries;
        }

        static void AppendEncounterBaseDeckEntries(
            PartyMemberSnapshot member,
            IReadOnlyList<CardTemplate> baseDeck,
            List<DeckEntry> entries,
            Dictionary<string, int> removedLeft)
        {
            if (member == null || baseDeck == null || entries == null)
                return;

            for (var slot = 0; slot < baseDeck.Count; slot++)
            {
                var template = baseDeck[slot];
                if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                    continue;

                if (removedLeft != null
                    && removedLeft.TryGetValue(template.DefinitionId, out var left)
                    && left > 0)
                {
                    removedLeft[template.DefinitionId] = left - 1;
                    continue;
                }

                var instanceId = ExpeditionDeckInstanceRules.ResolveBaseDeckInstanceId(member, slot);
                if (string.IsNullOrEmpty(instanceId))
                    continue;

                var copy = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                copy.DeckInstanceId = instanceId;
                // 基组已按角色取出；纠偏归属，避免错误 Owner 导致整张被跳过。
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

        static List<CardTemplate> FindCharacterBaseDeck(ExpeditionConfig config, string characterId)
        {
            if (config?.CombatEncounters == null || string.IsNullOrEmpty(characterId))
                return null;

            foreach (var candidateId in EnumerateCharacterIdAliases(characterId))
            {
                foreach (var encounter in config.CombatEncounters)
                {
                    if (encounter?.Combatants == null)
                        continue;

                    foreach (var cc in encounter.Combatants)
                    {
                        if (cc == null
                            || cc.Team != TeamSide.Player
                            || cc.CharacterDefinitionId != candidateId
                            || cc.DeckTemplates == null
                            || cc.DeckTemplates.Count == 0)
                            continue;

                        return cc.DeckTemplates;
                    }
                }
            }

            return null;
        }

        /// <summary>角色 ID 别名（旧资源 / 演示配置偶发混用）。</summary>
        static string[] EnumerateCharacterIdAliases(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return System.Array.Empty<string>();

            return characterId switch
            {
                "char_warrior" => new[] { characterId, "char_knight" },
                "char_knight" => new[] { characterId, "char_warrior" },
                "char_pharaoh" => new[] { characterId, "char_mage" },
                "char_mage" => new[] { characterId, "char_pharaoh" },
                "char_demon" => new[] { characterId, "char_ranger" },
                "char_ranger" => new[] { characterId, "char_demon" },
                "char_viper_queen" => new[] { characterId, "char_snake_queen" },
                "char_snake_queen" => new[] { characterId, "char_viper_queen" },
                "char_lich" => new[] { characterId, "char_lich_queen" },
                "char_lich_queen" => new[] { characterId, "char_lich" },
                _ => new[] { characterId }
            };
        }

        public static int GetCharacterBaseDeckCount(ExpeditionConfig config, string characterId)
        {
            var deck = FindCharacterBaseDeck(config, characterId);
            return deck?.Count ?? 0;
        }

        static void AppendCampDeckEntries(
            ExpeditionConfig config,
            PartyMemberSnapshot member,
            List<DeckEntry> entries,
            Dictionary<string, int> removedLeft)
        {
            for (var slot = 0; slot < member.CampDeckCardIds.Count; slot++)
            {
                var cardId = member.CampDeckCardIds[slot];
                if (string.IsNullOrEmpty(cardId))
                    continue;

                if (!CampDeckOwnershipRules.IsCardOwnedByCharacter(config, cardId, member.CharacterDefinitionId))
                    continue;

                if (removedLeft.TryGetValue(cardId, out var left) && left > 0)
                {
                    removedLeft[cardId] = left - 1;
                    continue;
                }

                var instanceId = ExpeditionDeckInstanceRules.ResolveBaseDeckInstanceId(member, slot);
                if (string.IsNullOrEmpty(instanceId))
                    continue;

                var template = ResolveCampCollectionCard(config, cardId, member.CharacterDefinitionId);
                if (template == null)
                    continue;

                template.DeckInstanceId = instanceId;
                ApplyCardUpgrades(template, member);
                entries.Add(new DeckEntry
                {
                    Key = instanceId,
                    Template = template,
                    IsBonus = false
                });
            }
        }

        static void AppendBonusDeckEntries(
            ExpeditionConfig config,
            PartyMemberSnapshot member,
            List<DeckEntry> entries)
        {
            for (var i = 0; i < member.BonusCards.Count; i++)
            {
                var bonus = member.BonusCards[i];
                if (bonus == null || string.IsNullOrEmpty(bonus.DefinitionId))
                    continue;

                if (!CampDeckOwnershipRules.IsTemplateOwnedByMember(bonus, member))
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
            if (!string.IsNullOrEmpty(ownerId)
                && !string.IsNullOrEmpty(template.OwnerCharacterId)
                && template.OwnerCharacterId != ownerId)
                return null;

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
