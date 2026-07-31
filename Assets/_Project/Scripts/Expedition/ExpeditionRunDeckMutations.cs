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
                var removed = member.BonusCards[entry.BonusIndex];
                ExpeditionDeckInstanceRules.ClearUpgradeForInstance(member, removed?.DeckInstanceId);
                member.BonusCards.RemoveAt(entry.BonusIndex);
                return true;
            }

            if (entry.IsBonus)
                return false;

            var id = entry.Template?.DefinitionId;
            if (string.IsNullOrEmpty(id))
                return false;

            ExpeditionDeckInstanceRules.ClearUpgradeForInstance(member, entry.Template?.DeckInstanceId);

            if (!member.RemovedCardCounts.ContainsKey(id))
                member.RemovedCardCounts[id] = 0;

            member.RemovedCardCounts[id]++;
            return true;
        }

        public static bool TryUpgradeCard(PartyMemberSnapshot member, DeckCardEntry entry, int levels = 1)
        {
            if (entry?.Template == null)
                return false;

            var instanceId = entry.Template.DeckInstanceId;
            if (string.IsNullOrEmpty(instanceId))
                instanceId = entry.Key;

            return CardUpgradeRules.TryUpgradeLevel(
                member,
                instanceId,
                entry.Template.DisplayName,
                levels);
        }

        public static bool TryUpgradeCard(
            PartyMemberSnapshot member,
            ExpeditionRunDeckCatalog.DeckEntry entry,
            int levels = 1)
        {
            if (entry?.Template == null)
                return false;

            var instanceId = entry.Template.DeckInstanceId;
            if (string.IsNullOrEmpty(instanceId))
                instanceId = entry.Key;

            return CardUpgradeRules.TryUpgradeLevel(
                member,
                instanceId,
                entry.Template.DisplayName,
                levels);
        }

        public static bool TryUpgradeCard(
            PartyMemberSnapshot member,
            string deckInstanceId,
            string displayName,
            int levels = 1) =>
            CardUpgradeRules.TryUpgradeLevel(member, deckInstanceId, displayName, levels);

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

            if (first.Key == second.Key)
                return false;

            if (run?.Party == null || run.Party.Count == 0)
                return false;

            var rarityA = CardRarityTable.GetOrDefault(first.Template.DefinitionId);
            var rarityB = CardRarityTable.GetOrDefault(second.Template.DefinitionId);
            if (rarityA != rarityB)
                return false;

            foreach (var entry in OrderForRemoval(first, second))
            {
                if (!TryRemoveExactEntry(run, entry))
                    return false;
            }

            var next = CardRarityRules.UpgradeRarity(rarityA);
            owner = PickFusionOwner(run, first, second, rng);
            if (owner == null)
                return false;

            if (!ExpeditionCardPool.TryRollCardRewardForMember(config, owner, next, rng, out result))
            {
                // 换另一名参与融合的角色再试
                var alt = FindMember(run, first.MemberId == owner.CharacterDefinitionId
                    ? second.MemberId
                    : first.MemberId);
                if (alt != null
                    && alt.CharacterDefinitionId != owner.CharacterDefinitionId
                    && ExpeditionCardPool.TryRollCardRewardForMember(config, alt, next, rng, out result))
                {
                    owner = alt;
                }
                else
                {
                    result = ExpeditionBattleConfigBuilder.CloneTemplate(first.Template);
                    owner = ResolveFusionFallbackOwner(run, first, second, result, rng);
                }
            }

            ExpeditionBattleConfigBuilder.HydrateTemplateFromCatalog(result, config?.PlayerCardCatalog);
            result.OwnerCharacterId = owner.CharacterDefinitionId;
            ExpeditionDeckInstanceRules.PrepareNewDeckCard(owner, result);
            return true;
        }

        static PartyMemberSnapshot PickFusionOwner(
            ExpeditionRunState run,
            DeckCardEntry first,
            DeckCardEntry second,
            BattleRng rng)
        {
            var candidates = new List<PartyMemberSnapshot>();
            var a = FindMember(run, first.MemberId);
            var b = FindMember(run, second.MemberId);
            if (a != null)
                candidates.Add(a);
            if (b != null && (a == null || b.CharacterDefinitionId != a.CharacterDefinitionId))
                candidates.Add(b);

            if (candidates.Count == 0)
                return null;

            return candidates[rng.NextIndex(candidates.Count)];
        }

        static List<DeckCardEntry> OrderForRemoval(DeckCardEntry first, DeckCardEntry second)
        {
            if (first.MemberId == second.MemberId && first.IsBonus && second.IsBonus)
            {
                if (first.BonusIndex >= second.BonusIndex)
                    return new List<DeckCardEntry> { first, second };

                return new List<DeckCardEntry> { second, first };
            }

            return new List<DeckCardEntry> { first, second };
        }

        static PartyMemberSnapshot ResolveFusionFallbackOwner(
            ExpeditionRunState run,
            DeckCardEntry first,
            DeckCardEntry second,
            CardTemplate result,
            BattleRng rng)
        {
            var preferred = new List<PartyMemberSnapshot>();
            var a = FindMember(run, first.MemberId);
            var b = FindMember(run, second.MemberId);
            if (a != null)
                preferred.Add(a);
            if (b != null && (a == null || b.CharacterDefinitionId != a.CharacterDefinitionId))
                preferred.Add(b);

            foreach (var member in preferred)
            {
                if (ExpeditionCardPool.IsCardOwnedByCharacter(result, member.CharacterDefinitionId))
                    return member;
            }

            // 融合结果必须归属所选角色之一
            if (preferred.Count > 0)
                return preferred[rng.NextIndex(preferred.Count)];

            return run.Party.Count > 0 ? run.Party[rng.NextIndex(run.Party.Count)] : null;
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
