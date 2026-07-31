using System;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;

namespace Grimhand.Battle.Rules
{
    /// <summary>v0.8 战斗修饰符：增伤、虚弱、易伤、减伤、护甲获取增减。</summary>
    public static class CombatModifierRules
    {
        public static void RefreshCombatantModifiers(
            BattleState state,
            CombatantState combatant,
            RunModifierSnapshot mods)
        {
            if (combatant == null)
                return;

            combatant.OutgoingDamageFlatBonus = 0;
            combatant.OutgoingDamagePercentBonus = 0;
            combatant.OutgoingDamageReductionFlat = 0;
            combatant.IncomingDamageFlatBonus = 0;
            combatant.IncomingDamagePercentBonus = 0;
            combatant.IncomingDamageReductionPercent = 0;
            combatant.BlockGainFlatBonus = 0;
            combatant.BlockGainPercentBonus = 0;
            combatant.BlockGainReductionPercent = 0;

            // 新版无 ATK/DEF：属性字段不参与伤害/护甲结算
            combatant.BaseAttack = 0;
            combatant.BaseDefense = 0;
            combatant.Attack = 0;
            combatant.Defense = 0;

            // 突击姿态等条件状态需在读取 Status 列表前同步，避免与 ApplyStatus 递归刷新
            TalentBattleRules.SyncConditionalTalentStatuses(state, combatant);

            foreach (var status in combatant.Statuses)
            {
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;

                var stacks = status.Stacks;
                combatant.OutgoingDamageFlatBonus += def.OutgoingDamageFlatPerStack * stacks;
                combatant.OutgoingDamagePercentBonus += def.OutgoingDamagePercentPerStack * stacks;
                combatant.OutgoingDamageReductionFlat += def.OutgoingDamageReductionFlatPerStack * stacks;
                combatant.IncomingDamageFlatBonus += def.IncomingDamageFlatPerStack * stacks;
                combatant.IncomingDamagePercentBonus += def.IncomingDamagePercentPerStack * stacks;
                combatant.IncomingDamageReductionPercent += def.IncomingDamageReductionPercentPerStack * stacks;
                combatant.BlockGainFlatBonus += def.BlockGainFlatPerStack * stacks;
                combatant.BlockGainPercentBonus += def.BlockGainPercentPerStack * stacks;
                combatant.BlockGainReductionPercent += def.BlockGainReductionPercentPerStack * stacks;

                // 旧状态字段兼容映射
                combatant.OutgoingDamageFlatBonus += def.AttackModifierPerStack * stacks;
                combatant.OutgoingDamagePercentBonus += def.AttackPercentBonusPerStack * stacks;
                combatant.BlockGainFlatBonus += def.DefenseModifierPerStack * stacks;
                combatant.BlockGainPercentBonus += def.DefensePercentBonusPerStack * stacks;
            }

            if (combatant.RatPackAttackBonusPercent > 0)
                combatant.OutgoingDamagePercentBonus += combatant.RatPackAttackBonusPercent;

            if (combatant.MermaidZeroCostAttackBonusPercent > 0)
                combatant.OutgoingDamagePercentBonus += combatant.MermaidZeroCostAttackBonusPercent;

            if (combatant.TurnAttackBonusPercent > 0)
                combatant.OutgoingDamagePercentBonus += combatant.TurnAttackBonusPercent;

            if (combatant.TurnDefenseBonusPercent > 0)
                combatant.BlockGainPercentBonus += combatant.TurnDefenseBonusPercent;

            if (mods != null && combatant.Team == TeamSide.Player)
            {
                combatant.OutgoingDamageFlatBonus += mods.TeamAttackBonus;
                combatant.BlockGainFlatBonus += mods.TeamDefenseBonus;

                if (mods.TeamAttackBonusPercent > 0f)
                    combatant.OutgoingDamagePercentBonus += (int)System.Math.Round(mods.TeamAttackBonusPercent);
                if (mods.TeamBlockGainBonusPercent > 0f)
                    combatant.BlockGainPercentBonus += (int)System.Math.Round(mods.TeamBlockGainBonusPercent);

                if (mods.FelskullOutgoingDamagePercentBonus > 0)
                    combatant.OutgoingDamagePercentBonus += mods.FelskullOutgoingDamagePercentBonus;

                if (mods.FrontDefenseBonus != 0 && state != null)
                {
                    var effective = PositionRules.GetEffectiveSlot(state, combatant);
                    if (effective == FormationSlot.Front)
                        combatant.BlockGainFlatBonus += mods.FrontDefenseBonus;
                }
            }

            // 神罚增伤由 StatusCatalog.DivinePunishmentAtk 状态结算并展示脚标。

            combatant.OutgoingDamageFlatBonus += combatant.PersistentOutgoingDamageFlatBonus;
            combatant.BlockGainFlatBonus += combatant.PersistentBlockGainFlatBonus;

            if (StatusRules.HasStatus(combatant, StatusCatalog.LastStand))
                combatant.OutgoingDamagePercentBonus += 20;

            TalentBattleRules.ApplyCombatModifiers(state, combatant, mods);

            V09BossMechanicsRules.ApplyExtraTideDamageReduction(combatant);
        }

