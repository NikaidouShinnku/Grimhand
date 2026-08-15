using System;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>卡牌升级：按 Excel「可升级次数 / 每次升级效果」逐级调整数值；每张牌实例独立计费。</summary>
    public static class CardUpgradeRules
    {
        public static int GetLevel(PartyMemberSnapshot member, string deckInstanceId)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId))
                return 0;

            return member.CardUpgradeLevels.TryGetValue(deckInstanceId, out var level) ? level : 0;
        }

        public static int GetMaxLevel(string displayName) =>
            CardUpgradeCatalog.TryGetByDisplayName(displayName, out var spec) ? spec.MaxUpgrades : 0;

        public static int GetUpgradeXpCost(string displayName) =>
            CardUpgradeCatalog.GetXpCostPerLevel(displayName);

        public static bool CanUpgrade(PartyMemberSnapshot member, string deckInstanceId, string displayName)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId))
                return false;

            return CardUpgradeCatalog.CanUpgrade(displayName, GetLevel(member, deckInstanceId));
        }

        /// <summary>
        /// 事件/祭坛强化列表：满级或目录中不可升级的卡不出现。
        /// 同时参考模板上已烘焙的 UpgradeLevel，避免字典键漂移时满级卡仍可选。
        /// </summary>
        public static bool CanUpgrade(PartyMemberSnapshot member, CardTemplate template)
        {
            if (member == null || template == null || string.IsNullOrEmpty(template.DisplayName))
                return false;

            if (!CardUpgradeCatalog.TryGetByDisplayName(template.DisplayName, out var spec)
                || spec.MaxUpgrades <= 0)
                return false;

            var level = GetLevel(member, template.DeckInstanceId);
            if (template.UpgradeLevel > level)
                level = template.UpgradeLevel;

            return level < spec.MaxUpgrades;
        }

        public static bool TryUpgradeLevel(
            PartyMemberSnapshot member,
            string deckInstanceId,
            string displayName,
            int levels = 1)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId) || levels <= 0)
                return false;

            if (!CardUpgradeCatalog.TryGetByDisplayName(displayName, out var spec))
                return false;

            var current = GetLevel(member, deckInstanceId);
            var next = Math.Min(spec.MaxUpgrades, current + levels);
            if (next <= current)
                return false;

            member.CardUpgradeLevels[deckInstanceId] = next;
            return true;
        }

        public static void ApplyToTemplate(CardTemplate template, int upgradeLevel)
        {
            if (template == null || upgradeLevel <= 0)
                return;

            if (!CardUpgradeCatalog.TryGetByDisplayName(template.DisplayName, out var spec))
                return;

            var dmg = spec.DamagePerLevel * upgradeLevel;
            var block = spec.BlockPerLevel * upgradeLevel;
            var heal = spec.HealPerLevel * upgradeLevel;
            var poison = spec.PoisonStacksPerLevel * upgradeLevel;
            var slow = spec.SlowStacksPerLevel * upgradeLevel;
            var costReduce = spec.CostReductionPerLevel * upgradeLevel;
            var draw = spec.DrawPerLevel * upgradeLevel;
            var damageReduction = spec.DamageReductionPerLevel * upgradeLevel;
            var respondMitigation = spec.RespondMitigationPerLevel * upgradeLevel;
            var reflectPercent = spec.ReflectPercentPerLevel * upgradeLevel;
            var attackUpPct = spec.AttackUpPercentPerLevel * upgradeLevel;
            var defenseUpPct = spec.DefenseUpPercentPerLevel * upgradeLevel;
            var weaken = spec.WeakenPerLevel * upgradeLevel;
            var vulnerable = spec.VulnerablePerLevel * upgradeLevel;

            if (costReduce > 0)
                template.Cost = Math.Max(0, template.Cost - costReduce);

            foreach (var action in template.Actions)
            {
                switch (action.Type)
                {
                    case EffectActionType.DealDamage when dmg > 0:
                    case EffectActionType.ApplyDelayedDamage when dmg > 0:
                    case EffectActionType.DealDamageAlternateIfHealedThisTurn when dmg > 0:
                    case EffectActionType.DealDamageBonusPerTargetDebuffStack when dmg > 0:
                    case EffectActionType.ConsumeBlockDealDamage when dmg > 0:
                    case EffectActionType.DamagePerRespondCount when dmg > 0:
                    case EffectActionType.EtherealCountBonusDamage when dmg > 0:
                    case EffectActionType.RandomSnakeGodEffect when dmg > 0:
                        action.Value += dmg;
                        break;
                    case EffectActionType.DealDamageScaledByActorHpLoss when dmg > 0:
                        action.HpLossStepValue += dmg;
                        break;
                    case EffectActionType.ApplyConstrict when dmg > 0:
                        action.Value += dmg;
                        break;
                    case EffectActionType.GainBlock when block > 0:
                    case EffectActionType.GainBlockBonusIfSelfPoisoned when block > 0:
                        action.Value += block;
                        break;
                    case EffectActionType.Heal when heal > 0:
                    case EffectActionType.RemovePoisonHealPerStack when heal > 0:
                        action.Value += heal;
                        break;
                    case EffectActionType.ApplyStatus when action.StatusId == "poison" && poison > 0:
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyStatus when action.StatusId == "slow" && slow > 0:
                        action.Stacks += slow;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.OpportunisticStance && dmg > 0:
                        action.Stacks += dmg;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.DamageReduction && damageReduction > 0:
                        action.Stacks += damageReduction;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.Guard && respondMitigation > 0:
                        action.Stacks += respondMitigation;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.FinalBloodRitual && heal > 0:
                        action.Stacks += heal;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.RespondStance && block > 0:
                        action.Stacks += block;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.ImmortalShed && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.BattleWill && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.HeavyArmor && defenseUpPct > 0:
                        action.Stacks += defenseUpPct;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.FinalBulwark && damageReduction > 0:
                        action.Stacks += damageReduction;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.PlagueSpread && reflectPercent > 0:
                        action.Stacks += reflectPercent;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.BloodSharing && heal > 0:
                        action.Stacks += heal;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.BloodFrenzy && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.BloodlineLegacy && heal > 0:
                        action.Stacks += heal;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.AttackUpPercent && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.DefenseUpPercent && defenseUpPct > 0:
                        action.Stacks += defenseUpPct;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.Weaken && weaken > 0:
                        action.Stacks += weaken;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.Vulnerable && vulnerable > 0:
                        action.Stacks += vulnerable;
                        break;
                    case EffectActionType.BuffAllOtherAllies
                        when action.StatusId == StatusCatalog.AttackUpPercent && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                    case EffectActionType.GainBlockFromLastDamagePercent when respondMitigation > 0:
                        action.Value += respondMitigation;
                        break;
                    case EffectActionType.ReflectLastDamageToAttacker when reflectPercent > 0:
                        action.Value += reflectPercent;
                        break;
                    case EffectActionType.DrawCards when draw > 0:
                    case EffectActionType.DrawCardsNextTurn when draw > 0:
                        action.Value += draw;
                        break;
                    case EffectActionType.ApplyPoisonBySpeedCompare when poison > 0:
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyStatusNextTurn
                        when action.StatusId == StatusCatalog.AttackUpPercent && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                }
            }
        }

        public static void ApplyToTemplate(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null)
                return;

            var level = GetLevel(member, template.DeckInstanceId);
            template.UpgradeLevel = level;
            ApplyToTemplate(template, level);
            ApplyFlatDamageBonus(template, member);
        }

        public static string FormatDisplayName(string displayName, int upgradeLevel) =>
            upgradeLevel > 0 && !string.IsNullOrEmpty(displayName)
                ? $"{displayName}+{upgradeLevel}"
                : displayName ?? "";

        public static void ApplyFlatDamageBonus(CardTemplate template, PartyMemberSnapshot member)
        {
            if (template == null || member == null || string.IsNullOrEmpty(template.DeckInstanceId))
                return;

            if (!member.CardFlatDamageBonuses.TryGetValue(template.DeckInstanceId, out var bonus) || bonus <= 0)
                return;

            foreach (var action in template.Actions)
            {
                if (action.Type == EffectActionType.DealDamage
                    || action.Type == EffectActionType.ApplyConstrict)
                    action.Value += bonus;
            }
        }

        public static bool AddFlatDamageToAttackCard(PartyMemberSnapshot member, string deckInstanceId, int amount = 1)
        {
            if (member == null || string.IsNullOrEmpty(deckInstanceId) || amount <= 0)
                return false;

            member.CardFlatDamageBonuses.TryGetValue(deckInstanceId, out var current);
            member.CardFlatDamageBonuses[deckInstanceId] = current + amount;
            return true;
        }

        public static string FormatUpgradeSlots(string displayName, int currentLevel)
        {
            var max = GetMaxLevel(displayName);
            if (max <= 0)
                return "";

            var filled = Math.Clamp(currentLevel, 0, max);
            return new string('●', filled) + new string('○', max - filled);
        }
    }
}
