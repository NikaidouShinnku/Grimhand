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
        public const int GuardDamageReductionPercent = 50;
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

        public static void ClearResolveTurnFlags(BattleState state)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
                combatant.SkipRemainingPlaysThisTurn = false;

            // 只清已消耗的武装；未消耗的「下次受击」可跨回合保留到回合末再过期
            if (state.DefenderRespondArms != null)
            {
                for (var i = state.DefenderRespondArms.Count - 1; i >= 0; i--)
                {
                    if (state.DefenderRespondArms[i] == null || state.DefenderRespondArms[i].Consumed)
                        state.DefenderRespondArms.RemoveAt(i);
                }
            }

            state.SuppressedEnemyCardInstanceIds.Clear();
            state.PlayerRespondStatusUsedThisTurn = false;
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

            if (intendedTarget.Team == attacker.Team)
                return intendedTarget;

            if (StatusRules.HasStatus(intendedTarget, StatusCatalog.Guard))
                return intendedTarget;

            var guardian = FindGuardian(state, intendedTarget.Team);
            if (guardian == null || guardian.Id == intendedTarget.Id)
                return intendedTarget;

            return guardian;
        }

        public static int ApplyGuardReduction(int hpDamage, CombatantState guardian = null)
        {
            if (hpDamage <= 0)
                return 0;

            var percent = GuardDamageReductionPercent;
            if (guardian != null)
            {
                var guard = StatusRules.FindStatus(guardian, StatusCatalog.Guard);
                if (guard != null && guard.Stacks > 1)
                    percent = Math.Clamp(guard.Stacks, 0, 95);
            }

            return Math.Max(1, (int)Math.Round(hpDamage * (100 - percent) / 100f));
        }

        public static int GetEffectiveDefense(BattleState state, CombatantState combatant, int ignoreDefPercent)
        {
            if (combatant == null)
                return 0;

            RelicBattleRules.RefreshDerivedStats(state, combatant, state?.Config?.RunModifiers);
            return combatant.IncomingDamageReductionPercent;
        }

        public static int ComputeHpDamageAfterDefense(int afterBlock, int effectiveDefense)
        {
            if (afterBlock <= 0)
                return 0;

            if (effectiveDefense <= 0)
                return afterBlock;

            return Math.Max(1, (int)Math.Round(
                afterBlock * (100 - Math.Min(100, effectiveDefense)) / 100f));
        }

        public static int ComputeConditionalDamageBonus(
            BattleState state,
            EffectActionSpec action,
            CombatantState target,
            int basePower,
            CombatantState actor = null)
        {
            if (action == null || basePower <= 0)
                return basePower;

            var power = basePower;

            if (target != null)
            {
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

                if (action.BonusIfTargetHasStatusFlat > 0
                    && !string.IsNullOrEmpty(action.BonusIfTargetHasStatusId)
                    && StatusRules.HasStatus(target, action.BonusIfTargetHasStatusId))
                {
                    power += action.BonusIfTargetHasStatusFlat;
                }
            }

            if (action.BonusIfActorFasterThanAllEnemiesFlat > 0
                && actor != null
                && IsActorFasterThanAllEnemies(state, actor))
            {
                power += action.BonusIfActorFasterThanAllEnemiesFlat;
            }

            return power;
        }

        public static bool IsActorFasterThanAllEnemies(BattleState state, CombatantState actor)
        {
            if (state == null || actor == null || !actor.IsAlive)
                return false;

            var opposing = actor.Team == TeamSide.Enemy ? TeamSide.Player : TeamSide.Enemy;
            var actorSpeed = StatusRules.GetEffectiveSpeed(state, actor);
            var foundEnemy = false;
            foreach (var enemy in state.GetTeam(opposing))
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                foundEnemy = true;
                if (StatusRules.GetEffectiveSpeed(state, enemy) >= actorSpeed)
                    return false;
            }

            return foundEnemy;
        }

        public static int ComputeActionValueForTarget(
            BattleState state,
            EffectActionSpec action,
            CombatantState owner,
            CombatantState target)
        {
            if (action == null)
                return 0;

            var useAlternateDebuff = action.UseAlternateIfTargetHasDebuff
                                     && target != null
                                     && StatusRules.HasDebuff(target);
            var useAlternateAnyStatus = action.UseAlternateIfTargetHasAnyStatus
                                        && target != null
                                        && StatusRules.HasAnyStatus(target);
            var useAlternateAttack = action.AlternateAttackScaleIfActorUsedAttack > 0
                                     && owner != null
                                     && owner.UsedAttackThisTurn;
            var useAlternateNotHit = action.UseAlternateIfActorNotHitThisTurn
                                     && owner != null
                                     && !owner.HitThisTurn
                                     && action.AlternateValue > 0;
            var useAlternateSelfBlock = action.SelfBlockAboveThreshold > 0
                                        && owner != null
                                        && owner.Block > action.SelfBlockAboveThreshold
                                        && action.AlternateValueIfSelfBlockAbove > 0;

            var working = EffectActionSpec.Clone(action);
            if (useAlternateDebuff || useAlternateAnyStatus)
            {
                working.Value = action.AlternateValue;
                working.AttackScalePercent = action.AlternateAttackScalePercent > 0
                    ? action.AlternateAttackScalePercent
                    : action.AttackScalePercent;
            }
            else if (useAlternateNotHit)
            {
                working.Value = action.AlternateValue;
            }
            else if (useAlternateSelfBlock)
            {
                working.Value = action.AlternateValueIfSelfBlockAbove;
            }
            else if (useAlternateAttack)
            {
                working.Value = action.AlternateValueIfActorUsedAttack;
                working.AttackScalePercent = action.AlternateAttackScaleIfActorUsedAttack;
            }

            var power = CardPowerRules.ComputeActionValue(working, owner);

            if (action.DamageMultiplierPercentIfRespondArmed > 0
                && action.DamageMultiplierPercentIfRespondArmed != 100
                && owner != null
                && (owner.RespondArmedThisTurn || owner.DodgeChanceBonus > 0f))
            {
                power = Math.Max(1, (int)Math.Round(power * action.DamageMultiplierPercentIfRespondArmed / 100f));
            }

            if (owner != null && action.Type == EffectActionType.DealDamage)
                power = MinionTraitRules.ApplyMinionOutgoingAttackBonus(state, owner, target, CardType.Attack, power);

            return power;
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
            DamageRules.ApplyHeal(state, actor, heal, events, actor, isLifesteal: true);
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
