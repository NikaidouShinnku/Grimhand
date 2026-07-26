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

            var targetCount = ResolveBaseDeckSlotCount(config, member);
            if (targetCount <= 0)
                return;

            while (member.BaseDeckInstanceIds.Count < targetCount)
            {
                member.BaseDeckInstanceIds.Add(
                    ExpeditionDeckCardKey.GenerateInstanceId(member.CharacterDefinitionId));
            }
        }

        static int ResolveBaseDeckSlotCount(ExpeditionConfig config, PartyMemberSnapshot member)
        {
            if (member.UsesCampDeckAsBattleBase)
                return CountCampDeckSlots(member);

            var encounterCount = ExpeditionRunDeckCatalog.GetCharacterBaseDeckCount(
                config, member.CharacterDefinitionId);
            if (encounterCount > 0)
                return encounterCount;

            // 遭遇模板没有该角色基组时，按军营携带牌槽位数预留实例 id。
            return CountCampDeckSlots(member);
        }

        static int CountCampDeckSlots(PartyMemberSnapshot member)
        {
            if (member?.CampDeckCardIds == null)
                return 0;

            var filled = 0;
            foreach (var cardId in member.CampDeckCardIds)
            {
                if (!string.IsNullOrEmpty(cardId))
                    filled++;
            }

            return System.Math.Max(filled, member.CampDeckCardIds.Count);
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
    }
}
