using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    /// <summary>
    /// 卡牌机制框架：嘲讽、守护、条件加伤、吸血、死亡触发等。
    /// </summary>
    public static class CombatMechanicsRules
    {
        public const int GuardDamageReductionPercent = 40;
        public const int ReviveBlessingHpPercent = 25;
        public const int UnyieldingHealAmount = 20;
        public const int UnyieldingHpThresholdPercent = 25;

        public static void ClearTurnFlags(BattleState state)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
                combatant.HitThisTurn = false;
        }

        public static CombatantState FindTauntHolder(BattleState state, TeamSide defenderTeam)
        {
            if (state == null)
                return null;

            foreach (var combatant in state.GetTeam(defenderTeam))
            {
                if (!combatant.IsAlive)
                    continue;

                if (StatusRules.HasStatus(combatant, StatusCatalog.Taunt))
                    return combatant;
            }

            return null;
        }

        public static CombatantState FindGuardian(BattleState state, TeamSide defenderTeam)
        {
            if (state == null)
                return null;

            foreach (var combatant in state.GetTeam(defenderTeam))
            {
                if (!combatant.IsAlive)
                    continue;

                if (StatusRules.HasStatus(combatant, StatusCatalog.Guard))
                    return combatant;
            }

            return null;
        }

        public static CombatantState ResolveDamageRecipient(
            BattleState state,
            CombatantState attacker,
            CombatantState intendedTarget)
        {
            if (state == null || intendedTarget == null || attacker == null)
                return intendedTarget;

            if (intendedTarget.Team != attacker.Team)
                return intendedTarget;

            if (StatusRules.HasStatus(intendedTarget, StatusCatalog.Guard))
                return intendedTarget;

            var guardian = FindGuardian(state, intendedTarget.Team);
            if (guardian == null || guardian.Id == intendedTarget.Id)
                return intendedTarget;

            return guardian;
        }

        public static int ApplyGuardReduction(int hpDamage)
        {
            if (hpDamage <= 0)
                return 0;

            return Math.Max(1, (int)Math.Round(hpDamage * (100 - GuardDamageReductionPercent) / 100f));
        }

        public static int GetEffectiveDefense(BattleState state, CombatantState combatant, int ignoreDefPercent)
        {
            if (combatant == null)
                return 0;

            RelicBattleRules.RefreshDerivedStats(state, combatant, state?.Config?.RunModifiers);
            var defense = combatant.Defense;
            if (ignoreDefPercent <= 0)
                return Math.Max(0, defense);

            return Math.Max(0, (int)Math.Round(defense * (100 - ignoreDefPercent) / 100f));
        }

        public static int ComputeConditionalDamageBonus(
            BattleState state,
            EffectActionSpec action,
            CombatantState target,
            int basePower)
        {
            if (action == null || target == null || basePower <= 0)
                return basePower;

            var power = basePower;

            if (action.BonusIfTargetHpBelowPercent > 0
                && target.MaxHp > 0
                && target.Hp * 100 / target.MaxHp < action.BonusIfTargetHpBelowPercent)
            {
                power += action.BonusIfTargetHpBelowFlat;
            }

            if (action.BonusIfTargetHitThisTurnPercent > 0 && target.HitThisTurn)
            {
                power += Math.Max(1, (int)Math.Round(basePower * action.BonusIfTargetHitThisTurnPercent / 100f));
            }

            return power;
        }

        public static int ComputeHpDamageAfterDefense(int afterBlock, int effectiveDefense)
        {
            if (afterBlock <= 0)
                return 0;

            return Math.Max(1, afterBlock - effectiveDefense);
        }

        public static bool TryPreventDeathWithReviveBlessing(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (state == null || target == null || !StatusRules.HasStatus(target, StatusCatalog.ReviveBlessing))
                return false;

            StatusRules.RemoveStatus(target, StatusCatalog.ReviveBlessing, 1, events);
            var restored = Math.Max(1, (int)Math.Round(target.MaxHp * ReviveBlessingHpPercent / 100f));
            target.Hp = restored;
            CombatantDeathRules.RestoreUsableCards(state, target);
            RelicBattleRules.RefreshDerivedStats(state, target, state.Config?.RunModifiers);

            events.Add(new BattleEvent(BattleEventKind.CharacterRevived, $"{target.DisplayName}（复活祝福）")
            {
                CombatantId = target.Id,
                Amount = restored
            });

            return true;
        }

        public static void TryTriggerUnyielding(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (state == null || target == null || !target.IsAlive)
                return;

            if (!StatusRules.HasStatus(target, StatusCatalog.Unyielding))
                return;

            if (target.MaxHp <= 0)
                return;

            if (target.Hp * 100 / target.MaxHp >= UnyieldingHpThresholdPercent)
                return;

            StatusRules.RemoveStatus(target, StatusCatalog.Unyielding, 1, events);
            DamageRules.ApplyHeal(state, target, UnyieldingHealAmount, events, target);
        }

        public static void ApplyLifesteal(
            BattleState state,
            CombatantState actor,
            int damageDealt,
            int lifestealPercent,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || damageDealt <= 0 || lifestealPercent <= 0)
                return;

            var heal = Math.Max(1, (int)Math.Round(damageDealt * lifestealPercent / 100f));
            DamageRules.ApplyHeal(state, actor, heal, events, actor);
        }

        public static int GetPendingLifestealPercent(CombatantState actor)
        {
            if (actor == null)
                return 0;

            var status = StatusRules.FindStatus(actor, StatusCatalog.VampAura);
            return status?.Stacks ?? 0;
        }

        public static void ConsumeVampAura(CombatantState actor, List<BattleEvent> events)
        {
            if (actor == null || !StatusRules.HasStatus(actor, StatusCatalog.VampAura))
                return;

            StatusRules.RemoveStatus(actor, StatusCatalog.VampAura, 1, events);
        }
    }
}
