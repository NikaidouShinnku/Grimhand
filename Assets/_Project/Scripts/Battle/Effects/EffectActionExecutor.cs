using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Battle.Effects
{
    public static class EffectActionExecutor
    {
        public static void ExecuteAll(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            ExecuteConditionalActions(state, actor, card, events, rng);
            ExecuteUnconditionalActions(state, actor, card, events, rng);
        }

        public static void ExecuteUnconditionalActions(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                ExecuteOne(state, actor, card, action, events, rng, sourceCardInstanceId: card.InstanceId);
            }
        }

        static void ExecuteConditionalActions(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (RespondRules.IsRespondCard(card))
                return;

            var triggeredReaction = false;
            foreach (var action in card.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                if (!ReactionRules.MeetsCondition(state, action.Condition, actor.Id))
                    continue;

                triggeredReaction = true;
                ExecuteOne(state, actor, card, action, events, rng, sourceCardInstanceId: card.InstanceId);
            }

            if (triggeredReaction)
            {
                events.Add(new BattleEvent(BattleEventKind.ReactionTriggered, card.DisplayName)
                {
                    CombatantId = actor.Id
                });
            }
        }

        static void ExecuteOne(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            var target = TargetRules.ResolveTarget(state, actor, action.Target, card.InstanceId);
            var value = CardPowerRules.ComputeActionValue(action, actor);
            if (action.Type == EffectActionType.DealDamage
                && action.Target == EffectTarget.Self
                && card.Keywords.Contains("sacrifice"))
            {
                value = RelicEffectRules.AdjustSacrificeSelfDamage(
                    state.Config?.RunModifiers, actor, value);
            }

            var beneficiary = target ?? actor;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    if (action.Target == EffectTarget.AllEnemies)
                        ExecuteDamageToAllEnemies(state, actor, card, action, value, events, rng, sourceCardInstanceId);
                    else if (target != null)
                        ExecuteDamage(
                            state, actor, card, action, target, value, events, rng, sourceCardInstanceId,
                            isSacrificeSelfDamage: action.Target == EffectTarget.Self
                                && card.Keywords.Contains("sacrifice"));
                    break;
                case EffectActionType.GainBlock:
                {
                    var totalBlock = value + RelicBattleRules.GetOutgoingDefenseFlatBonus(
                        state.Config?.RunModifiers, actor);
                    totalBlock = RelicBattleRules.ApplyPharaohBlockBonus(
                        state.Config?.RunModifiers, actor, totalBlock);
                    DamageRules.ApplyBlock(beneficiary, totalBlock, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, beneficiary.Id, false, 0);
                    break;
                }
                case EffectActionType.Heal:
                {
                    var healAmount = action.HealMaxHpPercent > 0
                        ? System.Math.Max(1, (int)System.Math.Round(
                            beneficiary.MaxHp * action.HealMaxHpPercent / 100f))
                        : value;
                    DamageRules.ApplyHeal(state, beneficiary, healAmount, events, actor);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, beneficiary.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyStatus:
                    if (target != null)
                        StatusRules.ApplyStatus(state, target, action.StatusId, action.Stacks, action.Duration, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status,
                        target != null ? target.Id : actor.Id, false, 0);
                    break;
                case EffectActionType.RemoveStatus:
                    if (target != null)
                        StatusRules.RemoveStatus(target, action.StatusId, action.Stacks, events);
                    break;
                case EffectActionType.SwapPositionWithFrontAlly:
                    PositionRules.SwapWithAdjacentAlly(state, actor, -1, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.DrawCardsNextTurn:
                    state.PendingDrawNextTurn += value;
                    events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"下回合额外抽 {value} 张")
                    {
                        CombatantId = actor.Id
                    });
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.DrawCards:
                    state.PendingDrawNextTurn += value;
                    events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"下回合额外抽 {value} 张")
                    {
                        CombatantId = actor.Id,
                        CardInstanceId = card.InstanceId
                    });
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ReflectLastDamageToAttacker:
                    var attacker = TargetRules.ResolveTarget(state, actor, EffectTarget.LastActionActor, card.InstanceId);
                    if (attacker != null)
                    {
                        var reflected = state.LastAction.DamageAmount * action.Value / 100;
                        if (reflected > 0)
                            DamageRules.ApplyDamage(
                                state, actor, attacker, reflected, card.CardType, events,
                                canTriggerParry: false,
                                sourceCardInstanceId: sourceCardInstanceId);
                    }
                    break;
                case EffectActionType.GainBlockFromLastDamagePercent:
                {
                    var blockFromDamage = state.LastAction.DamageAmount * action.Value / 100;
                    if (blockFromDamage > 0)
                    {
                        DamageRules.ApplyBlock(actor, blockFromDamage, events);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    }

                    break;
                }
            }
        }

        static void ExecuteDamageToAllEnemies(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            int value,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var targetIds = PositionRules.SnapshotAliveCombatantIds(state, enemyTeam);
            if (targetIds.Count == 0)
                return;

            var totalLifesteal = 0;
            var anyKill = false;

            foreach (var targetId in targetIds)
            {
                var target = state.GetCombatant(targetId);
                if (target == null || !target.IsAlive)
                    continue;

                var primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, value);
                primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(state, action, target, primaryPower);

                DamageRules.ApplyDamage(
                    state,
                    actor,
                    target,
                    primaryPower,
                    card.CardType,
                    events,
                    isSacrificeDamage: false,
                    rng: rng,
                    cardCost: card.Cost,
                    ignoreDefPercent: action.IgnoreDefPercent,
                    sourceCardInstanceId: sourceCardInstanceId,
                    isAoEWave: true);

                if (state.LastAction.DamageAmount > 0)
                    totalLifesteal += state.LastAction.DamageAmount;
                if (state.LastAction.WasKill)
                    anyKill = true;
            }

            var lifestealPercent = action.LifestealPercent;
            if (lifestealPercent <= 0)
                lifestealPercent = CombatMechanicsRules.GetPendingLifestealPercent(actor);

            if (lifestealPercent > 0 && totalLifesteal > 0)
                CombatMechanicsRules.ApplyLifesteal(state, actor, totalLifesteal, lifestealPercent, events);

            if (action.OnKillHealAmount > 0 && anyKill)
                DamageRules.ApplyHeal(state, actor, action.OnKillHealAmount, events, actor);
        }

        static void ExecuteDamage(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState target,
            int value,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId,
            bool isSacrificeSelfDamage = false)
        {
            if (target == null)
                return;

            var isSacrifice = isSacrificeSelfDamage;
            var primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, value);
            primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(state, action, target, primaryPower);

            var lifestealPercent = action.LifestealPercent;
            if (lifestealPercent <= 0)
                lifestealPercent = CombatMechanicsRules.GetPendingLifestealPercent(actor);

            var splashTargetId = action.SplashBehindTarget
                ? PositionRules.SnapshotCombatantBehindId(state, target)
                : null;

            DamageRules.ApplyDamage(
                state,
                actor,
                target,
                primaryPower,
                card.CardType,
                events,
                isSacrificeDamage: isSacrifice,
                rng: rng,
                cardCost: card.Cost,
                ignoreDefPercent: action.IgnoreDefPercent,
                sourceCardInstanceId: sourceCardInstanceId);

            if (lifestealPercent > 0 && state.LastAction.DamageAmount > 0)
            {
                CombatMechanicsRules.ApplyLifesteal(
                    state, actor, state.LastAction.DamageAmount, lifestealPercent, events);
            }

            if (action.OnKillHealAmount > 0 && state.LastAction.WasKill)
                DamageRules.ApplyHeal(state, actor, action.OnKillHealAmount, events, actor);

            if (action.SplashBehindTarget && !string.IsNullOrEmpty(splashTargetId))
            {
                var behind = state.GetCombatant(splashTargetId);
                if (behind != null && behind.IsAlive)
                {
                    var splashPower = System.Math.Max(1,
                        (int)System.Math.Round(primaryPower * action.SplashPowerPercent / 100f));
                    DamageRules.ApplyDamage(state, actor, behind, splashPower, card.CardType, events,
                        rng: rng, cardCost: card.Cost, ignoreDefPercent: action.IgnoreDefPercent,
                        sourceCardInstanceId: sourceCardInstanceId);
                }
            }
        }
    }
}
