using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>v0.8 祭坛升级：从共享经验池花费 XP（对照 Excel 升级表）。</summary>
    public static class ExpeditionAltarUpgradeRules
    {
        public const int HpPlus5Cost = 8;
        public const int HpPlus5CostIncrement = 2;
        public const int HpPlus10Cost = 15;
        public const int HpPlus10CostIncrement = 3;
        public const int DrawPlus1Cost = 35;
        public const int FullHealCost = 8;
        public const int SpeedPlus1FirstCost = 20;
        public const int SpeedPlus1SecondCost = 35;
        public const int HpPlus5Amount = 5;

        public const int RestHealGoldCost = 30;
        public const int RestHealXpCost = 20;
        public const int RestHealPercent = 25;

        /// <summary>能量上限 8→9→10，各档 XP（Excel 能量上限升级表）。</summary>
        public static readonly int[] EnergyCapUpgradeCosts = { 100, 200 };
        public const int MaxEnergyCapUpgrades = 2;

        /// <summary>手牌上限 +1×3 档 XP（Excel 手牌数升级表：50/100/200）。</summary>
        public static readonly int[] HandLimitUpgradeCosts = { 50, 100, 200 };
        public const int MaxHandLimitUpgrades = 3;

        public static bool TrySpendPool(ExpeditionRunState run, int cost)
        {
            if (run == null || cost <= 0 || run.SharedXpPool < cost)
                return false;

            run.SharedXpPool -= cost;
            return true;
        }

        public static int GetHpPlus5Cost(ExpeditionRunModifiers modifiers) =>
            HpPlus5Cost + (modifiers?.AltarHpPlus5Purchases ?? 0) * HpPlus5CostIncrement;

        public static int GetHpPlus10Cost(ExpeditionRunModifiers modifiers) =>
            HpPlus10Cost + (modifiers?.AltarHpPlus10Purchases ?? 0) * HpPlus10CostIncrement;

        public static int GetEnergyCapUpgradeCost(ExpeditionRunModifiers modifiers)
        {
            var tier = modifiers?.EnergyCapBonus ?? 0;
            return tier >= 0 && tier < EnergyCapUpgradeCosts.Length
                ? EnergyCapUpgradeCosts[tier]
                : 0;
        }

        public static int GetHandLimitUpgradeCost(ExpeditionRunModifiers modifiers)
        {
            var tier = modifiers?.HandLimitBonus ?? 0;
            return tier >= 0 && tier < HandLimitUpgradeCosts.Length
                ? HandLimitUpgradeCosts[tier]
                : 0;
        }

        public static int GetSpeedUpgradeCost(PartyMemberSnapshot member) =>
            member?.AltarSpeedUpgrades >= 1 ? SpeedPlus1SecondCost : SpeedPlus1FirstCost;

        public static int GetCardUpgradeCost(string displayName) =>
            CardUpgradeCatalog.GetXpCostPerLevel(displayName);

        public static int GetRemainingEnergyUpgrades(ExpeditionRunModifiers modifiers) =>
            System.Math.Max(0, MaxEnergyCapUpgrades - (modifiers?.EnergyCapBonus ?? 0));

        public static int GetRemainingHandLimitUpgrades(ExpeditionRunModifiers modifiers) =>
            System.Math.Max(0, MaxHandLimitUpgrades - (modifiers?.HandLimitBonus ?? 0));

        public static bool CanUpgradeEnergyCap(ExpeditionRunModifiers modifiers) =>
            GetEnergyCapUpgradeCost(modifiers) > 0;

        public static bool CanUpgradeHandLimit(ExpeditionRunModifiers modifiers) =>
            GetHandLimitUpgradeCost(modifiers) > 0;

        public static bool TryUpgradeMemberHp(ExpeditionRunState run, PartyMemberSnapshot member)
        {
            if (run == null || member == null)
                return false;

            var cost = GetHpPlus5Cost(run.Modifiers);
            if (!TrySpendPool(run, cost))
                return false;

            member.AltarMaxHpBonus += HpPlus5Amount;
            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
            run.Modifiers.AltarHpPlus5Purchases++;
            return true;
        }

        public static bool TryUpgradeEnergyCap(ExpeditionRunState run)
        {
            if (run == null || !CanUpgradeEnergyCap(run.Modifiers))
                return false;

            var cost = GetEnergyCapUpgradeCost(run.Modifiers);
            if (!TrySpendPool(run, cost))
                return false;

            run.Modifiers.EnergyCapBonus++;
            return true;
        }

        public static bool TryUpgradeHandLimit(ExpeditionRunState run)
        {
            if (run == null || !CanUpgradeHandLimit(run.Modifiers))
                return false;

            var cost = GetHandLimitUpgradeCost(run.Modifiers);
            if (!TrySpendPool(run, cost))
                return false;

            run.Modifiers.HandLimitBonus++;
            return true;
        }

        public static bool TryUpgradeMemberCard(
            ExpeditionRunState run,
            PartyMemberSnapshot member,
            string deckInstanceId,
            string displayName)
        {
            if (run == null || member == null || string.IsNullOrEmpty(deckInstanceId))
                return false;

            if (!CardUpgradeRules.CanUpgrade(member, deckInstanceId, displayName))
                return false;

            var cost = GetCardUpgradeCost(displayName);
            if (cost <= 0 || !TrySpendPool(run, cost))
                return false;

            return CardUpgradeRules.TryUpgradeLevel(member, deckInstanceId, displayName, 1);
        }

        public static int ComputeRestHealAmount(PartyMemberSnapshot member, ExpeditionRunState run)
        {
            if (member == null || run == null)
                return 0;

            var bonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(run.Party, run.Relics, run.RelicGrowthTiers);
            var max = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, bonus);
            return System.Math.Max(1, max * RestHealPercent / 100);
        }

        public static bool PartyHasRestHealableMember(ExpeditionRunState run)
        {
            if (run?.Party == null)
                return false;

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);

            var bonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(run.Party, run.Relics, run.RelicGrowthTiers);
            foreach (var member in run.Party)
            {
                if (member == null || member.Hp <= 0)
                    continue;

                var maxHp = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, bonus);
                if (member.Hp < maxHp)
                    return true;
            }

            return false;
        }

        public static bool CanRestHealWithGold(ExpeditionRunState run) =>
            run != null && run.Gold >= RestHealGoldCost && PartyHasRestHealableMember(run);

        public static bool CanRestHealWithXp(ExpeditionRunState run) =>
            run != null && run.SharedXpPool >= RestHealXpCost && PartyHasRestHealableMember(run);

        public static bool TryRestHealWithGold(ExpeditionRunState run)
        {
            if (!CanRestHealWithGold(run))
                return false;

            run.Gold -= RestHealGoldCost;
            ApplyRestHeal(run);
            return true;
        }

        public static bool TryRestHealWithXp(ExpeditionRunState run)
        {
            if (!CanRestHealWithXp(run))
                return false;

            if (!TrySpendPool(run, RestHealXpCost))
                return false;

            ApplyRestHeal(run);
            return true;
        }

        static void ApplyRestHeal(ExpeditionRunState run)
        {
            var bonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(run.Party, run.Relics, run.RelicGrowthTiers);
            foreach (var member in run.Party)
            {
                if (member == null || member.Hp <= 0)
                    continue;

                var maxHp = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, bonus);
                member.MaxHp = maxHp;
                ExpeditionPartyStatsRules.GetDisplayHp(
                    member, run.Party, run.Relics, run.RelicGrowthTiers, out var currentHp, out _);
                var heal = System.Math.Max(1, maxHp * RestHealPercent / 100);
                member.Hp = System.Math.Min(maxHp, currentHp + heal);
            }

            ExpeditionPartyStatsRules.SyncPartyEffectiveMaxHp(run.Party, run.Relics, run.RelicGrowthTiers);
            ClampPartyHpToEffectiveMax(run);
        }

        static void ClampPartyHpToEffectiveMax(ExpeditionRunState run)
        {
            var bonus = ExpeditionPartyStatsRules.GetPartyMaxHpBonus(run.Party, run.Relics, run.RelicGrowthTiers);
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

                var maxHp = ExpeditionPartyStatsRules.GetEffectiveMaxHp(member, bonus);
                member.MaxHp = maxHp;
                if (member.Hp > maxHp)
                    member.Hp = maxHp;
            }
        }
    }
}