        public static int ApplyOutgoingDamageModifiers(
            CombatantState actor,
            CardType cardType,
            int power)
        {
            if (actor == null || power <= 0 || cardType != CardType.Attack)
                return power;

            power += actor.OutgoingDamageFlatBonus;
            power -= actor.OutgoingDamageReductionFlat;
            power += actor.NextAttackFlatBonus;

            if (actor.OutgoingDamagePercentBonus != 0)
            {
                power = (int)Math.Round(power * (100 + actor.OutgoingDamagePercentBonus) / 100f);
            }

            return Math.Max(1, power);
        }

        public static int ApplyBlockGainModifiers(CombatantState actor, int block)
        {
            if (actor == null || block <= 0)
                return block;

            block += actor.BlockGainFlatBonus;

            if (actor.BlockGainPercentBonus != 0)
            {
                block = (int)Math.Round(block * (100 + actor.BlockGainPercentBonus) / 100f);
            }

            if (actor.BlockGainReductionPercent > 0)
            {
                block = (int)Math.Round(
                    block * (100 - Math.Min(100, actor.BlockGainReductionPercent)) / 100f);
            }

            return Math.Max(0, block);
        }

        public static int ApplyIncomingDamageModifiers(
            CombatantState recipient,
            int afterBlock,
            int ignoreReductionPercent)
        {
            if (afterBlock <= 0 || recipient == null)
                return 0;

            var damage = afterBlock + recipient.IncomingDamageFlatBonus;
            if (damage <= 0)
                return 0;

            if (recipient.IncomingDamagePercentBonus != 0)
            {
                damage = (int)Math.Round(
                    damage * (100 + recipient.IncomingDamagePercentBonus) / 100f);
            }

            var reduction = recipient.IncomingDamageReductionPercent;
            if (ignoreReductionPercent > 0)
            {
                reduction = (int)Math.Round(
                    reduction * (100 - Math.Min(100, ignoreReductionPercent)) / 100f);
            }

            if (reduction > 0)
            {
                damage = (int)Math.Round(
                    damage * (100 - Math.Min(100, reduction)) / 100f);
            }

            return Math.Max(1, damage);
        }

        public static int ComputeEffectiveBlock(CombatantState recipient, int ignoreBlockPercent)
        {
            if (recipient == null || recipient.Block <= 0)
                return 0;

            if (ignoreBlockPercent <= 0)
                return recipient.Block;

            return Math.Max(0, (int)Math.Round(
                recipient.Block * (100 - Math.Min(100, ignoreBlockPercent)) / 100f));
        }
    }
}
