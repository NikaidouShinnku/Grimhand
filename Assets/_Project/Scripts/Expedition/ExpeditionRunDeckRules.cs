using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public enum CardGrantResult
    {
        Failed,
        Added,
        PendingReplace
    }

    public static class ExpeditionRunDeckRules
    {
        public static int DeckSize => CampRosterState.DeckSize;

        public static int CountMemberDeck(ExpeditionConfig config, PartyMemberSnapshot member) =>
            ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member).Count;

        public static bool CanAddWithoutReplace(ExpeditionConfig config, PartyMemberSnapshot member) =>
            CountMemberDeck(config, member) < DeckSize;

        public static bool NeedsReplace(ExpeditionConfig config, PartyMemberSnapshot member) =>
            !CanAddWithoutReplace(config, member);

        public static List<int> GetAvailableCollectionIndices(ExpeditionRunState run, PartyMemberSnapshot member)
        {
            var list = new List<int>();
            var ids = GetCampCollectionCardIds(run, member);
            for (var i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                    continue;

                if (CampCollectionProgress.IsExtracted(run, member?.CharacterDefinitionId, i))
                    continue;

                list.Add(i);
            }

            return list;
        }

        public static IReadOnlyList<string> GetCampCollectionCardIds(ExpeditionRunState run, PartyMemberSnapshot member)
        {
            if (member != null
                && !string.IsNullOrEmpty(member.CharacterDefinitionId)
                && run?.RunStartCampDecks != null
                && run.RunStartCampDecks.TryGetValue(member.CharacterDefinitionId, out var snapshot)
                && snapshot != null
                && snapshot.Count > 0)
                return snapshot;

            return member?.CampDeckCardIds;
        }

        public static bool TryFindMemberDeckEntryByKey(
            ExpeditionConfig config,
            PartyMemberSnapshot member,
            string key,
            out ExpeditionRunDeckMutations.DeckCardEntry entry)
        {
            entry = null;
            if (member == null || string.IsNullOrEmpty(key))
                return false;

            foreach (var deckEntry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
            {
                if (deckEntry.Key != key)
                    continue;

                entry = new ExpeditionRunDeckMutations.DeckCardEntry
                {
                    Key = deckEntry.Key,
                    MemberId = member.CharacterDefinitionId,
                    MemberName = member.DisplayName,
                    Template = ExpeditionBattleConfigBuilder.CloneTemplate(deckEntry.Template),
                    IsBonus = deckEntry.IsBonus,
                    BonusIndex = deckEntry.BonusIndex
                };
                return true;
            }

            return false;
        }

        public static bool TryReplaceAndAdd(
            ExpeditionRunState run,
            PartyMemberSnapshot member,
            ExpeditionRunDeckMutations.DeckCardEntry removeEntry,
            CardTemplate addTemplate)
        {
            if (run == null || member == null || removeEntry == null || addTemplate == null)
                return false;

            if (!ExpeditionRunDeckMutations.TryRemoveExactEntry(run, removeEntry))
                return false;

            member.BonusCards.Add(ExpeditionBattleConfigBuilder.CloneTemplate(addTemplate));
            return true;
        }

        public static CardGrantResult TryOfferCard(
            ExpeditionConfig config,
            ExpeditionRunState run,
            PartyMemberSnapshot member,
            CardTemplate template,
            ExpeditionCardOfferContext context,
            Action<string> recordAcquisition = null)
        {
            if (config == null || run == null || member == null || template == null)
                return CardGrantResult.Failed;

            var clone = ExpeditionBattleConfigBuilder.CloneTemplate(template);
            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(clone, config.PlayerCardCatalog);
            if (string.IsNullOrEmpty(clone.OwnerCharacterId))
                clone.OwnerCharacterId = member.CharacterDefinitionId;

            if (CanAddWithoutReplace(config, member))
            {
                member.BonusCards.Add(clone);
                recordAcquisition?.Invoke($"获得卡牌：{clone.DisplayName}（{member.DisplayName}）");
                return CardGrantResult.Added;
            }

            run.PendingCardOffer = new ExpeditionPendingCardOffer
            {
                OwnerCharacterId = member.CharacterDefinitionId,
                Template = clone,
                Context = context
            };
            return CardGrantResult.PendingReplace;
        }
    }
}
