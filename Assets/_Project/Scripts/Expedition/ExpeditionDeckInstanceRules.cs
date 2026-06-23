using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征卡组内每张牌的稳定实例 id（升级、选牌 UI 均按实例而非牌名）。</summary>
    public static class ExpeditionDeckInstanceRules
    {
        public static void EnsurePartyBaseDeckInstances(ExpeditionConfig config, IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (config == null || party == null)
                return;

            foreach (var member in party)
                EnsureBaseDeckInstances(config, member);
        }

        public static void EnsureBaseDeckInstances(ExpeditionConfig config, PartyMemberSnapshot member)
        {
            if (config?.CombatEncounters == null || config.CombatEncounters.Count == 0
                || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return;

            var baseDecks = BuildBaseDeckLookup(config.CombatEncounters[0]);
            if (!baseDecks.TryGetValue(member.CharacterDefinitionId, out var baseDeck) || baseDeck == null)
                return;

            while (member.BaseDeckInstanceIds.Count < baseDeck.Count)
            {
                member.BaseDeckInstanceIds.Add(
                    ExpeditionDeckCardKey.GenerateInstanceId(member.CharacterDefinitionId));
            }
        }

        public static string ResolveBaseDeckInstanceId(PartyMemberSnapshot member, int slotIndex)
        {
            if (member == null || slotIndex < 0 || slotIndex >= member.BaseDeckInstanceIds.Count)
                return "";

            return member.BaseDeckInstanceIds[slotIndex];
        }

        public static void PrepareNewDeckCard(PartyMemberSnapshot member, CardTemplate template)
        {
            if (member == null || template == null)
                return;

            if (string.IsNullOrEmpty(template.OwnerCharacterId))
                template.OwnerCharacterId = member.CharacterDefinitionId;

            if (string.IsNullOrEmpty(template.DeckInstanceId))
                template.DeckInstanceId = ExpeditionDeckCardKey.GenerateInstanceId(member.CharacterDefinitionId);
        }

        public static void ClearUpgradeForInstance(PartyMemberSnapshot member, string deckInstanceId)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId))
                return;

            member.CardUpgradeLevels.Remove(deckInstanceId);
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
