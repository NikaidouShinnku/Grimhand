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
            var target = TargetRules.ResolveTarget(state, actor, action.Target, card.InstanceId, rng, action);
            var value = action.Type == EffectActionType.DealDamage
                ? CombatMechanicsRules.ComputeActionValueForTarget(state, action, actor, target)
                : CardPowerRules.ComputeActionValue(action, actor);
            if (action.Type == EffectActionType.DealDamage
                && action.Target == EffectTarget.Self
                && card.Keywords.Contains("sacrifice"))
            {
                value = RelicEffectRules.AdjustSacrificeSelfDamage(
                    state, state.Config?.RunModifiers, actor, value);
            }

            var beneficiary = target ?? actor;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    if (action.Target == EffectTarget.AllEnemies)
                        ExecuteDamageToAllEnemies(state, actor, card, action, value, events, rng, sourceCardInstanceId);
                    else if (target != null
                             && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
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
                    DamageRules.ApplyBlock(beneficiary, totalBlock, events, state, rng);
                    TalentBattleRules.AfterDefenseBlockApplied(
                        state, actor, beneficiary, totalBlock, events, rng);
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
                    if (action.Target == EffectTarget.AllEnemies)
                        ExecuteStatusToAllEnemies(state, actor, card, action, events);
                    else if (action.Target == EffectTarget.RandomEnemies)
                        ExecuteStatusToRandomEnemies(state, actor, action, events, rng);
                    else if (action.Target == EffectTarget.RandomEnemy)
                    {
                        var randomTarget = TargetRules.ResolveTarget(
                            state, actor, EffectTarget.RandomEnemy, card.InstanceId, rng, action);
                        if (randomTarget != null)
                            ApplyStatusWithTalents(state, actor, randomTarget, action, events);
                    }
                    else if (target != null
                             && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                        ApplyStatusWithTalents(state, actor, target, action, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ApplyAnubisAvatar:
                    AnubisAvatarRules.Apply(state, actor, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
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
                        DamageRules.ApplyBlock(actor, blockFromDamage, events, state, rng);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    }

                    break;
                }
                case EffectActionType.LockRandomPlayerPlaysThisTurn:
                    ApplyRandomPlayerPlayLock(state, actor, events, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ReducePlayerEnergyRegenNextTurn:
                    if (action.Value > 0)
                    {
                        state.PendingPlayerEnergyRegenPenaltyNextTurn += action.Value;
                        events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                            $"下回合能量回复 -{action.Value}")
                        {
                            CombatantId = actor.Id,
                            Amount = action.Value
                        });
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ArmRespondDamageRedirect:
                    DefenderRespondArmRules.ArmRedirectDouble(state, actor.Id);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    break;
                case EffectActionType.SummonOrGainBlock:
                    ExecuteSummonOrGainBlock(state, actor, action, events, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.GrantDodgeChance:
                    if (action.Value > 0)
                    {
                        actor.DodgeChanceBonus = action.Value / 100f;
                        events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                            $"{actor.DisplayName} 闪避率 +{action.Value}%")
                        {
                            CombatantId = actor.Id,
                            Amount = action.Value
                        });
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    break;
            }

            if (action.SelfDamageFlat > 0 && actor.IsAlive)
            {
                DamageRules.ApplyDamage(
                    state, actor, actor, action.SelfDamageFlat, CardType.Status, events,
                    canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                    sourceCardInstanceId: sourceCardInstanceId);
            }
        }

        static void ApplyRandomPlayerPlayLock(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var pool = new List<CombatantState>();
            foreach (var unit in state.GetTeam(TeamSide.Player))
            {
                if (unit.IsAlive)
                    pool.Add(unit);
            }

            if (pool.Count == 0)
                return;

            var index = rng != null ? rng.NextIndex(pool.Count) : 0;
            var victim = pool[index];
            victim.SkipRemainingPlaysThisTurn = true;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{victim.DisplayName} 被威慑，本回合后续出牌被打断")
            {
                CombatantId = victim.Id,
                TargetId = actor.Id
            });
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

                var primaryPower = CombatMechanicsRules.ComputeActionValueForTarget(state, action, actor, target);
                primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, primaryPower);
                primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(state, action, target, primaryPower);
                primaryPower = PassiveCardMechanicsRules.ApplyEndlessBladeMultiplier(state, card, primaryPower);

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

        static void ExecuteStatusToAllEnemies(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events)
        {
            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
            {
                var target = state.GetCombatant(targetId);
                if (target == null || !target.IsAlive)
                    continue;

                ApplyStatusWithTalents(state, actor, target, action, events);
            }
        }

        static void ApplyStatusWithTalents(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            EffectActionSpec action,
            List<BattleEvent> events)
        {
            var stacks = action.Stacks;
            TalentBattleRules.AdjustPoisonStacks(state, actor, ref stacks);
            StatusRules.ApplyStatus(state, target, action.StatusId, stacks, action.Duration, events);
        }

        static void ExecuteStatusToRandomEnemies(
            BattleState state,
            CombatantState actor,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var count = action.Value > 0 ? action.Value : 1;
            foreach (var target in TargetRules.PickRandomEnemies(state, actor.Team, count, rng))
                ApplyStatusWithTalents(state, actor, target, action, events);
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

            var hitCount = System.Math.Max(1, action.HitCount);
            var repeatTimes = 1;
            if (action.RepeatPerEnemyAttackCardThisTurn > 0)
            {
                repeatTimes = state.EnemyAttackCardsPlayedThisTurn;
                if (card.CardType == CardType.Attack)
                    repeatTimes += 1;
                repeatTimes = System.Math.Max(1, repeatTimes);
            }

            for (var repeat = 0; repeat < repeatTimes; repeat++)
            {
                var damageTarget = target;
                if (action.RepeatPerEnemyAttackCardThisTurn > 0 && repeat > 0)
                {
                    damageTarget = TargetRules.ResolveTarget(
                        state, actor, EffectTarget.RandomEnemy, card.InstanceId, rng, action);
                    if (damageTarget == null)
                        continue;
                }

                for (var hit = 0; hit < hitCount; hit++)
                {
                    ApplySingleHit(
                        state, actor, card, action, damageTarget, value, events, rng,
                        sourceCardInstanceId, isSacrificeSelfDamage);
                }
            }
        }

        static void ApplySingleHit(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState target,
            int value,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId,
            bool isSacrificeSelfDamage)
        {
            if (target == null || !target.IsAlive)
                return;

            var primaryPower = CombatMechanicsRules.ComputeActionValueForTarget(state, action, actor, target);
            primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, primaryPower);
            primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(state, action, target, primaryPower);
            primaryPower = PassiveCardMechanicsRules.ApplyEndlessBladeMultiplier(state, card, primaryPower);

            var lifestealPercent = action.LifestealPercent;
            if (lifestealPercent <= 0 && !action.LifestealUnblockedOnly)
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
                isSacrificeDamage: isSacrificeSelfDamage,
                rng: rng,
                cardCost: card.Cost,
                ignoreDefPercent: action.IgnoreDefPercent,
                sourceCardInstanceId: sourceCardInstanceId);

            var hpDamage = state.LastAction.DamageAmount;
            if (isSacrificeSelfDamage && hpDamage > 0)
                TalentBattleRules.OnSacrificeHpSpent(state, actor, hpDamage);

            if (action.LifestealUnblockedOnly && hpDamage > 0)
                DamageRules.ApplyHeal(state, actor, hpDamage, events, actor, isLifesteal: true);
            else if (lifestealPercent > 0 && hpDamage > 0)
            {
                CombatMechanicsRules.ApplyLifesteal(
                    state, actor, hpDamage, lifestealPercent, events);
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

        static void ExecuteSummonOrGainBlock(
            BattleState state,
            CombatantState actor,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null)
                return;

            var slot = SummonRules.FindEmptyTeamSlot(state, actor.Team);
            if (slot.HasValue
                && !string.IsNullOrEmpty(action.SummonCharacterId)
                && state.Config.SummonTemplates.TryGetValue(action.SummonCharacterId, out var template))
            {
                SummonRules.SpawnFromTemplate(state, template, slot.Value, events);
                SummonRules.MergeSummonedSkillPoolIntoTeamDeck(state, template, actor.Team, rng, events);
                return;
            }

            var blockValue = action.FallbackBlockValue;
            if (action.FallbackBlockDefenseScalePercent > 0)
            {
                blockValue += (int)System.Math.Round(
                    actor.Defense * action.FallbackBlockDefenseScalePercent / 100f);
            }

            if (blockValue > 0)
                DamageRules.ApplyBlock(actor, blockValue, events, state, rng);
        }
    }
}
