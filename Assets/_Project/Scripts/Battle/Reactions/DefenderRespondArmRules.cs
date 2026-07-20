using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;

namespace Grimhand.Battle.Reactions
{
    public sealed class DefenderRespondArm
    {
        public string DefenderId { get; set; } = "";
        public int MitigationPercent { get; set; }
        public bool RedirectDoubleToRandomAlly { get; set; }
        public bool Consumed { get; set; }
        public bool GrantInvulnerableUntilTurnEnd { get; set; }
        public int SideEffectAllyDamage { get; set; }
        public string SideEffectAllyCharacterId { get; set; } = "";
        /// <summary>武装时所在回合；未消耗时可跨入下一回合，再下一次回合末过期。</summary>
        public int ArmedOnTurn { get; set; }
        /// <summary>应对成功时对攻击者施加减速层数（钻地逃遁等）。</summary>
        public int SlowAttackerStacks { get; set; }
        public int SlowAttackerDuration { get; set; } = 2;
        /// <summary>应对成功时锁定攻击者攻击牌的回合数（蛛网包裹等）。</summary>
        public int LockAttackerTurns { get; set; }
    }

    /// <summary>敌方防御牌【应对攻击】：出牌后武装，下次受到玩家攻击时生效。</summary>
    public static class DefenderRespondArmRules
    {
        public static void ArmMitigation(
            BattleState state,
            string defenderId,
            int mitigationPercent,
            bool grantInvulnerableUntilTurnEnd = false,
            int sideEffectAllyDamage = 0,
            string sideEffectAllyCharacterId = "",
            int slowAttackerStacks = 0,
            int slowAttackerDuration = 2,
            int lockAttackerTurns = 0)
        {
            if (state == null || string.IsNullOrEmpty(defenderId))
                return;

            state.DefenderRespondArms.Add(new DefenderRespondArm
            {
                DefenderId = defenderId,
                MitigationPercent = mitigationPercent,
                GrantInvulnerableUntilTurnEnd = grantInvulnerableUntilTurnEnd,
                SideEffectAllyDamage = sideEffectAllyDamage,
                SideEffectAllyCharacterId = sideEffectAllyCharacterId ?? "",
                ArmedOnTurn = state.TurnNumber,
                SlowAttackerStacks = System.Math.Max(0, slowAttackerStacks),
                SlowAttackerDuration = slowAttackerDuration,
                LockAttackerTurns = System.Math.Max(0, lockAttackerTurns)
            });
        }

        public static void ArmRedirectDouble(BattleState state, string defenderId)
        {
            if (state == null || string.IsNullOrEmpty(defenderId))
                return;

            state.DefenderRespondArms.Add(new DefenderRespondArm
            {
                DefenderId = defenderId,
                RedirectDoubleToRandomAlly = true,
                ArmedOnTurn = state.TurnNumber
            });
        }

        /// <summary>
        /// 回合末清理：已消耗的立刻去掉；更早回合武装且仍未触发的过期。
        /// 本回合刚武装的可保留到下回合（「下次受击」）。
        /// </summary>
        public static void ExpireArmsAtEndOfTurn(BattleState state)
        {
            if (state?.DefenderRespondArms == null)
                return;

            for (var i = state.DefenderRespondArms.Count - 1; i >= 0; i--)
            {
                var arm = state.DefenderRespondArms[i];
                if (arm == null || arm.Consumed || arm.ArmedOnTurn < state.TurnNumber)
                    state.DefenderRespondArms.RemoveAt(i);
            }
        }

        public static void TryArmFromEnemyCardResolve(
            BattleState state,
            CombatantState actor,
            CardInstanceState card)
        {
            if (state == null || actor == null || card == null || actor.Team != TeamSide.Enemy)
                return;

            foreach (var action in card.Actions)
            {
                if (action.Type == EffectActionType.GainBlockFromLastDamagePercent
                    && action.Condition == ReactionConditionType.LastActionAttackOnSelf
                    && action.Value > 0)
                {
                    ReadSlowOnAttackerSideEffect(card, out var slowStacks, out var slowDuration);
                    ReadLockAttackOnAttackerSideEffect(card, out var lockTurns);
                    ArmMitigation(
                        state,
                        actor.Id,
                        action.Value,
                        action.GrantInvulnerableOnRespondArm,
                        action.RespondSideEffectAllyDamage,
                        action.RespondSideEffectAllyCharacterId,
                        slowStacks,
                        slowDuration,
                        lockTurns);
                    actor.RespondArmedThisTurn = true;
                    return;
                }

                if (action.Type == EffectActionType.ArmRespondDamageRedirect)
                {
                    // Condition=None：由出牌无条件结算武装；有应对条件时仅在此武装
                    if (action.Condition == ReactionConditionType.None)
                        continue;

                    ArmRedirectDouble(state, actor.Id);
                    actor.RespondArmedThisTurn = true;
                    return;
                }
            }
        }

