using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Reactions
{
    public sealed class DefenderRespondArm
    {
        public string DefenderId { get; set; } = "";
        public int MitigationPercent { get; set; }
        public bool RedirectDoubleToRandomAlly { get; set; }
        public bool Consumed { get; set; }
        public bool GrantInvulnerableUntilTurnEnd { get; set; }
    }

    /// <summary>敌方防御牌【应对攻击】：出牌后武装，下次受到玩家攻击时生效。</summary>
    public static class DefenderRespondArmRules
    {
        public static void ArmMitigation(
            BattleState state,
            string defenderId,
            int mitigationPercent,
            bool grantInvulnerableUntilTurnEnd = false)
        {
            if (state == null || string.IsNullOrEmpty(defenderId))
                return;

            state.DefenderRespondArms.Add(new DefenderRespondArm
            {
                DefenderId = defenderId,
                MitigationPercent = mitigationPercent,
                GrantInvulnerableUntilTurnEnd = grantInvulnerableUntilTurnEnd
            });
        }

        public static void ArmRedirectDouble(BattleState state, string defenderId)
        {
            if (state == null || string.IsNullOrEmpty(defenderId))
                return;

            state.DefenderRespondArms.Add(new DefenderRespondArm
            {
                DefenderId = defenderId,
                RedirectDoubleToRandomAlly = true
            });
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
                    ArmMitigation(state, actor.Id, action.Value, action.GrantInvulnerableOnRespondArm);
                    actor.RespondArmedThisTurn = true;
                    return;
                }

                if (action.Type == EffectActionType.ArmRespondDamageRedirect)
                {
                    ArmRedirectDouble(state, actor.Id);
                    return;
                }
            }
        }

        public static bool TryConsumeForIncomingPlayerAttack(
            BattleState state,
            CombatantState attacker,
            ref CombatantState recipient,
            ref int hpDamage,
            out int mitigatedAmount)
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
                var redirectTarget = PickRandomPlayerAlly(state, recipient.Id);
                if (redirectTarget != null)
                    recipient = redirectTarget;

                hpDamage = System.Math.Max(0, hpDamage * 2);
            }

            if (arm.MitigationPercent > 0 && hpDamage > 0)
            {
                var before = hpDamage;
                hpDamage = (int)System.Math.Round(hpDamage * (100 - arm.MitigationPercent) / 100f);
                mitigatedAmount = before - hpDamage;
            }

            if (arm.GrantInvulnerableUntilTurnEnd)
                recipient.InvulnerableRestOfTurn = true;

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

        static CombatantState PickRandomPlayerAlly(BattleState state, string excludeId)
        {
            var pool = new List<CombatantState>();
            foreach (var unit in state.GetTeam(TeamSide.Player))
            {
                if (unit.IsAlive)
                    pool.Add(unit);
            }

            if (pool.Count == 0)
                return null;

            if (pool.Count == 1)
                return pool[0];

            CombatantState fallback = null;
            foreach (var unit in pool)
            {
                if (unit.Id != excludeId)
                    return unit;

                fallback = unit;
            }

            return fallback;
        }
    }
}
