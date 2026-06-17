using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class ExpeditionRunDeckMutations
    {
        public sealed class DeckCardEntry
        {
            public string Key { get; set; } = "";
            public string MemberId { get; set; } = "";
            public string MemberName { get; set; } = "";
            public CardTemplate Template { get; set; }
            public bool IsBonus { get; set; }
            public int BonusIndex { get; set; } = -1;
        }

        public static List<DeckCardEntry> ListSelectableCards(ExpeditionConfig config, ExpeditionRunState run)
        {
            var list = new List<DeckCardEntry>();
            if (config == null || run?.Party == null)
                return list;

            foreach (var member in run.Party)
            {
                foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
                {
                    list.Add(new DeckCardEntry
                    {
                        Key = entry.Key,
                        MemberId = member.CharacterDefinitionId,
                        MemberName = member.DisplayName,
                        Template = ExpeditionBattleConfigBuilder.CloneTemplate(entry.Template),
                        IsBonus = entry.IsBonus,
                        BonusIndex = entry.BonusIndex
                    });
                }
            }

            return list;
        }

        public static bool TryRemoveCard(ExpeditionRunState run, ExpeditionConfig config, DeckCardEntry entry)
        {
            return TryRemoveExactEntry(run, entry);
        }

        public static bool TryRemoveExactEntry(ExpeditionRunState run, DeckCardEntry entry)
        {
            if (run == null || entry == null)
                return false;

            var member = FindMember(run, entry.MemberId);
            if (member == null)
                return false;

            if (entry.IsBonus && entry.BonusIndex >= 0 && entry.BonusIndex < member.BonusCards.Count)
            {
                member.BonusCards.RemoveAt(entry.BonusIndex);
                return true;
            }

            var id = entry.Template?.DefinitionId;
            if (string.IsNullOrEmpty(id))
                return false;

            if (!member.RemovedCardCounts.ContainsKey(id))
                member.RemovedCardCounts[id] = 0;

            member.RemovedCardCounts[id]++;
            return true;
        }

        public static bool TryUpgradeCard(PartyMemberSnapshot member, string definitionId, int bonusPercent)
        {
            if (member == null || string.IsNullOrEmpty(definitionId))
                return false;

            if (!member.CardPowerBonusPercent.ContainsKey(definitionId))
                member.CardPowerBonusPercent[definitionId] = 0;

            member.CardPowerBonusPercent[definitionId] += bonusPercent;
            return true;
        }

        public static bool TryFuseCards(
            ExpeditionConfig config,
            ExpeditionRunState run,
            DeckCardEntry first,
            DeckCardEntry second,
            BattleRng rng,
            out CardTemplate result,
            out PartyMemberSnapshot owner)
        {
            result = null;
            owner = null;
            if (first?.Template == null || second?.Template == null)
                return false;

            if (first.Template.CardType != second.Template.CardType)
                return false;

            if (first.Key == second.Key)
                return false;

            if (run?.Party == null || run.Party.Count == 0)
                return false;

            TryRemoveExactEntry(run, first);
            TryRemoveExactEntry(run, second);

            var rarityA = CardRarityTable.GetOrDefault(first.Template.DefinitionId);
            var rarityB = CardRarityTable.GetOrDefault(second.Template.DefinitionId);
            var current = rarityA >= rarityB ? rarityA : rarityB;
            var next = CardRarityRules.UpgradeRarity(current);

            owner = run.Party[rng.NextIndex(run.Party.Count)];
            if (!ExpeditionCardPool.TryRollCardRewardForMember(config, owner, next, rng, out result))
            {
                result = ExpeditionBattleConfigBuilder.CloneTemplate(first.Template);
                result.OwnerCharacterId = owner.CharacterDefinitionId;
            }

            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(result, config?.PlayerCardCatalog);
            result.OwnerCharacterId = owner.CharacterDefinitionId;
            return true;
        }

        static PartyMemberSnapshot FindMember(ExpeditionRunState run, string memberId)
        {
            foreach (var m in run.Party)
            {
                if (m.CharacterDefinitionId == memberId)
                    return m;
            }

            return null;
        }
    }
}