        static void ReadSlowOnAttackerSideEffect(
            CardInstanceState card,
            out int slowStacks,
            out int slowDuration)
        {
            slowStacks = 0;
            slowDuration = 2;
            if (card?.Actions == null)
                return;

            foreach (var action in card.Actions)
            {
                if (action.Type != EffectActionType.ApplyStatus
                    || action.Condition != ReactionConditionType.LastActionAttackOnSelf
                    || action.StatusId != StatusCatalog.Slow
                    || action.Stacks <= 0)
                    continue;

                if (action.Target is EffectTarget.LastActionActor or EffectTarget.DefaultEnemy)
                {
                    slowStacks = action.Stacks;
                    slowDuration = action.Duration;
                    return;
                }
            }
        }

        static void ReadLockAttackOnAttackerSideEffect(CardInstanceState card, out int lockTurns)
        {
            lockTurns = 0;
            if (card?.Actions == null)
                return;

            foreach (var action in card.Actions)
            {
                if (action.Type != EffectActionType.LockAttackCards
                    || action.Condition != ReactionConditionType.LastActionAttackOnSelf
                    || action.Value <= 0)
                    continue;

                if (action.Target is EffectTarget.LastActionActor or EffectTarget.DefaultEnemy)
                {
                    lockTurns = action.Value;
                    return;
                }
            }
        }

        public static bool TryConsumeForIncomingPlayerAttack(
            BattleState state,
            CombatantState attacker,
            ref CombatantState recipient,
            ref int hpDamage,
            List<BattleEvent> events,
            out int mitigatedAmount,
            BattleRng rng = null)
        {
            mitigatedAmount = 0;
            if (state == null || attacker == null || recipient == null || attacker.Team != TeamSide.Player)
                return false;

            var arm = FindActiveArm(state, recipient.Id);
            if (arm == null)
                return false;

            arm.Consumed = true;

            if (arm.RedirectDoubleToRandomAlly)
            {
                var redirectTarget = PickRandomPlayerCombatant(state, excludeId: null, rng);
                if (redirectTarget != null)
                {
                    var originalId = recipient.Id;
                    recipient = redirectTarget;
                    hpDamage = System.Math.Max(0, hpDamage * 2);
                    events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                        $"女王的命令：伤害×2 转嫁给 {redirectTarget.DisplayName}")
                    {
                        CombatantId = originalId,
                        TargetId = redirectTarget.Id,
                        Amount = hpDamage
                    });
                }
            }

            if (arm.MitigationPercent > 0 && hpDamage > 0)
            {
                var before = hpDamage;
                hpDamage = (int)System.Math.Round(hpDamage * (100 - arm.MitigationPercent) / 100f);
                mitigatedAmount = before - hpDamage;
            }

            if (arm.SlowAttackerStacks > 0 && attacker.IsAlive)
            {
                StatusRules.ApplyStatus(
                    state,
                    attacker,
                    StatusCatalog.Slow,
                    arm.SlowAttackerStacks,
                    arm.SlowAttackerDuration,
                    events);
            }

            if (arm.LockAttackerTurns > 0 && attacker.IsAlive)
                CardLockRules.ApplyAttackLock(attacker, arm.LockAttackerTurns);

            if (arm.GrantInvulnerableUntilTurnEnd)
            {
                recipient.InvulnerableRestOfTurn = true;
                StatusRules.ApplyStatus(state, recipient, StatusCatalog.Invulnerable, 1, -1, events);
            }

            if (arm.SideEffectAllyDamage > 0 && !string.IsNullOrEmpty(arm.SideEffectAllyCharacterId))
            {
                // 必须以应对方（典狱长）为参照，才能找到同队囚笼；勿用攻击者（玩家）
                V09BossMechanicsRules.DamageRandomAllyByCharacterId(
                    state,
                    recipient,
                    arm.SideEffectAllyCharacterId,
                    arm.SideEffectAllyDamage,
                    events,
                    rng);
            }

            return true;
        }

        static DefenderRespondArm FindActiveArm(BattleState state, string defenderId)
        {
            foreach (var arm in state.DefenderRespondArms)
            {
                if (!arm.Consumed && arm.DefenderId == defenderId)
                    return arm;
            }

            return null;
        }

        static CombatantState PickRandomPlayerCombatant(
            BattleState state,
            string excludeId,
            BattleRng rng)
        {
            var pool = new List<CombatantState>();
            foreach (var unit in state.GetTeam(TeamSide.Player))
            {
                if (unit != null && unit.IsAlive && unit.Id != excludeId)
                    pool.Add(unit);
            }

            if (pool.Count == 0)
            {
                foreach (var unit in state.GetTeam(TeamSide.Player))
                {
                    if (unit != null && unit.IsAlive)
                        pool.Add(unit);
                }
            }

            if (pool.Count == 0)
                return null;

            if (pool.Count == 1 || rng == null)
                return pool[0];

            return pool[rng.NextIndex(pool.Count)];
        }
    }
}
