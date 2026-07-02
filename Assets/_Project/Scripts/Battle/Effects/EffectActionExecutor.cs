using System;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
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

        public static void ExecuteOne(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId,
            CombatantState targetOverride = null)
        {
            var target = targetOverride
                         ?? TargetRules.ResolveTarget(state, actor, action.Target, card.InstanceId, rng, action);
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
            var sacrificeSelfDamageAppliedEarly = false;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    if (action.Target == EffectTarget.AllEnemies)
                        ExecuteDamageToAllEnemies(state, actor, card, action, value, events, rng, sourceCardInstanceId);
                    else if (target != null
                             && (action.Target == EffectTarget.Self
                                 || TargetRules.IsTargetValidForAction(state, target, action.Reach, action)))
                        ExecuteDamage(
                            state, actor, card, action, target, value, events, rng, sourceCardInstanceId,
                            isSacrificeSelfDamage: action.Target == EffectTarget.Self
                                && card.Keywords.Contains("sacrifice"));
                    break;
                case EffectActionType.GainBlock:
                {
                    if (action.SelfDamageFlat > 0
                        && card.Keywords.Contains("sacrifice")
                        && actor.IsAlive)
                    {
                        ApplySacrificeFlatSelfDamage(
                            state, actor, card, action, events, rng, sourceCardInstanceId);
                        sacrificeSelfDamageAppliedEarly = true;
                    }

                    var totalBlock = value + RelicBattleRules.GetOutgoingDefenseFlatBonus(
                        state.Config?.RunModifiers, actor);
                    totalBlock = RelicBattleRules.ApplyPharaohBlockBonus(
                        state.Config?.RunModifiers, actor, totalBlock);
                    DamageRules.ApplyBlock(beneficiary, totalBlock, events, state, rng);
                    TalentBattleRules.AfterDefenseBlockApplied(
                        state, actor, beneficiary, totalBlock, events, rng, card);
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
                        ExecuteStatusToRandomEnemies(state, actor, action, events, rng, card);
                    else if (action.Target == EffectTarget.RandomEnemy)
                    {
                        var randomTarget = TargetRules.ResolveTarget(
                            state, actor, EffectTarget.RandomEnemy, card.InstanceId, rng, action);
                        if (randomTarget != null)
                            ApplyStatusWithTalents(state, actor, randomTarget, action, events, card);
                    }
                    else if (target != null
                             && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                        ApplyStatusWithTalents(state, actor, target, action, events, card);
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
                    // v0.9：抽牌效果当回合立即抽到手中（配合 quick_start 可在本回合规划阶段直接使用）。
                    DeckRules.DrawCards(state, actor.Team, rng, value, events);
                    events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"立即抽 {value} 张牌")
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
                // ===== v0.9 新增动作类型 =====
                case EffectActionType.ConsumeBlockDealDamage:
                {
                    if (target == null)
                        break;
                    var blockConsumed = actor.Block;
                    if (blockConsumed <= 0 && action.Value <= 0)
                        break;
                    actor.Block = 0;
                    if (blockConsumed > 0)
                    {
                        events.Add(new BattleEvent(BattleEventKind.BlockGained,
                            $"{actor.DisplayName} 消耗护甲 {blockConsumed}")
                        {
                            CombatantId = actor.Id,
                            Amount = blockConsumed
                        });
                    }
                    var consumeDamage = blockConsumed + Math.Max(0, action.Value);
                    if (consumeDamage > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, consumeDamage, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DamagePerRespondCount:
                {
                    if (target == null)
                        break;
                    var count = state.RespondSuccessCount;
                    var dmg = Math.Max(0, count * action.Value);
                    if (dmg > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DoubleStatusStacks:
                {
                    if (target == null)
                        break;
                    DoubleTargetStatusStacks(target, StatusCatalog.Poison, events);
                    DoubleTargetStatusStacks(target, StatusCatalog.Burn, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target.Id, false, 0);
                    break;
                }
                case EffectActionType.RecycleExhaustCardsFromDiscard:
                {
                    RecycleExhaustCardsInDiscard(state, actor.Team, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageScaledByActorHpLoss:
                {
                    if (target == null)
                        break;
                    var basePower = value;
                    var step = Math.Max(1, action.HpLossStepPercent);
                    var lostPercent = Math.Max(0, (actor.MaxHp - actor.Hp) * 100 / Math.Max(1, actor.MaxHp));
                    var steps = lostPercent / step;
                    basePower += steps * Math.Max(0, action.HpLossStepValue);
                    if (basePower > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, basePower, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageAlternateIfHealedThisTurn:
                {
                    if (target == null)
                        break;
                    var dmg = actor.HealedThisTurn && action.AlternateValueIfHealed > 0
                        ? action.AlternateValueIfHealed
                        : value;
                    if (dmg > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageBonusPerTargetDebuffStack:
                {
                    if (target == null)
                        break;
                    var debuffStacks = CountDebuffStacks(target);
                    var dmg = value + debuffStacks * Math.Max(0, action.Stacks);
                    if (dmg > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                // ===== v0.9 毒蛇女王 / 巫妖女王 新增动作 =====
                case EffectActionType.GainEnergy:
                {
                    if (action.Value > 0 && actor.IsAlive)
                    {
                        state.EnergyCurrent = Math.Min(state.EnergyMax, state.EnergyCurrent + action.Value);
                        events.Add(new BattleEvent(BattleEventKind.EnergyChanged, $"获得 {action.Value} 能量")
                        {
                            CombatantId = actor.Id,
                            Energy = state.EnergyCurrent,
                            EnergyMax = state.EnergyMax,
                            EnergyRemaining = state.EnergyCurrent,
                            Amount = action.Value
                        });
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DrawToHandLimit:
                {
                    var hand = state.GetHand(actor.Team);
                    var draw = Math.Max(0, state.Config.HandLimit - hand.Count);
                    if (draw > 0)
                        DeckRules.DrawCards(state, actor.Team, rng, draw, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.GainBlockBonusIfSelfPoisoned:
                {
                    var totalBlock = value;
                    if (StatusRules.HasStatus(actor, StatusCatalog.Poison))
                        totalBlock += Math.Max(0, action.Stacks);
                    totalBlock += RelicBattleRules.GetOutgoingDefenseFlatBonus(state.Config?.RunModifiers, actor);
                    totalBlock = RelicBattleRules.ApplyPharaohBlockBonus(state.Config?.RunModifiers, actor, totalBlock);
                    DamageRules.ApplyBlock(beneficiary, totalBlock, events, state, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, beneficiary.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyPoisonBySpeedCompare:
                {
                    if (target != null
                        && TargetRules.IsTargetValidForAction(state, target, action.Reach, action)
                        && value > 0)
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, value, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    if (target != null)
                    {
                        var actorSpeed = StatusRules.GetEffectiveSpeed(state, actor);
                        var targetSpeed = StatusRules.GetEffectiveSpeed(state, target);
                        var stacks = targetSpeed < actorSpeed ? Math.Max(1, action.Stacks) : 1;
                        var duration = targetSpeed < actorSpeed ? Math.Max(1, action.Duration) : Math.Max(1, action.Duration);
                        StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, stacks, duration, events);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.RemovePoisonHealPerStack:
                {
                    V09NewMechanicsRules.RemovePoisonHealPerStack(state, actor, action.Value, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.TransferHalfPoisonToRandomEnemy:
                {
                    V09NewMechanicsRules.TransferHalfPoisonToRandomEnemy(state, actor, rng, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyConstrict:
                {
                    if (target != null && target.Team != actor.Team)
                        V09NewMechanicsRules.ApplyConstrict(state, actor, target, action.Value, action.Duration, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.SettlePoisonAndClear:
                {
                    if (action.Target == EffectTarget.AllEnemies)
                    {
                        var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                        foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                        {
                            var t = state.GetCombatant(targetId);
                            if (t != null && t.IsAlive)
                                V09NewMechanicsRules.SettlePoisonAndClear(state, actor, t, events);
                        }
                    }
                    else if (target != null)
                        V09NewMechanicsRules.SettlePoisonAndClear(state, actor, target, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyDelayedDamage:
                {
                    if (target != null)
                        StatusRules.ApplyStatus(state, target, StatusCatalog.DelayedDamage, action.Value, 1, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.EtherealCountBonusDamage:
                {
                    var bonus = V09NewMechanicsRules.GetEtherealEntryCount(state) * Math.Max(0, action.Stacks);
                    var dmg = value + bonus;
                    if (action.Target == EffectTarget.AllEnemies)
                    {
                        var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                        foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                        {
                            var t = state.GetCombatant(targetId);
                            if (t != null && t.IsAlive)
                                DamageRules.ApplyDamage(state, actor, t, dmg, card.CardType, events,
                                    canTriggerParry: true, rng: rng, cardCost: card.Cost,
                                    sourceCardInstanceId: sourceCardInstanceId, isAoEWave: true);
                        }
                    }
                    else if (target != null && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                        DamageRules.ApplyDamage(state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            sourceCardInstanceId: sourceCardInstanceId);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.AddTokenCardToHand:
                {
                    V09NewMechanicsRules.AddTokenCardToHand(state, actor, action.TokenCardId, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ShuffleHandCosts:
                {
                    V09NewMechanicsRules.ShuffleHandCosts(state, actor, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.RandomSnakeGodEffect:
                {
                    ExecuteRandomSnakeGodEffect(state, actor, card, action, events, rng, sourceCardInstanceId);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, "", false, 0);
                    break;
                }
                case EffectActionType.SealNextEnemyCard:
                {
                    // 占位：对敌方施加封印状态（TODO：敌方下张牌失效逻辑待实装）
                    if (target != null && target.Team != actor.Team)
                        StatusRules.ApplyStatus(state, target, StatusCatalog.SealedNextCard, 1, 2, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.LockSelfCards:
                {
                    if (actor != null && actor.IsAlive)
                        actor.CardsLockedTurnsRemaining = Math.Max(actor.CardsLockedTurnsRemaining, Math.Max(1, action.Value));
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DrawCardsIfEthereal:
                {
                    var draw = StatusRules.HasStatus(actor, StatusCatalog.Ethereal) && action.AlternateValue > 0
                        ? action.AlternateValue
                        : Math.Max(0, action.Value);
                    if (draw > 0)
                        DeckRules.DrawCards(state, actor.Team, rng, draw, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.BuffAllOtherAllies:
                {
                    foreach (var ally in state.GetTeam(actor.Team))
                    {
                        if (ally == null || !ally.IsAlive || ally.Id == actor.Id)
                            continue;
                        StatusRules.ApplyStatus(state, ally, action.StatusId, Math.Max(1, action.Stacks), action.Duration, events);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.RevealEnemyIntent:
                {
                    // 占位：看破敌人意图系统尚未实装；暂抽 1 牌。
                    DeckRules.DrawCards(state, actor.Team, rng, 1, events);
                    events.Add(new BattleEvent(BattleEventKind.StatusApplied, "TODO: 恐惧低语 看破敌人意图")
                    {
                        CombatantId = actor.Id
                    });
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
            }

            if (action.SelfDamageFlat > 0 && actor.IsAlive && !sacrificeSelfDamageAppliedEarly)
            {
                ApplySacrificeFlatSelfDamage(
                    state, actor, card, action, events, rng, sourceCardInstanceId);
            }
        }

        static void ApplySacrificeFlatSelfDamage(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            var dmg = RelicEffectRules.AdjustSacrificeSelfDamage(
                state, state.Config?.RunModifiers, actor, action.SelfDamageFlat);
            DamageRules.ApplyDamage(
                state, actor, actor, dmg, CardType.Status, events,
                canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                sourceCardInstanceId: sourceCardInstanceId);
            if (state.LastAction.DamageAmount > 0)
            {
                TalentBattleRules.OnSacrificeHpSpent(state, actor, state.LastAction.DamageAmount);
                PassiveCardMechanicsRules.TryTriggerBloodFrenzyOnSacrifice(state, actor, events);
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

                ApplyStatusWithTalents(state, actor, target, action, events, card);
            }
        }

        static void ApplyStatusWithTalents(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            EffectActionSpec action,
            List<BattleEvent> events,
            CardInstanceState card = null)
        {
            var stacks = action.Stacks;
            if (card?.DefinitionId == PassiveCardMechanicsRules.FinalBindCardId
                && action.StatusId == StatusCatalog.Poison)
                stacks = PassiveCardMechanicsRules.ResolveFinalBindPoisonStacks(state, target, stacks);

            // v0.9 毒囊破裂：施毒者有 venom_sac_burst 时 +1 层中毒
            stacks = V09NewMechanicsRules.AdjustPoisonStacksForVenomSac(actor, action.StatusId, stacks);

            var duration = action.Duration;
            if (action.StatusId == StatusCatalog.Poison)
            {
                TalentBattleRules.AdjustPoisonStacks(state, actor, ref stacks);
                duration = TalentBattleRules.AdjustPoisonDuration(state, actor, duration);
            }
            StatusRules.ApplyStatus(state, target, action.StatusId, stacks, duration, events);

            if (!action.SplashBehindTarget || target == null)
                return;

            var splashTargetId = PositionRules.SnapshotCombatantBehindId(state, target);
            if (string.IsNullOrEmpty(splashTargetId))
                return;

            var behind = state.GetCombatant(splashTargetId);
            if (behind == null || !behind.IsAlive)
                return;

            var splashStacks = stacks;
            if (action.SplashPowerPercent > 0 && action.SplashPowerPercent < 100)
            {
                splashStacks = System.Math.Max(1,
                    (int)System.Math.Round(stacks * action.SplashPowerPercent / 100f));
            }

            StatusRules.ApplyStatus(state, behind, action.StatusId, splashStacks, action.Duration, events);
        }

        static void ExecuteStatusToRandomEnemies(
            BattleState state,
            CombatantState actor,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            CardInstanceState card = null)
        {
            var count = action.Value > 0 ? action.Value : 1;
            foreach (var target in TargetRules.PickRandomEnemies(state, actor.Team, count, rng))
                ApplyStatusWithTalents(state, actor, target, action, events, card);
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

            PassiveCardMechanicsRules.PrepareGargoyleSunderTarget(state, target, card, events);

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
                    var hitTarget = damageTarget;
                    if (action.Target == EffectTarget.RandomEnemy)
                    {
                        hitTarget = TargetRules.ResolveTarget(
                            state, actor, EffectTarget.RandomEnemy, card.InstanceId, rng, action);
                        if (hitTarget == null)
                            continue;
                    }
                    ApplySingleHit(
                        state, actor, card, action, hitTarget, value, events, rng,
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

            if (card.Keywords.Contains("respond_status") && state.PlayerRespondStatusUsedThisTurn)
                primaryPower = System.Math.Max(1, primaryPower * 3);

            var lifestealPercent = action.LifestealPercent;
            if (lifestealPercent <= 0 && !action.LifestealUnblockedOnly)
                lifestealPercent = CombatMechanicsRules.GetPendingLifestealPercent(actor);

            var splashTargetId = action.SplashBehindTarget
                ? PositionRules.SnapshotCombatantBehindId(state, target)
                : null;

            var targetHadBlock = target.Block > 0;

            DamageRules.ApplyDamage(
                state,
                actor,
                target,
                primaryPower,
                isSacrificeSelfDamage ? CardType.Status : card.CardType,
                events,
                isSacrificeDamage: isSacrificeSelfDamage,
                canTriggerParry: !isSacrificeSelfDamage,
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

            PassiveCardMechanicsRules.AfterSingleHitResolved(
                state, actor, card, target, targetHadBlock, events, rng);

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
            if (blockValue > 0)
                DamageRules.ApplyBlock(actor, blockValue, events, state, rng);
        }

        // ===== v0.9 新增动作类型的辅助方法 =====

        static void ExecuteRandomSnakeGodEffect(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            if (state == null || actor == null || rng == null)
                return;

            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var roll = rng.NextIndex(3);
            if (roll == 0)
            {
                // AOE 造成 Value 伤害
                foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                {
                    var t = state.GetCombatant(targetId);
                    if (t != null && t.IsAlive)
                        DamageRules.ApplyDamage(state, actor, t, action.Value, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            sourceCardInstanceId: sourceCardInstanceId, isAoEWave: true);
                }
            }
            else if (roll == 1)
            {
                // AOE 施加 Stacks 层中毒（永久）
                foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                {
                    var t = state.GetCombatant(targetId);
                    if (t != null && t.IsAlive)
                        StatusRules.ApplyStatus(state, t, StatusCatalog.Poison, Math.Max(1, action.Stacks), -1, events);
                }
            }
            else
            {
                // 随机一名敌人造成 AlternateValue 伤害
                var pick = TargetRules.PickRandomEnemies(state, actor.Team, 1, rng);
                if (pick != null && pick.Count > 0 && pick[0] != null)
                    DamageRules.ApplyDamage(state, actor, pick[0], action.AlternateValue, card.CardType, events,
                        canTriggerParry: false, rng: rng, cardCost: card.Cost,
                        sourceCardInstanceId: sourceCardInstanceId);
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "蛇神的回应降临")
            {
                CombatantId = actor.Id,
                Amount = roll
            });
        }

        static void DoubleTargetStatusStacks(CombatantState target, string statusId, List<BattleEvent> events)
        {
            var existing = StatusRules.FindStatus(target, statusId);
            if (existing == null || existing.Stacks <= 0)
                return;
            existing.Stacks *= 2;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{target.DisplayName} {StatusCatalog.Get(statusId)?.DisplayName} 层数翻倍")
            {
                CombatantId = target.Id,
                Amount = existing.Stacks,
                TargetId = statusId
            });
        }

        static int CountDebuffStacks(CombatantState target)
        {
            if (target == null)
                return 0;
            var total = 0;
            foreach (var status in target.Statuses)
            {
                if (status.Stacks <= 0)
                    continue;
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;
                // 视为减益：跳伤（中毒/灼烧）、减速、虚弱、破损、易伤、减伤对自身无意义但属负面修饰
                if (def.TurnStartDamagePerStack > 0
                    || def.TurnEndDamagePerStack > 0
                    || def.SpeedModifierPerStack < 0
                    || def.OutgoingDamageReductionFlatPerStack > 0
                    || def.BlockGainReductionPercentPerStack > 0
                    || def.IncomingDamagePercentPerStack > 0)
                {
                    total += status.Stacks;
                }
            }
            return total;
        }

        static void RecycleExhaustCardsInDiscard(BattleState state, TeamSide team, List<BattleEvent> events)
        {
            var discard = state.GetDiscardPile(team);
            var draw = state.GetDrawPile(team);
            var moved = 0;
            for (var i = discard.Count - 1; i >= 0; i--)
            {
                var card = discard[i];
                if (card == null || !card.Keywords.Contains("exhaust"))
                    continue;
                card.Keywords.Remove("exhaust");
                discard.RemoveAt(i);
                draw.Add(card);
                moved++;
            }

            if (moved > 0)
            {
                events.Add(new BattleEvent(BattleEventKind.CardDrawn,
                    $"神圣轮回：将 {moved} 张消耗牌洗回抽牌堆并祛除消耗")
                {
                    Amount = moved
                });
            }
        }
    }
}
