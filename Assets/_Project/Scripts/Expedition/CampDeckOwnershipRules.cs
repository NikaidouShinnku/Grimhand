using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>军营/远征卡组：卡牌必须属于对应角色。</summary>
    public static class CampDeckOwnershipRules
    {
        public static bool IsCardOwnedByCharacter(
            ExpeditionConfig config,
            string cardDefinitionId,
            string characterDefinitionId)
        {
            if (string.IsNullOrEmpty(cardDefinitionId) || string.IsNullOrEmpty(characterDefinitionId))
                return false;

            var owner = ResolveOwnerCharacterId(config, cardDefinitionId);
            if (string.IsNullOrEmpty(owner))
                return true;

            return owner == characterDefinitionId;
        }

        public static string ResolveOwnerCharacterId(ExpeditionConfig config, string cardDefinitionId)
        {
            if (config == null || string.IsNullOrEmpty(cardDefinitionId))
                return "";

            foreach (var template in config.PlayerCardCatalog)
            {
                if (template?.DefinitionId == cardDefinitionId)
                    return template.OwnerCharacterId ?? "";
            }

            return "";
        }

        public static void SanitizeMemberCampDeck(ExpeditionConfig config, PartyMemberSnapshot member)
        {
            if (config == null || member?.CampDeckCardIds == null
                || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return;

            for (var i = 0; i < member.CampDeckCardIds.Count; i++)
            {
                var cardId = member.CampDeckCardIds[i];
                if (string.IsNullOrEmpty(cardId))
                    continue;

                if (!IsCardOwnedByCharacter(config, cardId, member.CharacterDefinitionId))
                    member.CampDeckCardIds[i] = "";
            }
        }

        public static void SanitizeParty(ExpeditionConfig config, IList<PartyMemberSnapshot> party)
        {
            if (config == null || party == null)
                return;

            foreach (var member in party)
            {
                if (member == null)
                    continue;

                SanitizeMemberCampDeck(config, member);
                PruneForeignBonusCards(member);
            }
        }

        public static void SyncRunStartCampDecks(ExpeditionRunState run)
        {
            if (run?.Party == null)
                return;

            run.RunStartCampDecks.Clear();
            foreach (var member in run.Party)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                run.RunStartCampDecks[member.CharacterDefinitionId] =
                    new List<string>(member.CampDeckCardIds);
            }
        }

        static void PruneForeignBonusCards(PartyMemberSnapshot member)
        {
            if (member?.BonusCards == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return;

            for (var i = member.BonusCards.Count - 1; i >= 0; i--)
            {
                var bonus = member.BonusCards[i];
                if (bonus == null)
                {
                    member.BonusCards.RemoveAt(i);
                    continue;
                }

                if (!string.IsNullOrEmpty(bonus.OwnerCharacterId)
                    && bonus.OwnerCharacterId != member.CharacterDefinitionId)
                {
                    member.BonusCards.RemoveAt(i);
                }
            }
        }

        public static bool IsTemplateOwnedByMember(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                return false;

            return string.IsNullOrEmpty(template.OwnerCharacterId)
                   || template.OwnerCharacterId == member.CharacterDefinitionId;
        }
    }
}
