using System;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>对战斗内卡牌实例追加升级层数（可超出卡牌升级上限）。</summary>
    public static class CardInstanceUpgradeApplier
    {
        public static CardInstanceState ApplyBonusLevels(CardInstanceState source, int bonusLevels)
        {
            if (source == null || bonusLevels <= 0)
                return source;

            if (!CardUpgradeCatalog.TryGetByDisplayName(source.DisplayName, out var spec))
                return source;

            var clone = Clone(source);
            // Use the same upgrade effects as expedition upgrades; only the level cap differs.
            var template = new CardTemplate { DisplayName = source.DisplayName, Cost = clone.Cost };
            template.Actions.AddRange(clone.Actions);
            ApplyToTemplate(template, bonusLevels);
            var costReduction = clone.Cost - template.Cost;
            clone.Cost = template.Cost;
            clone.BaseCost = Math.Max(0, clone.BaseCost - costReduction);
            if (clone.CostBeforeHandDiscount.HasValue)
                clone.CostBeforeHandDiscount = Math.Max(0,
                    clone.CostBeforeHandDiscount.Value - spec.CostReductionPerLevel * bonusLevels);

            clone.UpgradeLevel = source.UpgradeLevel + bonusLevels;
            return clone;
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
                    case EffectActionType.DealDamage when dmg > 0 && action.Target != EffectTarget.Self:
                        action.Value += dmg;
                        if (action.UseAlternateIfTargetHasDebuff || action.UseAlternateIfTargetHasAnyStatus
                            || action.UseAlternateIfActorNotHitThisTurn)
                            action.AlternateValue += dmg;
                        if (action.AlternateValueIfActorUsedAttack > 0)
                            action.AlternateValueIfActorUsedAttack += dmg;
                        break;
                    case EffectActionType.DealDamageAlternateIfHealedThisTurn when dmg > 0:
                        var doublesWhenHealed = action.AlternateValueIfHealed == action.Value * 2;
                        action.Value += dmg;
                        action.AlternateValueIfHealed += doublesWhenHealed ? dmg * 2 : dmg;
                        break;
                    case EffectActionType.RandomSnakeGodEffect:
                        action.Value += dmg;
                        action.AlternateValue += dmg;
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyPoisonBySpeedCompare when dmg > 0 || poison > 0:
                        action.Value += dmg;
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyDelayedDamage when dmg > 0:
                    case EffectActionType.DealDamageBonusPerTargetDebuffStack when dmg > 0:
                    case EffectActionType.ConsumeBlockDealDamage when dmg > 0:
                    case EffectActionType.DamagePerRespondCount when dmg > 0:
                    case EffectActionType.EtherealCountBonusDamage when dmg > 0:
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
                        when action.StatusId is StatusCatalog.RotAvatar or StatusCatalog.VenomSacBurst:
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyStatus
                        when action.StatusId == StatusCatalog.PsionicBody:
                        action.Stacks += attackUpPct;
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
                    case EffectActionType.ApplyStatusNextTurn
                        when action.StatusId == StatusCatalog.AttackUpPercent && attackUpPct > 0:
                        action.Stacks += attackUpPct;
                        break;
                }
            }
        }

        static CardInstanceState Clone(CardInstanceState source)
        {
            var clone = new CardInstanceState
            {
                InstanceId = source.InstanceId,
                DefinitionId = source.DefinitionId,
                OwnerCharacterId = source.OwnerCharacterId,
                OwnerCombatantId = source.OwnerCombatantId,
                Cost = source.Cost,
                BaseCost = source.BaseCost != 0 || source.Cost == 0 ? source.BaseCost : source.Cost,
                CardType = source.CardType,
                IsUsable = source.IsUsable,
                DisabledByDeath = source.DisabledByDeath,
                CostBeforeHandDiscount = source.CostBeforeHandDiscount,
                RetainInHandOverTurnEnd = source.RetainInHandOverTurnEnd,
                IsBonusHandCard = source.IsBonusHandCard,
                BonusHandGrantedTurn = source.BonusHandGrantedTurn,
                DisplayName = source.DisplayName,
                UpgradeLevel = source.UpgradeLevel
            };

            foreach (var keyword in source.Keywords)
                clone.Keywords.Add(keyword);

            foreach (var action in source.Actions)
                clone.Actions.Add(EffectActionSpec.Clone(action));

            return clone;
        }
    }
}
