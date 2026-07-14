using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>遗物每 20 层成长一次（对照遗物表「后续成长」列）。</summary>
    public static class RelicGrowthRules
    {
        public const int FloorsPerGrowthTier = 20;

        public static int GrowthTiersForFloor(int floor) =>
            floor > 0 ? floor / FloorsPerGrowthTier : 0;

        public static void OnRelicAcquired(IDictionary<string, int> growthTiers, string relicId, int floor)
        {
            if (growthTiers == null || string.IsNullOrEmpty(relicId))
                return;

            growthTiers[relicId] = GrowthTiersForFloor(floor);
        }

        public static void TransferGrowthTiers(
            IDictionary<string, int> growthTiers,
            string fromRelicId,
            string toRelicId)
        {
            if (growthTiers == null || string.IsNullOrEmpty(fromRelicId) || string.IsNullOrEmpty(toRelicId))
                return;

            if (!growthTiers.TryGetValue(fromRelicId, out var tiers))
                return;

            growthTiers.Remove(fromRelicId);
            growthTiers[toRelicId] = tiers;
        }

        /// <summary>事件进化：立即用目标遗物替换源遗物（不依赖奖励领取 UI）。</summary>
        public static bool TryEvolveRelic(ExpeditionRunState run, string fromRelicId, string toRelicId)
        {
            if (run?.Relics == null
                || string.IsNullOrEmpty(fromRelicId)
                || string.IsNullOrEmpty(toRelicId)
                || !RelicDatabase.TryGet(toRelicId, out _))
                return false;

            if (!run.Relics.Contains(fromRelicId))
                return false;

            run.Relics.Remove(fromRelicId);
            TransferGrowthTiers(run.RelicGrowthTiers, fromRelicId, toRelicId);

            if (!run.Relics.Contains(toRelicId))
            {
                run.Relics.Add(toRelicId);
                var floor = System.Math.Max(1, run.Map?.NodesCompleted ?? 0);
                OnRelicAcquired(run.RelicGrowthTiers, toRelicId, floor);
            }

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
            return true;
        }

        public static void SyncFloorGrowth(IDictionary<string, int> growthTiers, IReadOnlyList<string> relicIds, int floor)
        {
            if (growthTiers == null || relicIds == null)
                return;

            var expected = GrowthTiersForFloor(floor);
            foreach (var relicId in relicIds)
            {
                if (string.IsNullOrEmpty(relicId))
                    continue;

                if (!growthTiers.TryGetValue(relicId, out var current))
                    current = 0;

                if (expected > current)
                    growthTiers[relicId] = expected;
            }
        }

        public static int GetGrowthTiers(IReadOnlyDictionary<string, int> growthTiers, string relicId)
        {
            if (growthTiers == null || string.IsNullOrEmpty(relicId))
                return 0;

            return growthTiers.TryGetValue(relicId, out var tiers) ? tiers : 0;
        }

        public static void ApplyGrowthBonuses(string relicId, int tiers, RunModifierSnapshot mods)
        {
            if (tiers <= 0 || mods == null || string.IsNullOrEmpty(relicId))
                return;

            switch (relicId)
            {
                case RelicIds.SunPyramid:
                    mods.StatusCardTeamBlock += 5 * tiers;
                    break;
                case RelicIds.KnightInCastle:
                    mods.WarriorFirstHitBlockAmount += 8 * tiers;
                    break;
                case RelicIds.BloodAlter:
                    mods.SacrificeHpCostReductionPercent += 5f * tiers;
                    break;
                case RelicIds.JadeStone:
                    mods.TurnStartRandomAllyBlock += 2 * tiers;
                    break;
                case RelicIds.JadeRing:
                    mods.TurnStartTeamBlock += 3 * tiers;
                    break;
                case RelicIds.JadeDagger:
                    mods.TeamAttackBonusPercent += 2f * tiers;
                    break;
                case RelicIds.CrimsonBurningBoots:
                    mods.TurnStartEnemyBurnStacks += 1 * tiers;
                    break;
                case RelicIds.FlameSword:
                    mods.TeamAttackBonusPercent += 2f * tiers;
                    mods.AttackBurnStacks += 5 * tiers;
                    break;
                case RelicIds.IronArmor:
                    mods.TeamBlockGainBonusPercent += 2f * tiers;
                    mods.BattleStartFrontBlock += 10 * tiers;
                    break;
                case RelicIds.WarriorHelmet:
                    mods.TeamHpBonus += 8 * tiers;
                    mods.RevengeAttackFlatBonus += 4 * tiers;
                    break;
                case RelicIds.DragonRing:
                    mods.TeamAttackBonus += 3 * tiers;
                    break;
                case RelicIds.PaladinShield:
                    mods.FirstHitDamageReductionPercent += 5f * tiers;
                    break;
                case RelicIds.SilverMoonPendant:
                    mods.EndTurnTeamHeal += 2 * tiers;
                    break;
                case RelicIds.TaichiRing:
                    mods.FirstAttackFlatBonus += 5 * tiers;
                    mods.FirstDefenseFlatBonus += 5 * tiers;
                    mods.AttackAndDefenseSameTurnHeal += 5 * tiers;
                    break;
                case RelicIds.LeafOfMiracle:
                    mods.MiracleLeafReviveHpPercent += 10 * tiers;
                    break;
                case RelicIds.Bonfire:
                    mods.PostBattleTeamHealPercent += 1f * tiers;
                    break;
            }
        }
    }
}
