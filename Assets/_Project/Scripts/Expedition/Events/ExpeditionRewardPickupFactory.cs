using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public static class ExpeditionRewardPickupFactory
    {
        public static string RollRelicId(ExpeditionRunState run, BattleRng rng)
        {
            var pool = new List<string>();
            foreach (var relic in RelicDatabase.All)
            {
                if (run.Relics.Contains(relic.Id))
                    continue;

                if (!RelicDatabase.CanAppearInRewardPool(relic, run.Party))
                    continue;

                pool.Add(relic.Id);
            }

            if (pool.Count == 0)
                return "";

            return pool[rng.NextIndex(pool.Count)];
        }

        public static ExpeditionRewardPickup Relic(string relicId, string header, RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (string.IsNullOrEmpty(relicId))
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                RelicId = relicId
            };
        }

        public static ExpeditionRewardPickup Gold(
            int amount,
            string header,
            bool enableDivinePunishment = false,
            RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (amount <= 0 && !enableDivinePunishment)
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                Gold = amount,
                EnableDivinePunishment = enableDivinePunishment
            };
        }

        public static ExpeditionRewardPickup Consumable(
            string consumableId,
            int count,
            string header,
            RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (string.IsNullOrEmpty(consumableId) || count <= 0)
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                ConsumableId = consumableId,
                ConsumableCount = count
            };
        }

        public static ExpeditionRewardPickup RelicEvolution(
            string fromRelicId,
            string toRelicId,
            string header,
            RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (string.IsNullOrEmpty(fromRelicId) || string.IsNullOrEmpty(toRelicId))
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                RelicEvolveFromId = fromRelicId,
                RelicEvolveToId = toRelicId,
                RelicId = toRelicId
            };
        }

        public static ExpeditionRewardPickup Card(
            CardTemplate template,
            PartyMemberSnapshot owner,
            string header,
            RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (template == null || owner == null)
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                CardDefinitionId = template.DefinitionId,
                CardOwnerCharacterId = string.IsNullOrEmpty(template.OwnerCharacterId)
                    ? owner.CharacterDefinitionId
                    : template.OwnerCharacterId,
                CardDisplayName = template.DisplayName
            };
        }

        public static ExpeditionRewardPickup TeamStats(
            string header,
            int teamAttack = 0,
            int teamDefense = 0,
            int energyCap = 0,
            int grantXp = 0,
            bool enableSoulRiftBattleStartRandomHpLoss = false,
            bool enableDivinePunishment = false,
            RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (teamAttack == 0 && teamDefense == 0 && energyCap == 0 && grantXp == 0
                && !enableSoulRiftBattleStartRandomHpLoss && !enableDivinePunishment)
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                TeamAttackBonus = teamAttack,
                TeamDefenseBonus = teamDefense,
                EnergyCapBonus = energyCap,
                GrantXp = grantXp,
                EnableSoulRiftBattleStartRandomHpLoss = enableSoulRiftBattleStartRandomHpLoss,
                EnableDivinePunishment = enableDivinePunishment
            };
        }

        public static ExpeditionRewardPickup MemberPersonalAttack(
            string header,
            int personalAttack,
            bool resolveCharacterFromInteraction = true,
            RewardPickupKind kind = RewardPickupKind.EventOrShrine)
        {
            if (personalAttack == 0)
                return null;

            return new ExpeditionRewardPickup
            {
                HeaderText = header,
                Kind = kind,
                PersonalAttackBonus = personalAttack,
                ResolveStatCharacterFromInteraction = resolveCharacterFromInteraction
            };
        }
    }
}
